using UnityEngine;

namespace Bomome.EditorTools
{
    [CreateAssetMenu(menuName = "Bomome/Editor Tools/Asset Reference Explorer Settings")]
    public sealed class AssetReferenceExplorerSettings : ScriptableObject
    {
        [Tooltip("Work budget per editor tick. A single dependency import can exceed it.")]
        [Range(1, 50)] public int ScanMillisecondsPerTick = 8;
        public bool IncludePackages = true;
    }
}
