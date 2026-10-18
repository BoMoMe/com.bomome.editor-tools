using Bomome.EditorTools.Build;
using UnityEngine;

namespace Bomome.EditorTools
{
    public enum ToolVersionIncrement { Patch, Minor, Major, None }
    public enum ToolIl2CppConfiguration { Auto, Debug, Release }

    [CreateAssetMenu(menuName = "Bomome/Editor Tools/Android Build Settings")]
    public sealed class AndroidBuildToolSettings : ScriptableObject
    {
        [Tooltip("Project-relative or absolute output folder.")]
        public string OutputFolder = "Builds/Android";
        public BuildEnvironment Environment = BuildEnvironment.Development;
        public ToolVersionIncrement VersionIncrement = ToolVersionIncrement.Patch;
        public ToolIl2CppConfiguration Il2CppConfiguration = ToolIl2CppConfiguration.Debug;
        public bool DevelopmentBuild = true;
        public bool ConnectProfiler;
        public bool AutoRunPlayer;
        public bool FirebaseDistribution;
    }
}
