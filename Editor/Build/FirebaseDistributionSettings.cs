using System;
using UnityEditor;
using UnityEngine;

namespace Bomome.EditorTools.Build
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Bomome.EditorTools.Build", sourceAssembly: "Assembly-CSharp-Editor")]
    [FilePath("ProjectSettings/FirebaseDistributionSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class FirebaseDistributionSettings : ScriptableSingleton<FirebaseDistributionSettings>
    {
        [Serializable]
        public sealed class Destination
        {
            public string appId = "";
            public string groups = "";
        }

        [SerializeField] private Destination development = new Destination();
        [SerializeField] private Destination stage = new Destination();
        [SerializeField] private Destination production = new Destination();

        public Destination GetDestination(int environment)
        {
            var config = EditorToolsProjectSettings.instance.Firebase;
            if (config != null) return config.GetDestination(environment);
            switch (environment)
            {
                case 0: return development;
                case 1: return stage;
                case 2: return production;
                default: throw new ArgumentOutOfRangeException(nameof(environment));
            }
        }

        public void SaveSettings()
        {
            var config = EditorToolsProjectSettings.instance.Firebase;
            if (config == null) Save(true);
            else
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
            }
        }
    }
}
