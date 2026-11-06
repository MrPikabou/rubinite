using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;
using UnityEngine;

namespace RubiniteDarkRuby
{
    internal static class DarkRubySkin
    {
        public const string MaterialName = "HardcodedDarkRubyMaterial";

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

        private const string FallbackVanillaSkinId = "eb9dd477-a430-48f8-8a7d-f721ea740a08";

        public static void Apply()
        {
            var player = GamePlayCore.Instance?.player;
            if (player == null) return;

            foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.name != "Girl") continue;
                if (sr.material == null) continue;
                if (sr.material.name.StartsWith(MaterialName)) continue;
                ApplyToRenderer(sr);
            }
        }

        public static void ApplyToRenderer(SpriteRenderer sr)
        {
            if (sr == null || sr.material == null) return;
            if (sr.material.name.StartsWith(MaterialName)) return;

            var mat = new Material(sr.material);
            mat.name = MaterialName;

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

            sr.material = mat;
            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "Dark Ruby skin applied to '" + sr.name
                + "' (renderer scene: " + sr.gameObject.scene.name + ")");
        }

        /// <summary>
        /// Restores the vanilla skin. Sets GameSetting.rubySkinID first so the
        /// game's ChangeSkin knows which color to rebuild.
        /// </summary>
        public static void RevertToVanilla()
        {
            var state = RubiniteDarkRubyPlugin.Instance?.State;
            var targetId = state?.LastVanillaSkinId;
            if (string.IsNullOrEmpty(targetId)) targetId = FallbackVanillaSkinId;

            if (GameSetting.Instance?.rubySkinID != null)
                GameSetting.Instance.rubySkinID.SetValue(targetId, "RubiniteDarkRuby");
            var itemOverview = SingletonScriptableObject<ItemDataOverview>.Instance;
            if (itemOverview?.currentSkinIDFlag != null)
                itemOverview.currentSkinIDFlag.SetValue(targetId, "RubiniteDarkRuby");

            foreach (var skin in UnityEngine.Resources.FindObjectsOfTypeAll<ChangeRubySkin>())
            {
                if (skin != null) skin.ChangeSkin();
            }

            var player = GamePlayCore.Instance?.player;
            if (player != null)
            {
                foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.name != "Girl") continue;
                    if (sr.material == null) continue;
                    if (!sr.material.name.StartsWith(MaterialName)) continue;

                    var vanilla = sr.sharedMaterial;
                    if (vanilla != null && !vanilla.name.StartsWith(MaterialName))
                    {
                        sr.material = vanilla;
                        RubiniteDarkRubyPlugin.Log?.LogInfo(
                            "Dark Ruby skin: reset material on renderer to '" + vanilla.name + "'");
                    }
                    else
                    {
                        RubiniteDarkRubyPlugin.Log?.LogWarning(
                            "Dark Ruby skin: cannot reset material — sharedMaterial is null or Dark Ruby. "
                            + "Vanilla material may need capture at apply time.");
                    }
                }
            }

            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "Dark Ruby skin reverted to vanilla (" + targetId + ")");
        }

        public static void Toggle()
        {
            var state = RubiniteDarkRubyPlugin.Instance?.State;
            if (state == null) return;

            if (!state.SkinSelected)
            {
                var current = GameSetting.Instance?.rubySkinID?.GetValue();
                if (!string.IsNullOrEmpty(current))
                {
                    state.LastVanillaSkinId = current;
                    RubiniteDarkRubyPlugin.Log?.LogInfo(
                        "Dark Ruby skin: captured vanilla color as '" + current + "'");
                }
            }

            state.SkinSelected = !state.SkinSelected;
            state.Save();

            if (state.SkinSelected)
            {
                Apply();
                RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby skin: ENABLED");
            }
            else
            {
                RevertToVanilla();
                RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby skin: DISABLED");
            }
        }
    }
}
