using UnityEngine;

namespace Bomome.EditorTools
{
    public static class EditorToolPreferences
    {
        public static string Key(string featureKey) => featureKey + "." + Application.dataPath;
    }
}
