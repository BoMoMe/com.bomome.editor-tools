using System;
using System.IO;
using System.Reflection;
using Bomome.EditorTools.Build;
using NUnit.Framework;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Bomome.EditorTools.Tests
{
    public sealed class EditorToolSettingsTests
    {
        [Test]
        public void CaptureBindingUsesConfiguredKeyAndModifiers()
        {
            var settings = ScriptableObject.CreateInstance<ScreenshotCaptureSettings>();
            try
            {
                settings.Hotkey = KeyCode.F9;
                settings.Modifiers = ShortcutModifiers.Shift;
                Assert.AreEqual(new ShortcutBinding(new KeyCombination(KeyCode.F9, ShortcutModifiers.Shift)), CaptureShortcutSettings.Binding(settings));
                settings.HotkeyEnabled = false;
                Assert.AreEqual(ShortcutBinding.empty, CaptureShortcutSettings.Binding(settings));
                settings.HotkeyEnabled = true;
                settings.Hotkey = KeyCode.None;
                Assert.AreEqual(ShortcutBinding.empty, CaptureShortcutSettings.Binding(settings));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void ShortcutProfileIdentityIsProjectSpecific()
        {
            Assert.AreEqual(CaptureShortcutSettings.ProfileId("A"), CaptureShortcutSettings.ProfileId("A"));
            Assert.AreNotEqual(CaptureShortcutSettings.ProfileId("A"), CaptureShortcutSettings.ProfileId("B"));
        }

        [Test]
        public void RelativeOutputsResolveAgainstProjectRoot()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Assert.AreEqual(Path.Combine(root, "Temp", "Captures"), EditorToolsProjectSettings.ResolveOutputFolder("Temp/Captures", "unused"));
            Assert.AreEqual(Path.GetFullPath(Path.GetTempPath()), EditorToolsProjectSettings.ResolveOutputFolder(Path.GetTempPath(), "unused"));
        }

        [TestCase("Assets/Configs")]
        [TestCase("Assets/Editor/../Other")]
        [TestCase("Packages/Editor/Settings")]
        public void DefaultsRejectRuntimeAndOutsideAssetFolders(string folder)
        {
            Assert.Throws<ArgumentException>(() => EditorToolsProjectSettings.CreateDefaults(folder));
        }

        [Test]
        public void BuildWindowAndQuickPolicyReadAssignedAsset()
        {
            var registry = EditorToolsProjectSettings.instance;
            var previous = registry.AndroidBuild;
            var settings = ScriptableObject.CreateInstance<AndroidBuildToolSettings>();
            AndroidDevBuildWindow window = null;
            try
            {
                settings.OutputFolder = "Temp/ConfiguredBuilds";
                settings.Environment = BuildEnvironment.Stage;
                settings.VersionIncrement = ToolVersionIncrement.None;
                settings.AutoRunPlayer = true;
                settings.AppendEnvironmentSuffix = false;
                registry.AndroidBuild = settings;
                window = ScriptableObject.CreateInstance<AndroidDevBuildWindow>();
                const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
                Assert.AreEqual(settings.OutputFolder, typeof(AndroidDevBuildWindow).GetField("outputFolder", instance).GetValue(window));
                Assert.AreEqual(false, typeof(AndroidDevBuildWindow).GetField("appendEnvironmentSuffix", instance).GetValue(window));
                var optionsType = typeof(AndroidDevBuildWindow).GetNestedType("AndroidBuildOptions", BindingFlags.NonPublic);
                var options = optionsType.GetProperty("Default").GetValue(null);
                options = typeof(AndroidDevBuildWindow).GetMethod("ApplyConfiguredSettings", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new[] { options });
                Assert.AreEqual(settings.OutputFolder, optionsType.GetField("OutputFolder").GetValue(options));
                Assert.AreEqual(BuildEnvironment.Stage, optionsType.GetField("Environment").GetValue(options));
                Assert.AreEqual(true, optionsType.GetField("AutoRunPlayer").GetValue(options));
                Assert.AreEqual(false, optionsType.GetField("AppendEnvironmentSuffix").GetValue(options));
                Assert.AreEqual("None", optionsType.GetField("VersionIncrementMode").GetValue(options).ToString());
            }
            finally
            {
                registry.AndroidBuild = previous;
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void FirebaseUsesAssignedDestinationAsset()
        {
            var registry = EditorToolsProjectSettings.instance;
            var previous = registry.Firebase;
            var settings = ScriptableObject.CreateInstance<FirebaseDistributionToolSettings>();
            try
            {
                settings.Development.appId = "1:123:android:test";
                registry.Firebase = settings;
                Assert.AreSame(settings.Development, FirebaseDistributionSettings.instance.GetDestination(0));
                Assert.AreSame(settings.Stage, FirebaseDistributionSettings.instance.GetDestination(1));
                Assert.AreSame(settings.Production, FirebaseDistributionSettings.instance.GetDestination(2));
            }
            finally
            {
                registry.Firebase = previous;
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
