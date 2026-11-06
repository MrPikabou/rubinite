using UnityEngine;

namespace RubiniteDarkRuby
{
    /// <summary>
    /// Applies the custom Dark Ruby character skin (hardcoded material swap).
    /// Selection is persisted in DarkRubyState.SkinSelected. The skin only takes
    /// effect when explicitly selected — default is vanilla.
    /// </summary>
    internal static class DarkRubySkin
    {
        public const string MaterialName = "HardcodedDarkRubyMaterial";

        // Color-change slots derived from the in-game Dark Ruby character material.
        // Each tuple: target RGBA, replacement RGBA, tolerance.
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

        /// <summary>
        /// Applies the Dark Ruby skin to every "Girl" renderer on the player.
        /// Idempotent — renderers already using our material are skipped.
        /// </summary>
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

        /// <summary>Apply the Dark Ruby material to a single renderer. Skips if already applied.</summary>
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
                "Dark Ruby skin material applied to '" + sr.name
                + "' (scene: " + sr.gameObject.scene.name + ")");
        }

        /// <summary>
        /// Forces the game to rebuild the character skin from GameSetting.rubySkinID,
        /// which effectively reverts to whatever vanilla color was last selected.
        /// </summary>
        public static void RevertToVanilla()
        {
            var skins = Object.FindObjectsOfType<ChangeRubySkin>();
            foreach (var skin in skins)
            {
                if (skin != null) skin.ChangeSkin();
            }
            RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby skin reverted to vanilla skin.");
        }

        /// <summary>
        /// Flips the persisted selection and applies/reverts accordingly.
        /// Called from F9 (debug) and, in Phase 3, from the inventory skin menu.
        /// </summary>
        public static void Toggle()
        {
            var state = RubiniteDarkRubyPlugin.Instance?.State;
            if (state == null) return;

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
