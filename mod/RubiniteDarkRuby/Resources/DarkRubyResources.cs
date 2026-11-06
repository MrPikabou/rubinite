using System.IO;
using System.Reflection;
using UnityEngine;

namespace RubiniteDarkRuby.Resources
{
    /// <summary>
    /// Loads PNG sprites for the Dark Ruby mod.
    /// Search order: BepInEx/plugins/RubiniteDarkRuby/{file} → embedded resource.
    /// </summary>
    internal static class DarkRubyResources
    {
        public static byte[] TryReadEmbedded(string fileName)
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = "RubiniteDarkRuby.Resources." + fileName;
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        public static Sprite LoadSprite(string fileName, float targetHeight = 0f)
        {
            byte[] data = null;

            // External override path: <PluginPath>/RubiniteDarkRuby/<file>
            try
            {
                var extFolder = Path.Combine(BepInEx.Paths.PluginPath, "RubiniteDarkRuby");
                var extPath = Path.Combine(extFolder, fileName);
                if (File.Exists(extPath)) data = File.ReadAllBytes(extPath);
            }
            catch { /* fall through to embedded */ }

            if (data == null) data = TryReadEmbedded(fileName);
            if (data == null)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning(
                    "Sprite '" + fileName + "' not found (external or embedded).");
                return null;
            }

            try
            {
                var tex = new Texture2D(2, 2);
                ImageConversion.LoadImage(tex, data);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                float ppu = 100f;
                if (targetHeight > 0f) ppu = tex.height / targetHeight;

                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), ppu);
            }
            catch (System.Exception ex)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning(
                    "Failed to load sprite '" + fileName + "': " + ex.Message);
                return null;
            }
        }
    }
}
