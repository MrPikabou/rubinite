using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RubiniteDarkRuby
{
    internal static class DarkRubySkin
    {
        private static SpriteRenderer _cachedGirlSr;
        private static Material _drCharacterMaterial;
        private static Material _drPortraitMaterial;
        private static int _lastPortraitAttemptFrame = -1;
        private static int _ddolCheckFrame = -1;
        private static bool _ddolExists;
        
        private static ChangeRubySkin[] _cachedCrsList;
        private static int _lastCrsScanFrame = -1;
        private const int CrsScanInterval = 10;

        private static SpriteRenderer[] _cachedPlayerGirls;
        private static int _playerGirlsFrame = -1;
        private const int PlayerGirlsCacheFrames = 30;

        public const string MaterialName = "HardcodedDarkRubyMaterial";
        private const string FallbackVanillaSkinId = "eb9dd477-a430-48f8-8a7d-f721ea740a08";

        private static readonly (float tr, float tg, float tb, float ta,
                                 float nr, float ng, float nb, float na, float tol)[] ColorSlots = new[]
        {
            (0.80f, 0.00f, 0.00f, 1f,  0.21f, 0.21f, 0.21f, 1f,  0.97f),
            (0.58f, 0.06f, 0.06f, 1f,  0.14f, 0.14f, 0.14f, 1f,  0.97f),
            (0.39f, 0.08f, 0.08f, 1f,  0.09f, 0.09f, 0.09f, 1f,  0.97f),
            (0.99f, 0.56f, 0.56f, 1f,  0.57f, 0.57f, 0.57f, 1f,  0.97f),
            (0.80f, 0.44f, 0.50f, 1f,  0.37f, 0.37f, 0.37f, 1f,  0.97f),
            (0.15f, 0.09f, 0.07f, 1f,  0.10f, 0.10f, 0.10f, 1f,  0.97f),
            (0.35f, 0.24f, 0.19f, 1f,  0.14f, 0.14f, 0.14f, 1f,  0.97f),
            (0.49f, 0.35f, 0.29f, 1f,  0.16f, 0.16f, 0.16f, 1f,  0.97f),
            (1.00f, 0.85f, 0.75f, 1f,  0.51f, 0.51f, 0.51f, 1f,  0.97f),
            (0.99f, 0.99f, 0.98f, 1f,  0.48f, 0.48f, 0.48f, 1f,  0.18f),
        };

        public static bool IsActive =>
            RubiniteDarkRubyPlugin.Instance?.State?.SkinSelected ?? false;
        public static bool IsSkinSystemEnabled =>
            RubiniteDarkRubyPlugin.Instance?.Settings?.UseCustomSkin?.Value == true;
        public static bool IsVanillaFrameFocused { get; set; } = false;
        
        /// <summary>
        /// Central gate: should the Dark Ruby skin be applied to this specific renderer right now?
        /// Rules:
        ///   - Dark Ruby skin must be selected globally
        ///   - Never in TitleMenu or GameStartup (bootstrap)
        ///   - Never for renderers that physically belong to the TitleMenu scene
        ///     (they can outlive the scene transition through DontDestroyOnLoad)
        /// </summary>
        public static bool ShouldApplyToRenderer(SpriteRenderer sr)
        {
            if (sr == null) return false;
            if (!IsActive) return false;
            if (IsVanillaFrameFocused) return false;

            var activeScene = SceneManager.GetActiveScene().name;
            if (activeScene == "GameStartup") return false;

            var srScene = sr.gameObject.scene.name;
            if (srScene == "TitleMenu")
            {
                var player = GamePlayCore.Instance?.player;
                if (player == null) return false;
                if (!player.gameObject.activeInHierarchy) return false;
            }

            return true;
        }

        private static bool HasDdolGirlRenderer()
        {
            if (Time.frameCount == _ddolCheckFrame) return _ddolExists;
            _ddolCheckFrame = Time.frameCount;
            _ddolExists = false;
            foreach (var s in UnityEngine.Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                if (s == null || s.name != "Girl") continue;
                if (s.gameObject.scene.name == "DontDestroyOnLoad")
                {
                    _ddolExists = true;
                    break;
                }
            }
            return _ddolExists;
        }

        public static void Apply()
        {
            if (!IsSkinSystemEnabled) return;
            var player = GamePlayCore.Instance?.player;
            if (player != null)
            {
                foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.name != "Girl") continue;
                    if (sr.material == null) continue;
                    if (!ShouldApplyToRenderer(sr)) continue;
                    if (sr.material.name.StartsWith(MaterialName)) continue;
                    ApplyToRenderer(sr);
                }
            }

            ApplyToPortraits();
        }

        /// <summary>
        /// Applies the DR material to every active portrait ChangeRubySkin
        /// (the character preview in the skin menu). Inactive menus means
        /// FindObjectsOfType returns nothing — no per-frame cost when the
        /// skin menu isn't open.
        /// </summary>
        public static void ApplyToPortraits()
        {
            if (!IsSkinSystemEnabled) return;
            if (!IsActive) return;
            if (IsVanillaFrameFocused) return;

            var player = GamePlayCore.Instance?.player;
            bool playerActive = player != null && player.gameObject.activeInHierarchy;
            var crsList = GetCachedChangeRubySkins();

            for (int i = 0; i < crsList.Length; i++)
            {
                var crs = crsList[i];
                if (crs == null) continue;

                var srScene = crs.gameObject.scene.name;
                bool inCA = srScene == "ChallengeArea";

                // Skip podium previews - only CA-scene has podiums, so only there
                // do we need the expensive hierarchy walk.
                if (inCA)
                {
                    var t = crs.transform;
                    bool underPodium = false;
                    while (t != null)
                    {
                        if (t.GetComponent<ChallengeLevelPoint>() != null) { underPodium = true; break; }
                        t = t.parent;
                    }
                    if (underPodium) continue;
                }

                if (srScene == "TitleMenu" && !playerActive)
                {
                    if (IsActive)
                    {
                        if (crs.image != null)
                            ApplyToPortraitImage(crs.image, forcePreview: false);
                        else if (crs.spriteRenderer != null)
                            ApplyToRenderer(crs.spriteRenderer);
                    }
                    continue;
                }

                if (crs.image != null)
                {
                    ApplyToPortraitImage(crs.image);
                    continue;
                }

                if (crs.spriteRenderer == null) continue;
                if (crs.isPortrait)
                {
                    if (crs.spriteRenderer.material != null
                        && crs.spriteRenderer.material.name.StartsWith(MaterialName + "_Portrait")) continue;
                    ApplyToRenderer(crs.spriteRenderer);
                    continue;
                }

                if (crs.spriteRenderer.gameObject.name == "Girl")
                {
                    if (!ShouldApplyToRenderer(crs.spriteRenderer)) continue;
                    if (crs.spriteRenderer.material != null
                        && crs.spriteRenderer.material.name.StartsWith(MaterialName)) continue;
                    ApplyToRenderer(crs.spriteRenderer);
                    continue;
                }
            }
        }

        public static void ApplyToPortraitPreview()
        {
            if (!IsSkinSystemEnabled) return;
            var crsList = GetCachedChangeRubySkins();
            for (int i = 0; i < crsList.Length; i++)
            {
                var crs = crsList[i];
                if (crs == null || !crs.isPortrait) continue;

                // Skip podium previews
                var t = crs.transform;
                bool underPodium = false;
                while (t != null)
                {
                    if (t.GetComponent<ChallengeLevelPoint>() != null) { underPodium = true; break; }
                    t = t.parent;
                }
                if (underPodium) continue;

                if (crs.image != null)
                    ApplyToPortraitImage(crs.image, forcePreview: true);
                else if (crs.spriteRenderer != null)
                    ApplyToRenderer(crs.spriteRenderer);
            }
        }

        public static void RestoreVanillaPreview(ChangeRubySkin crs)
        {
            if (crs == null) return;
            var overview = SingletonScriptableObject<ItemDataOverview>.Instance;
            var state = RubiniteDarkRubyPlugin.Instance?.State;
            if (overview == null) return;

            ItemData item = null;
            string source = null;

            var gameSkin = GameSetting.Instance?.rubySkinID?.GetValue();
            if (!string.IsNullOrEmpty(gameSkin) && gameSkin != DarkRubySkinEntry.SkinId)
            {
                item = overview.FindUniqueId(gameSkin);
                if (item != null) source = "GameSetting";
            }

            if (!string.IsNullOrEmpty(state?.LastVanillaSkinId)
                && state.LastVanillaSkinId != DarkRubySkinEntry.SkinId)
            {
                item = overview.FindUniqueId(state.LastVanillaSkinId);
                if (item != null) source = "LastVanilla";
                else
                {
                    RubiniteDarkRubyPlugin.Log?.LogWarning(
                        "[RestorePreview] LastVanillaSkinId='" 
                        + state.LastVanillaSkinId + "' did not resolve via FindUniqueId");
                }
            }

            if (item == null && overview.currentSkinIDFlag != null)
            {
                var flag = overview.currentSkinIDFlag.GetValue();
                if (!string.IsNullOrEmpty(flag) && flag != DarkRubySkinEntry.SkinId)
                {
                    item = overview.FindUniqueId(flag);
                    if (item != null) source = "currentFlag";
                }
            }

            if (item == null)
            {
                item = overview.FindUniqueId(overview.defaultSkinItem?.Id);
                source = "default";
            }

            if (RubiniteDarkRubyPlugin.Instance?.Settings?.LogSkinDiagnostics?.Value == true)
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "[RestorePreview] source=" + (source ?? "none")
                    + " item='" + (item?.DataId ?? "<null>") + "'");
            }

            if (item == null) return;

            if (crs.image != null)
            {
                var target = item.skinMaterial_Portrait;
                if (target == null || target.name.StartsWith(MaterialName))
                    target = crs.image.defaultMaterial;
                if (target != null) crs.image.material = target;
            }
            if (crs.spriteRenderer != null)
            {
                var target = item.skinMaterial;
                if (target == null || target.name.StartsWith(MaterialName))
                {
                    var shared = crs.spriteRenderer.sharedMaterial;
                    if (shared != null && !shared.name.StartsWith(MaterialName))
                        target = shared;
                }
                if (target != null) crs.spriteRenderer.material = target;
            }

            if (overview.currentSkinIDFlag != null && item != null 
                && !string.IsNullOrEmpty(item.DataId))
            {
                overview.currentSkinIDFlag.SetValue(item.DataId);

                if (GameSetting.Instance?.rubySkinID != null)
                {
                    GameSetting.Instance.rubySkinID.SetValue(item.DataId, "RubiniteDarkRuby");
                }
                try { DataSaver.SaveFlag(DataSaver.path_saveFolder); } catch { }
            }
        }

        public static void ApplyToPortraitImage(UnityEngine.UI.Image img, bool forcePreview = false)
        {
            if (img == null) return;
            if (!forcePreview && !IsActive) return;
            if (!IsSkinSystemEnabled) return;
            if (IsVanillaFrameFocused) return;

            if (img.gameObject.scene.name == "TitleMenu")
            {
                var player = GamePlayCore.Instance?.player;
                if (player == null || !player.gameObject.activeInHierarchy) return;
            }

            if (img.material != null && img.material.name.StartsWith(MaterialName + "_Portrait")) return;

            var mat = GetOrCreatePortraitMaterial(SingletonScriptableObject<ItemDataOverview>.Instance);
            if (mat == null) return;
            img.material = mat;
        }

        public static void ApplyColorSlotsToMaterial(Material mat)
        {
            if (mat == null) return;

            for (int i = 0; i < ColorSlots.Length; i++)
            {
                int slot = i + 1;
                string suffix = slot == 1 ? "" : slot.ToString();
                var s = ColorSlots[i];
                mat.SetColor("_ColorChangeTarget" + suffix, new Color(s.tr, s.tg, s.tb, s.ta));
                mat.SetColor("_ColorChangeNewCol" + suffix, new Color(s.nr, s.ng, s.nb, s.na));
                mat.SetFloat("_ColorChangeTolerance" + suffix, s.tol);
                mat.EnableKeyword("CHANGECOLOR" + slot + "_ON");
            }

            mat.SetFloat("_ColorChangeLuminosity", 1f);
            mat.SetFloat("_HsvSaturation", 0f);
            mat.SetFloat("_HsvBright", 2.0f);
            mat.EnableKeyword("HSV_ON");

            mat.SetFloat("_GlitchAmount", 5.3f);
            mat.SetFloat("_GlitchSize", 0.83f);
            mat.SetFloat("_DistortAmount", 0.5f);
            mat.SetFloat("_DistortTexXSpeed", 5f);
            mat.SetFloat("_DistortTexYSpeed", 5f);

            mat.EnableKeyword("GRADIENT2COL_ON");
            mat.EnableKeyword("GRADIENTCOLORRAMP_ON");

            mat.SetFloat("_ChangeColorBeforeTint", 1f);
        }

        public static void ApplyToRenderer(SpriteRenderer sr)
        {
            if (sr == null || sr.material == null) return;
            if (!ShouldApplyToRenderer(sr))
            {
                if (RubiniteDarkRubyPlugin.Instance?.Settings?.LogSkinDiagnostics?.Value == true)
                {
                    RubiniteDarkRubyPlugin.Log?.LogInfo(
                        "[SkinDebug] Skip: scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                        + " srScene=" + sr.gameObject.scene.name
                        + " active=" + sr.gameObject.activeInHierarchy
                        + " vanillaFocus=" + IsVanillaFrameFocused
                        + " IsActive=" + IsActive
                        + " isSkinEnabled=" + IsSkinSystemEnabled);
                }
                return;
            }
            if (sr.material.name.StartsWith(MaterialName)) { EnsureEnforcer(sr); return; }
            var mat = GetOrCreateCharacterMaterial();
            if (mat == null) return;
            sr.material = mat;
            EnsureEnforcer(sr);
        }

        /// <summary>Adds the render-time enforcer component if not already present.</summary>
        private static void EnsureEnforcer(SpriteRenderer sr)
        {
            if (sr.GetComponent<DarkRubyMaterialEnforcer>() != null) return;
            sr.gameObject.AddComponent<DarkRubyMaterialEnforcer>();
        }

        public static Shader GetPlayerShader()
        {
            var player = GamePlayCore.Instance?.player;
            if (player == null) return null;
            foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.name != "Girl") continue;
                if (sr.material == null) continue;
                return sr.material.shader;
            }
            return null;
        }

        public static Material CreateFreshMaterial(Texture texture)
        {
            // Build a material with the same DR keywords as the character, but use the
            // caller-provided texture as _MainTex (so it works for arbitrary sprites,
            // not just the character atlas).
            var template = Shader.Find("Sprites/Default");
            if (template == null) template = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (template == null) return null;

            var mat = new Material(template);
            mat.name = MaterialName + "_Pigment";

            if (texture != null) mat.SetTexture("_MainTex", texture);

            for (int i = 0; i < ColorSlots.Length; i++)
            {
                int slot = i + 1;
                string suffix = slot == 1 ? "" : slot.ToString();
                var s = ColorSlots[i];
                mat.SetColor("_ColorChangeTarget" + suffix, new Color(s.tr, s.tg, s.tb, s.ta));
                mat.SetColor("_ColorChangeNewCol" + suffix, new Color(s.nr, s.ng, s.nb, s.na));
                mat.SetFloat("_ColorChangeTolerance" + suffix, s.tol);
                mat.EnableKeyword("CHANGECOLOR" + slot + "_ON");
            }

            mat.SetFloat("_ColorChangeLuminosity", 1f);
            mat.SetFloat("_HsvSaturation", 0f);
            mat.SetFloat("_HsvBright", 2.0f);
            mat.EnableKeyword("HSV_ON");

            mat.SetFloat("_GlitchAmount", 5.3f);
            mat.SetFloat("_GlitchSize", 0.83f);
            mat.SetFloat("_DistortAmount", 0.5f);
            mat.SetFloat("_DistortTexXSpeed", 5f);
            mat.SetFloat("_DistortTexYSpeed", 5f);

            mat.EnableKeyword("GRADIENT2COL_ON");
            mat.EnableKeyword("GRADIENTCOLORRAMP_ON");

            return mat;
        }

        public static Material CreateFreshMaterialWithShader(Shader shader, Texture texture)
        {
            if (shader == null) return null;
            var mat = new Material(shader);
            mat.name = MaterialName + "_Pigment";
            if (texture != null) mat.SetTexture("_MainTex", texture);
            
            for (int i = 0; i < ColorSlots.Length; i++)
            {
                int slot = i + 1;
                string suffix = slot == 1 ? "" : slot.ToString();
                var s = ColorSlots[i];
                mat.SetColor("_ColorChangeTarget" + suffix, new Color(s.tr, s.tg, s.tb, s.ta));
                mat.SetColor("_ColorChangeNewCol" + suffix, new Color(s.nr, s.ng, s.nb, s.na));
                mat.SetFloat("_ColorChangeTolerance" + suffix, s.tol);
                mat.EnableKeyword("CHANGECOLOR" + slot + "_ON");
            }

            mat.SetFloat("_ColorChangeLuminosity", 1f);
            mat.SetFloat("_HsvSaturation", 0f);
            mat.SetFloat("_HsvBright", 2.0f);
            mat.EnableKeyword("HSV_ON");

            mat.SetFloat("_GlitchAmount", 5.3f);
            mat.SetFloat("_GlitchSize", 0.83f);
            mat.SetFloat("_DistortAmount", 0.5f);
            mat.SetFloat("_DistortTexXSpeed", 5f);
            mat.SetFloat("_DistortTexYSpeed", 5f);

            mat.EnableKeyword("GRADIENT2COL_ON");
            mat.EnableKeyword("GRADIENTCOLORRAMP_ON");

            return mat;
        }

        public static void ClearPortraitTints()
        {
            foreach (var portrait in UnityEngine.Object.FindObjectsOfType<ChangeRubySkin>())
            {
                if (portrait == null || !portrait.isPortrait) continue;

                if (portrait.image != null)
                {
                    if (portrait.image.material != null
                        && portrait.image.material.name.StartsWith(MaterialName + "_Portrait"))
                    {
                        portrait.ChangeSkin();
                    }
                }
                else if (portrait.spriteRenderer != null)
                {
                    if (portrait.spriteRenderer.material != null
                        && portrait.spriteRenderer.material.name.StartsWith(MaterialName))
                    {
                        portrait.ChangeSkin();
                    }
                }
            }
        }

        public static SpriteRenderer GetGirlSr()
        {
            if (_cachedGirlSr != null) return _cachedGirlSr;
            var player = GamePlayCore.Instance?.player;
            if (player == null) return null;
            foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.name == "Girl") { _cachedGirlSr = sr; return sr; }
            }
            return null;
        }

        public static Material GetOrCreateCharacterMaterial()
        {
            if (_drCharacterMaterial != null) return _drCharacterMaterial;

            Shader src = GetGirlSr()?.material?.shader;
            if (src == null)
            {
                foreach (var sr in UnityEngine.Resources.FindObjectsOfTypeAll<SpriteRenderer>())
                {
                    if (sr == null || sr.name != "Girl") continue;
                    if (sr.material == null) continue;
                    src = sr.material.shader;
                    break;
                }
            }
            if (src == null) return null;

            var mat = new Material(src);
            mat.name = MaterialName;
            ApplyColorSlotsToMaterial(mat);
            _drCharacterMaterial = mat;
            return mat;
        }

        public static Material BuildPortraitMaterial(ItemDataOverview overview)
        {
            if (overview == null) return null;

            var defaultItem = overview.FindUniqueId(overview.defaultSkinItem?.Id);
            if (defaultItem == null) return null;

            Material source = defaultItem.skinMaterial_Portrait;
            if (source == null || !source.HasProperty("_ColorChangeTarget"))
                source = defaultItem.skinMaterial;

            if (source == null || !source.HasProperty("_ColorChangeTarget"))
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning(
                    "[Portrait] no usable source. portrait=" 
                    + (defaultItem.skinMaterial_Portrait?.name ?? "null")
                    + " skin=" + (defaultItem.skinMaterial?.name ?? "null"));
                return null;
            }

            var mat = new Material(source);
            mat.name = MaterialName + "_Portrait";
            ApplyColorSlotsToMaterial(mat);
            return mat;
        }

        public static Material GetOrCreatePortraitMaterial(ItemDataOverview overview)
        {
            if (_drPortraitMaterial != null) return _drPortraitMaterial;

            if (Time.frameCount == _lastPortraitAttemptFrame) return null;
            _lastPortraitAttemptFrame = Time.frameCount;

            var built = BuildPortraitMaterial(overview);
            if (built != null) _drPortraitMaterial = built;
            return built;
        }

        public static void ForceApplyToRenderer(SpriteRenderer sr)
        {
            if (sr == null || sr.material == null) return;
            if (!IsActive) return;
            if (sr.material.name.StartsWith(MaterialName))
            {
                EnsureEnforcer(sr);
                return;
            }
            var mat = GetOrCreateCharacterMaterial();
            if (mat == null) return;
            sr.material = mat;
            EnsureEnforcer(sr);
        }

        public static void ClearCachedMaterials()
        {
            if (_drCharacterMaterial != null) Object.Destroy(_drCharacterMaterial);
            if (_drPortraitMaterial != null) Object.Destroy(_drPortraitMaterial);
            _drCharacterMaterial = null;
            _drPortraitMaterial = null;
            _lastPortraitAttemptFrame = -1;
        }

        private static ChangeRubySkin[] GetCachedChangeRubySkins()
        {
            if (Time.frameCount - _lastCrsScanFrame < CrsScanInterval && _cachedCrsList != null)
                return _cachedCrsList;

            _lastCrsScanFrame = Time.frameCount;
            _cachedCrsList = UnityEngine.Object.FindObjectsOfType<ChangeRubySkin>(true);
            return _cachedCrsList;
        }

        public static void InvalidateCrsCache()
        {
            _lastCrsScanFrame = -1;
            _cachedCrsList = null;
            InvalidatePlayerGirlsCache();
        }

        public static SpriteRenderer[] GetPlayerGirls()
        {
            var player = GamePlayCore.Instance?.player;
            if (player == null) return null;

            if (_cachedPlayerGirls != null && Time.frameCount - _playerGirlsFrame < PlayerGirlsCacheFrames)
                return _cachedPlayerGirls;

            _playerGirlsFrame = Time.frameCount;
            var all = player.GetComponentsInChildren<SpriteRenderer>(true);
            var list = new System.Collections.Generic.List<SpriteRenderer>(all.Length);
            foreach (var sr in all) if (sr.name == "Girl") list.Add(sr);
            _cachedPlayerGirls = list.ToArray();
            return _cachedPlayerGirls;
        }

        public static void InvalidatePlayerGirlsCache()
        {
            _playerGirlsFrame = -1;
            _cachedPlayerGirls = null;
            _cachedGirlSr = null;
        }

        public static void EnsureFlagConsistency()
        {
            if (!IsSkinSystemEnabled) return;

            try
            {
                if (MenuCore.Instance?.currentMenu is ChangeSkinMenu) return;
            }
            catch { }
            
            var state = RubiniteDarkRubyPlugin.Instance?.State;
            var overview = SingletonScriptableObject<ItemDataOverview>.Instance;
            if (state == null || overview?.currentSkinIDFlag == null) return;

            var currentId = overview.currentSkinIDFlag.GetValue();
            bool flagIsDR = currentId == DarkRubySkinEntry.SkinId;

            if (RubiniteDarkRubyPlugin.Instance?.Settings?.LogSkinDiagnostics?.Value == true)
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "[FlagCheck] flag='" + (currentId ?? "<null>")
                    + "' SkinSelected=" + state.SkinSelected
                    + " LastVanilla='" + (state.LastVanillaSkinId ?? "<null>")
                    + "' IsVanillaFrameFocused=" + IsVanillaFrameFocused);
            }

            if (!state.SkinSelected && string.IsNullOrEmpty(state.LastVanillaSkinId)
                && !string.IsNullOrEmpty(currentId) && !flagIsDR)
            {
                state.LastVanillaSkinId = currentId;
                state.Save();
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "DR: cached vanilla skin as '" + currentId + "'");
            }

            if (state.SkinSelected && !flagIsDR && !IsVanillaFrameFocused
                && SceneManager.GetActiveScene().name != "TitleMenu")
            {
                overview.currentSkinIDFlag.SetValue(DarkRubySkinEntry.SkinId);
            }

            if (!state.SkinSelected && flagIsDR)
            {
                var target = state.LastVanillaSkinId;

                if (string.IsNullOrEmpty(target) || target == DarkRubySkinEntry.SkinId
                    || overview.FindUniqueId(target) == null)
                {
                    var gameSkin = GameSetting.Instance?.rubySkinID?.GetValue();
                    if (!string.IsNullOrEmpty(gameSkin) && gameSkin != DarkRubySkinEntry.SkinId
                        && overview.FindUniqueId(gameSkin) != null)
                    {
                        target = gameSkin;
                    }
                }
                if (string.IsNullOrEmpty(target) || target == DarkRubySkinEntry.SkinId
                    || overview.FindUniqueId(target) == null)
                {
                    target = overview.defaultSkinItem?.Id;
                }

                if (!string.IsNullOrEmpty(target))
                {
                    overview.currentSkinIDFlag.SetValue(target);
                    state.LastVanillaSkinId = target;
                    state.Save();

                    if (GameSetting.Instance?.rubySkinID != null)
                    {
                        GameSetting.Instance.rubySkinID.SetValue(target, "RubiniteDarkRuby");
                    }
                    try { DataSaver.SaveFlag(DataSaver.path_saveFolder); } catch { }
                    
                    RubiniteDarkRubyPlugin.Log?.LogInfo(
                        "DR: stale flag reset to '" + target + "'");
                }
            }
        }

        public static void RestoreIntendedSkin()
        {
            var overview = SingletonScriptableObject<ItemDataOverview>.Instance;
            var state = RubiniteDarkRubyPlugin.Instance?.State;
            if (overview?.currentSkinIDFlag == null || state == null) return;

            if (state.SkinSelected)
            {
                overview.currentSkinIDFlag.SetValue(DarkRubySkinEntry.SkinId);
                return;
            }

            if (!string.IsNullOrEmpty(state.LastVanillaSkinId)
                && overview.FindUniqueId(state.LastVanillaSkinId) != null)
            {
                overview.currentSkinIDFlag.SetValue(state.LastVanillaSkinId);
                return;
            }

            overview.currentSkinIDFlag.SetValue(overview.defaultSkinItem?.Id ?? "");
        }
    }
}
