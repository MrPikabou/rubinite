using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;
using UnityEngine;
using UnityEngine.UI;

namespace RubiniteDarkRuby
{
    internal static class DarkRubySkinEntry
    {
        public const string FrameName = "DR_SkinFrame";
        public const string SkinId = "darkruby";

        private static GameObject _injectedFrame;
        private static ItemData _fakeData;
        private static Sprite _cachedItemsIcon; 

        public static ItemData FakeItemData => _fakeData;

        public static bool ShouldShow =>
            RubiniteDarkRubyPlugin.Instance?.Settings?.UseCustomSkin?.Value == true;

        public static GameObject EnsureInjected(ChangeSkinMenu menu)
        {
            if (menu == null) return null;
            if (!ShouldShow) return null;

            var skinGroup = menu.transform.Find("Skin_Group");
            if (skinGroup == null) return null;

            var existing = skinGroup.Find(FrameName);
            if (existing != null)
            {
                _injectedFrame = existing.gameObject;
                SyncSelection(menu);
                return _injectedFrame;
            }

            if (skinGroup.childCount == 0) return null;

            if (_fakeData == null)
                _fakeData = CreateDarkRubyItemData();
            if (_fakeData == null)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning(
                    "DR skin frame: could not create fake ItemData");
                return null;
            }

            var template = skinGroup.GetChild(0).gameObject;
            bool templateWasActive = template.activeSelf;
            template.SetActive(false);

            var clone = Object.Instantiate(template, skinGroup);
            template.SetActive(templateWasActive);

            clone.name = FrameName;
            clone.SetActive(false);

            var skinFrame = clone.GetComponent<SkinFrame>();
            if (skinFrame != null)
            {
                skinFrame.changeSkinItemData = _fakeData;
                skinFrame.forceEnable = true;
            }

            var iconImage = clone.transform.Find("Icon_Mask/Icon_frame")?.GetComponent<Image>();
            if (iconImage != null)
            {
                var customIcon = Resources.DarkRubyResources.LoadSprite("dark_ruby_pigment_icon.png", 1f);
                if (customIcon != null)
                {
                    iconImage.sprite = customIcon;
                    iconImage.color = Color.white;
                }
                else
                {
                    var itemsIcon = FindPreferredItemsIcon();
                    if (itemsIcon != null) iconImage.sprite = itemsIcon;

                    var portraitMat = DarkRubySkin.BuildPortraitMaterial(
                        SingletonScriptableObject<ItemDataOverview>.Instance);
                    if (portraitMat != null)
                    {
                        var iconMat = new Material(portraitMat);
                        iconMat.name = portraitMat.name + "_Icon";
                        iconMat.SetFloat("_HsvBright", 1.2f);
                        iconImage.material = iconMat;
                    }
                }
                iconImage.preserveAspect = true;
            }

            var selectImg = clone.transform.Find("Select")?.gameObject;
            if (selectImg != null) selectImg.SetActive(false);
            var newPointImg = clone.transform.Find("NewPoint")?.gameObject;
            if (newPointImg != null) newPointImg.SetActive(false);

            clone.SetActive(true);

            _injectedFrame = clone;
            SyncSelection(menu);
            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "DR skin frame injected (OnSelect/OnClick will be intercepted)");
            return clone;
        }

        private static Sprite FindPreferredItemsIcon()
        {
            if (_cachedItemsIcon != null) return _cachedItemsIcon;
            var candidates = new[] 
            { 
                "ItemsIcon_21",
                "ItemsIcon_24",
                "ItemsIcon_20"
            };
            
            foreach (var name in candidates)
            {
                foreach (var s in UnityEngine.Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s != null && s.name == name) { _cachedItemsIcon = s; return s; }
                }
            }
            return null;
        }

        public static void SyncSelection(ChangeSkinMenu menu)
        {
            if (_injectedFrame == null || menu == null) return;

            var skinGroup = menu.transform.Find("Skin_Group");
            if (skinGroup == null) return;

            bool drActive = DarkRubySkin.IsActive;

            for (int i = 0; i < skinGroup.childCount; i++)
            {
                var child = skinGroup.GetChild(i);
                if (child.name == FrameName) continue;
                var disp = child.Find("displayWhenSelected")?.gameObject;
                if (disp != null) disp.SetActive(false);
            }

            var ourDisp = _injectedFrame.transform.Find("displayWhenSelected")?.gameObject;
            if (ourDisp != null) ourDisp.SetActive(drActive);

            if (drActive && menu.selectedTitleText != null)
                menu.selectedTitleText.text = "Shadow Pigments";
        }

        private static ItemData CreateDarkRubyItemData()
        {
            var overview = SingletonScriptableObject<ItemDataOverview>.Instance;
            if (overview == null) return null;

            ItemData template = null;
            foreach (var item in overview.GetAllData())
            {
                if (item != null) { template = item; break; }
            }
            if (template == null) return null;

            var clone = Object.Instantiate(template);
            clone.name = "DR_FakeItemData";
            return clone;
        }

        private static Sprite RenderSpriteWithDarkRubyMaterial(Sprite source, int resolution)
        {
            if (source == null) return null;
            const int PreviewLayer = 31;

            var renderGo = new GameObject("DR_PigmentRenderRoot");
            renderGo.hideFlags = HideFlags.HideAndDontSave;
            renderGo.layer = PreviewLayer;

            var spriteGo = new GameObject("DR_PigmentSprite");
            spriteGo.hideFlags = HideFlags.HideAndDontSave;
            spriteGo.layer = PreviewLayer;
            spriteGo.transform.SetParent(renderGo.transform, false);

            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = source;
            var playerShader = DarkRubySkin.GetPlayerShader();
            sr.material = DarkRubySkin.CreateFreshMaterialWithShader(playerShader, source != null ? source.texture : null);

            var camGo = new GameObject("DR_PigmentRenderCam");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(source.bounds.extents.y, source.bounds.extents.x);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.cullingMask = 1 << PreviewLayer;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            cam.targetTexture = null;
            Object.Destroy(rt);
            Object.Destroy(camGo);
            Object.Destroy(renderGo);

            float ppu = resolution / (source.bounds.size.x > 0 ? source.bounds.size.x : 1f);
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f), ppu);
        }
    }
}
