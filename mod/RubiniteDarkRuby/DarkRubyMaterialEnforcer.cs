using UnityEngine;

namespace RubiniteDarkRuby
{
    [DefaultExecutionOrder(10000)]
    internal sealed class DarkRubyMaterialEnforcer : MonoBehaviour
    {
        private SpriteRenderer _sr;

        private void Awake() { _sr = GetComponent<SpriteRenderer>(); }
        private void OnEnable() { _sr = GetComponent<SpriteRenderer>(); }

        private void LateUpdate() { Enforce(); }
        private void OnWillRenderObject() { Enforce(); }

        private void Enforce()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) return;
            if (!DarkRubySkin.ShouldApplyToRenderer(_sr)) return;
            if (_sr.material == null) return;
            if (_sr.material.name.StartsWith(DarkRubySkin.MaterialName)) return;

            DarkRubySkin.ApplyToRenderer(_sr);
        }
    }
}
