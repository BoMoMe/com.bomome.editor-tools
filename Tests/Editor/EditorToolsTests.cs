using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using Bomome.EditorTools.Build;
using Bomome.EditorTools.Capture;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Bomome.EditorTools.Tests
{
    public sealed class EditorToolsTests
    {
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("1.2.3", "Patch", "1.2.4")]
        [TestCase("1.2.3", "Minor", "1.3.0")]
        [TestCase("1.2.3", "Major", "2.0.0")]
        [TestCase("1.2.3", "None", "1.2.3")]
        [TestCase("", "None", "0.1.0")]
        public void VersionIncrementIsStable(string version, string increment, string expected)
        {
            Type type = typeof(AndroidDevBuildWindow).GetNestedType("VersionIncrement", BindingFlags.NonPublic);
            Assert.AreEqual(expected, CallBuild("IncrementVersion", version, Enum.Parse(type, increment)));
        }

        [TestCase("com.example.game", BuildEnvironment.Development, "com.example.game_dev")]
        [TestCase("com.example.game_dev", BuildEnvironment.Stage, "com.example.game_stage")]
        [TestCase("com.example.game_stage", BuildEnvironment.Production, "com.example.game")]
        [TestCase("com.example.game", BuildEnvironment.Production, "com.example.game")]
        public void EnvironmentIdentifiersDoNotStackSuffixes(string id, BuildEnvironment environment, string expected)
        {
            Assert.AreEqual(expected, CallBuild("CreateEnvironmentIdentifier", id, environment, true));
        }

        [TestCase("com.example.game", BuildEnvironment.Development)]
        [TestCase("com.example.game", BuildEnvironment.Stage)]
        [TestCase("com.example.game_dev", BuildEnvironment.Production)]
        [TestCase("com.example.custom", BuildEnvironment.Production)]
        public void DisabledSuffixPreservesExactPlayerSettingsId(string id, BuildEnvironment environment)
        {
            Assert.AreEqual(id, CallBuild("CreateEnvironmentIdentifier", id, environment, false));
        }

        [Test]
        public void DisablingSuffixDoesNotDisableDevelopmentFlags()
        {
            object options = Options();
            Set(options, "Environment", BuildEnvironment.Development);
            Set(options, "DevelopmentBuild", true);
            Set(options, "ConnectProfiler", true);
            Set(options, "AppendEnvironmentSuffix", false);
            Assert.AreEqual(BuildOptions.Development | BuildOptions.ConnectWithProfiler,
                CallBuild("CreateBuildOptions", options));
        }

        [TestCase(BuildEnvironment.Production, true, true, BuildOptions.None)]
        [TestCase(BuildEnvironment.Development, true, true, BuildOptions.Development | BuildOptions.ConnectWithProfiler)]
        [TestCase(BuildEnvironment.Stage, false, true, BuildOptions.None)]
        public void ProductionNeverGetsDebugFlags(BuildEnvironment environment, bool development, bool profiler, BuildOptions expected)
        {
            object options = Options();
            Set(options, "Environment", environment);
            Set(options, "DevelopmentBuild", development);
            Set(options, "ConnectProfiler", profiler);
            Assert.AreEqual(expected, CallBuild("CreateBuildOptions", options));
        }

        [Test]
        public void StoreOptionsForceReleaseAndDisableAutoRun()
        {
            object options = Options();
            Set(options, "AutoRunPlayer", true);
            Set(options, "ConnectProfiler", true);
            object store = CallBuild("CreateStoreOptions", options);
            Assert.AreEqual(BuildEnvironment.Production, Get(store, "Environment"));
            Assert.AreEqual(false, Get(store, "DevelopmentBuild"));
            Assert.AreEqual(false, Get(store, "ConnectProfiler"));
            Assert.AreEqual(false, Get(store, "AutoRunPlayer"));
            Assert.AreEqual(true, Get(store, "StoreBundle"));
            Assert.AreEqual("Release", Get(store, "Il2CppConfiguration").ToString());
            Assert.AreEqual(BuildOptions.None, CallBuild("CreateBuildOptions", store));
        }

        [Test]
        public void ReplacingIl2CppConfigurationPreservesOtherArguments()
        {
            Assert.AreEqual("--foo=bar", CallBuild("RemoveIl2CppConfigurationArg", "--foo=bar --configuration=Debug"));
        }

        [Test]
        public void ProjectPreferenceKeyIncludesProjectAndFeature()
        {
            StringAssert.Contains(Application.dataPath, EditorToolPreferences.Key("capture"));
            Assert.AreNotEqual(EditorToolPreferences.Key("capture"), EditorToolPreferences.Key("build"));
        }

        [Test]
        public void ScreenshotDefaultUsesProductAndTimestamp()
        {
            var method = typeof(GameScreenshotCaptureWindow).GetMethod("FormatFileName", StaticPrivate);
            string name = (string)method.Invoke(null, new object[] { new DateTime(2026, 10, 19, 1, 2, 3), null });
            StringAssert.StartsWith(PlayerSettings.productName + "_", name);
            StringAssert.EndsWith("_20261019_010203", name);
        }

        [UnityTest]
        public IEnumerator ScreenshotJpgConversionWritesImageAndRemovesTemporaryPng()
        {
            string folder = Path.Combine(Path.GetTempPath(), "BomomeCaptureTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string input = Path.Combine(folder, "frame.capture_tmp.png");
            string output = Path.Combine(folder, "frame.jpg");
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixel(0, 0, Color.green);
                texture.Apply();
                File.WriteAllBytes(input, texture.EncodeToPNG());
                var conversion = typeof(GameScreenshotCaptureWindow).GetNestedType("PendingJpgConversion", BindingFlags.NonPublic);
                conversion.GetMethod("WaitForFile").Invoke(null, new object[] { input, output, 90 });
                double deadline = EditorApplication.timeSinceStartup + 8;
                while (!File.Exists(output) && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsTrue(File.Exists(output), "JPG conversion did not complete.");
                Assert.IsFalse(File.Exists(input), "Temporary PNG was not removed.");
                byte[] bytes = File.ReadAllBytes(output);
                Assert.AreEqual(0xff, bytes[0]);
                Assert.AreEqual(0xd8, bytes[1]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                if (File.Exists(input)) File.Delete(input);
                if (File.Exists(output)) File.Delete(output);
                Directory.Delete(folder);
            }
        }

        [TestCase("1:123:android:abc123", "qa,design", null)]
        [TestCase("com.example.game", "qa", "App ID")]
        [TestCase("1:123:android:abc123", "qa;whoami", "group")]
        [TestCase("1:123:android:abc123", "", "group")]
        public void FirebaseValidationRejectsInvalidDestinations(string app, string groups, string errorFragment)
        {
            string error = FirebaseDistributionUploader.Validate(app, groups);
            if (errorFragment == null) Assert.IsNull(error);
            else StringAssert.Contains(errorFragment, error);
        }

        [TestCase("firebase.cmd")]
        [TestCase("firebase.bat")]
        [TestCase("firebase.ps1")]
        public void FirebaseRejectsShellWrappers(string cli)
        {
            Assert.Throws<ArgumentException>(() => FirebaseDistributionUploader.CreateCliStartInfo(cli, "--version"));
        }

        [Test]
        public void FirebaseJsUsesNodeWithoutShell()
        {
            var info = FirebaseDistributionUploader.CreateCliStartInfo("C:/Tools/firebase.js", "--version");
            Assert.AreEqual("node", info.FileName);
            Assert.IsFalse(info.UseShellExecute);
            StringAssert.Contains("\"C:/Tools/firebase.js\"", info.Arguments);
            Assert.IsTrue(info.CreateNoWindow);
        }

        [Test]
        public void FirebaseArgumentsAreQuoted()
        {
            Assert.AreEqual("\"a b\"", FirebaseDistributionUploader.Quote("a b"));
            Assert.AreEqual("\"a\\\"b\"", FirebaseDistributionUploader.Quote("a\"b"));
            Assert.Throws<ArgumentException>(() => FirebaseDistributionUploader.Quote("bad\0arg"));
        }

        [Test]
        public void BuildAdaptersRestoreInReverseOrderAndOnlyOnce()
        {
            var order = new List<string>();
            using (AndroidBuildHooks.Register("Tests.A", env => new Cleanup(() => order.Add("A"))))
            using (AndroidBuildHooks.Register("Tests.B", env => new Cleanup(() => order.Add("B"))))
            {
                IDisposable scope = AndroidBuildHooks.Begin(BuildEnvironment.Stage);
                scope.Dispose();
                scope.Dispose();
                CollectionAssert.AreEqual(new[] { "B", "A" }, order);
            }
        }

        [Test]
        public void FailedPreparationRestoresAlreadyPreparedAdapters()
        {
            bool restored = false;
            using (AndroidBuildHooks.Register("Tests.A", env => new Cleanup(() => restored = true)))
            using (AndroidBuildHooks.Register("Tests.B", env => throw new InvalidOperationException("prepare")))
            {
                Assert.Throws<InvalidOperationException>(() => AndroidBuildHooks.Begin(BuildEnvironment.Development));
                Assert.IsTrue(restored);
            }
        }

        [Test]
        public void CleanupContinuesAfterAnAdapterThrows()
        {
            bool restored = false;
            using (AndroidBuildHooks.Register("Tests.A", env => new Cleanup(() => restored = true)))
            using (AndroidBuildHooks.Register("Tests.B", env => new Cleanup(() => throw new InvalidOperationException("cleanup"))))
            {
                IDisposable scope = AndroidBuildHooks.Begin(BuildEnvironment.Production);
                Assert.Throws<AggregateException>(() => scope.Dispose());
                Assert.IsTrue(restored);
            }
        }

        [Test]
        public void DuplicateAdapterIdsAreRejected()
        {
            using (AndroidBuildHooks.Register("Tests.Unique", env => null))
                Assert.Throws<ArgumentException>(() => AndroidBuildHooks.Register("Tests.Unique", env => null));
        }

        [Test]
        public void ReferenceGraphHandlesIndirectPathsAndCycles()
        {
            var window = ScriptableObject.CreateInstance<AssetReferenceExplorerWindow>();
            try
            {
                typeof(AssetReferenceExplorerWindow).GetField("targetPath", InstancePrivate).SetValue(window, "Assets/Target");
                var graph = (Dictionary<string, List<string>>)typeof(AssetReferenceExplorerWindow)
                    .GetField("referrers", InstancePrivate).GetValue(window);
                graph.Add("Assets/Target", new List<string> { "Assets/Direct" });
                graph.Add("Assets/Direct", new List<string> { "Assets/Scene", "Assets/Target" });
                typeof(AssetReferenceExplorerWindow).GetMethod("ResolveResults", InstancePrivate).Invoke(window, null);
                string chain = (string)typeof(AssetReferenceExplorerWindow).GetMethod("GetChain", InstancePrivate)
                    .Invoke(window, new object[] { "Assets/Scene" });
                Assert.AreEqual("Assets/Scene -> Assets/Direct -> Assets/Target", chain);
                var results = (List<string>)typeof(AssetReferenceExplorerWindow).GetField("results", InstancePrivate).GetValue(window);
                Assert.AreEqual(2, results.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        private static object CallBuild(string name, params object[] arguments) =>
            typeof(AndroidDevBuildWindow).GetMethod(name, StaticPrivate).Invoke(null, arguments);

        private static object Options()
        {
            var type = typeof(AndroidDevBuildWindow).GetNestedType("AndroidBuildOptions", BindingFlags.NonPublic);
            return type.GetProperty("Default").GetValue(null);
        }

        private static void Set(object options, string field, object value) => options.GetType().GetField(field).SetValue(options, value);
        private static object Get(object options, string field) => options.GetType().GetField(field).GetValue(options);

        private sealed class Cleanup : IDisposable
        {
            private readonly Action action;
            public Cleanup(Action action) => this.action = action;
            public void Dispose() => action();
        }
    }
}
