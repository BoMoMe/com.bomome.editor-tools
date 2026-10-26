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
        [Tooltip("Append _dev or _stage for environment-specific app IDs. Disable to keep the exact Android application ID from Player Settings. Does not change Development Build options.")]
        public bool AppendEnvironmentSuffix = true;
        public ToolVersionIncrement VersionIncrement = ToolVersionIncrement.Patch;
        public ToolIl2CppConfiguration Il2CppConfiguration = ToolIl2CppConfiguration.Debug;
        public bool DevelopmentBuild = true;
        public bool ConnectProfiler;
        public bool AutoRunPlayer;
        public bool FirebaseDistribution;
    }
}
