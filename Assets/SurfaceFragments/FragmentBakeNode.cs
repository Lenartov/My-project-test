using UnityEngine;

namespace SurfaceFragments
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class FragmentBakeNode : MonoBehaviour
    {
        [SerializeField, HideInInspector] private string sourceKey;
        public string SourceKey => sourceKey;
        public void SetSourceKey(string key) => sourceKey = key;
    }
}
