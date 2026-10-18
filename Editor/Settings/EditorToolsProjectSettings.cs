using System;
using System.IO;
using Bomome.EditorTools.Build;
using UnityEditor;
using UnityEngine;

namespace Bomome.EditorTools
{
    [FilePath("ProjectSettings/BomomeEditorTools.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class EditorToolsProjectSettings : ScriptableSingleton<EditorToolsProjectSettings>
    {
        public ScreenshotCaptureSettings Capture;
        public AndroidBuildToolSettings AndroidBuild;
        public AssetReferenceExplorerSettings References;
        public FirebaseDistributionToolSettings Firebase;

        public void SaveSettings()
        {
            Save(true);
            CaptureShortcutSettings.Apply();
        }

        public static string ResolveOutputFolder(string value, string fallback)
        {
            string folder = string.IsNullOrWhiteSpace(value) ? fallback : value;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(root, folder));
        }

        public static void CreateDefaults(string folder = "Assets/Editor/BomomeTools")
        {
            if (string.IsNullOrWhiteSpace(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal) ||
                folder.Contains("..") || !("/" + folder + "/").Contains("/Editor/"))
                throw new ArgumentException("Choose a project folder under Assets containing an Editor folder.");
            string current = "Assets";
            foreach (string part in folder.Substring(7).Split('/'))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
            var registry = instance;
            bool newCapture = registry.Capture == null && AssetDatabase.LoadAssetAtPath<ScreenshotCaptureSettings>(folder + "/ScreenshotCaptureSettings.asset") == null;
            bool newBuild = registry.AndroidBuild == null && AssetDatabase.LoadAssetAtPath<AndroidBuildToolSettings>(folder + "/AndroidBuildToolSettings.asset") == null;
            bool newFirebase = registry.Firebase == null && AssetDatabase.LoadAssetAtPath<FirebaseDistributionToolSettings>(folder + "/FirebaseDistributionToolSettings.asset") == null;
            // Capture legacy destination values before assigning the asset-backed provider.
            var destinations = new FirebaseDistributionSettings.Destination[3];
            if (newFirebase)
                for (int i = 0; i < 3; i++)
                {
                    var old = FirebaseDistributionSettings.instance.GetDestination(i);
                    destinations[i] = new FirebaseDistributionSettings.Destination { appId = old.appId, groups = old.groups };
                }
            registry.Capture = registry.Capture != null ? registry.Capture : LoadOrCreate<ScreenshotCaptureSettings>(folder);
            registry.AndroidBuild = registry.AndroidBuild != null ? registry.AndroidBuild : LoadOrCreate<AndroidBuildToolSettings>(folder);
            registry.References = registry.References != null ? registry.References : LoadOrCreate<AssetReferenceExplorerSettings>(folder);
            registry.Firebase = registry.Firebase != null ? registry.Firebase : LoadOrCreate<FirebaseDistributionToolSettings>(folder);
            if (newCapture)
            {
                var capture = registry.Capture;
                capture.OutputFolder = EditorPrefs.GetString(EditorToolPreferences.Key("Bomome.GameScreenshot.OutputFolder"), capture.OutputFolder);
                capture.Format = (ScreenshotFileFormat)EditorPrefs.GetInt(EditorToolPreferences.Key("Bomome.GameScreenshot.Format"), (int)capture.Format);
                capture.Scale = EditorPrefs.GetInt(EditorToolPreferences.Key("Bomome.GameScreenshot.Scale"), capture.Scale);
                capture.JpegQuality = EditorPrefs.GetInt(EditorToolPreferences.Key("Bomome.GameScreenshot.JpegQuality"), capture.JpegQuality);
                capture.FileNameFormat = EditorPrefs.GetString(EditorToolPreferences.Key("Bomome.GameScreenshot.FileNameFormat"), capture.FileNameFormat);
                EditorUtility.SetDirty(capture);
            }
            if (newBuild)
            {
                var build = registry.AndroidBuild;
                build.OutputFolder = EditorPrefs.GetString(EditorToolPreferences.Key("Bomome.AndroidBuild.OutputFolder"), build.OutputFolder);
                build.Environment = (Build.BuildEnvironment)EditorPrefs.GetInt(EditorToolPreferences.Key("Bomome.AndroidBuild.Environment"), (int)build.Environment);
                build.Il2CppConfiguration = (ToolIl2CppConfiguration)EditorPrefs.GetInt(EditorToolPreferences.Key("Bomome.AndroidBuild.Il2CppConfiguration"), (int)build.Il2CppConfiguration);
                build.FirebaseDistribution = EditorPrefs.GetBool(EditorToolPreferences.Key("Bomome.AndroidBuild.FirebaseEnabled"), false);
                EditorUtility.SetDirty(build);
            }
            if (newFirebase)
            {
                registry.Firebase.Development = destinations[0];
                registry.Firebase.Stage = destinations[1];
                registry.Firebase.Production = destinations[2];
                EditorUtility.SetDirty(registry.Firebase);
            }
            registry.SaveSettings();
            AssetDatabase.SaveAssets();
        }

        private static T LoadOrCreate<T>(string folder) where T : ScriptableObject
        {
            string path = folder + "/" + typeof(T).Name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Another asset already occupies " + path);
            var asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
