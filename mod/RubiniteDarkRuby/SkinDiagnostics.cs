using System.Collections.Generic;
using UnityEngine;

namespace RubiniteDarkRuby
{
    internal static class SkinDiagnostics
    {
        private struct RendererState
        {
            public string MatName;
            public int MatId;
            public string SharedName;
            public int SharedId;
            public string Scene;
        }

        private static readonly Dictionary<int, RendererState> _lastState = new();
        private static int _lastFrame = -1;

        public static void Poll(int frame)
        {
            if (frame == _lastFrame) return;
            _lastFrame = frame;

            var player = GamePlayCore.Instance?.player;
            if (player == null) return;

            foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.name != "Girl") continue;

                int id = sr.GetInstanceID();
                var current = new RendererState
                {
                    MatName = sr.material != null ? sr.material.name : "<null>",
                    MatId = sr.material != null ? sr.material.GetInstanceID() : 0,
                    SharedName = sr.sharedMaterial != null ? sr.sharedMaterial.name : "<null>",
                    SharedId = sr.sharedMaterial != null ? sr.sharedMaterial.GetInstanceID() : 0,
                    Scene = sr.gameObject.scene.name,
                };

                if (_lastState.TryGetValue(id, out var last))
                {
                    if (last.MatId != current.MatId || last.SharedId != current.SharedId)
                    {
                        RubiniteDarkRubyPlugin.Log?.LogInfo(
                            "[SkinDiag f" + frame + "] Girl#" + id + " (" + current.Scene + ") " +
                            "mat: '" + last.MatName + "'(" + last.MatId + ") -> '" + current.MatName + "'(" + current.MatId + ") " +
                            "shared: '" + last.SharedName + "'(" + last.SharedId + ") -> '" + current.SharedName + "'(" + current.SharedId + ")");
                    }
                }
                else
                {
                    RubiniteDarkRubyPlugin.Log?.LogInfo(
                        "[SkinDiag f" + frame + "] Girl#" + id + " (" + current.Scene + ") initial " +
                        "mat='" + current.MatName + "'(" + current.MatId + ") shared='" + current.SharedName + "'(" + current.SharedId + ")");
                }

                _lastState[id] = current;
            }
        }
    }
}
