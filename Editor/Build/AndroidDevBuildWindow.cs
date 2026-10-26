using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bomome.EditorTools.Build
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Bomome.EditorTools.Build", sourceAssembly: "Assembly-CSharp-Editor")]
    public sealed class AndroidDevBuildWindow : EditorWindow
    {
        private const string WindowTitle = "Android Build";
        private const string MenuPath = "Bomome/Build/Android APK Build...";
        private const string QuickBuildMenuPath = "Bomome/Build/Build Selected Android APK";
        private static string OutputFolderPrefKey => EditorToolPreferences.Key("Bomome.AndroidBuild.OutputFolder");
        private static string EnvironmentPrefKey => EditorToolPreferences.Key("Bomome.AndroidBuild.Environment");
        private static string CommitAndTagPrefKey => EditorToolPreferences.Key("Bomome.AndroidBuild.CommitAndTag");
        private static string CommitMessagePrefKey => EditorToolPreferences.Key("Bomome.AndroidBuild.CommitMessage");
        private static string TagFormatPrefKey => EditorToolPreferences.Key("Bomome.AndroidBuild.TagFormat");
        private static string Il2CppConfigurationPrefKey => EditorToolPreferences.Key("Bomome.AndroidBuild.Il2CppConfiguration");
        private static string FirebaseEnabledPrefKey => EditorToolPreferences.Key("Bomome.AndroidBuild.FirebaseEnabled");
        private static string AppendSuffixPrefKey => EditorToolPreferences.Key("Bomome.AndroidBuild.AppendEnvironmentSuffix");
        private const string DevSuffix = "_dev";
        private const string StageSuffix = "_stage";
        private const string DefaultCommitMessage = "Build {env} {version} ({build})";
        private const string DefaultTagFormat = "{env}-v{version}-b{build}";

        private string outputFolder;
        private BuildEnvironment buildEnvironment = BuildEnvironment.Development;
        private bool developmentBuild = true;
        private bool appendEnvironmentSuffix = true;
        private bool connectProfiler;
        private bool autoRunPlayer;
        private Il2CppBuildConfiguration il2CppConfiguration = Il2CppBuildConfiguration.Debug;
        private bool commitAndTag;
        private string commitMessageFormat = DefaultCommitMessage;
        private string tagFormat = DefaultTagFormat;
        private VersionIncrement versionIncrement = VersionIncrement.Patch;
        private Vector2 scrollPosition;
        private bool firebaseEnabled;

        private enum VersionIncrement
        {
            Patch,
            Minor,
            Major,
            None
        }

        private enum Il2CppBuildConfiguration
        {
            Auto,
            Debug,
            Release
        }

        [MenuItem(MenuPath)]
        public static void Open()
        {
            AndroidDevBuildWindow window = GetWindow<AndroidDevBuildWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(470f, 310f);
            window.LoadSettings();
            window.Show();
        }

        [MenuItem(QuickBuildMenuPath)]
        public static void BuildFromMenu()
        {
            AndroidBuildOptions options = AndroidBuildOptions.Default;
            options.OutputFolder = GetDefaultOutputFolder();
            options.Environment = GetSavedEnvironment();
            options.AppendEnvironmentSuffix = EditorPrefs.GetBool(AppendSuffixPrefKey, true);
            options.Il2CppConfiguration = GetSavedIl2CppConfiguration();
            options.CommitAndTag = EditorPrefs.GetBool(CommitAndTagPrefKey, false);
            options.CommitMessageFormat = EditorPrefs.GetString(CommitMessagePrefKey, DefaultCommitMessage);
            options.TagFormat = EditorPrefs.GetString(TagFormatPrefKey, DefaultTagFormat);
            options = ApplyConfiguredSettings(options);
            BuildAndroid(options);
        }

        [MenuItem("Bomome/Build/Build Store AAB")]
        public static void BuildStoreFromMenu()
        {
            AndroidBuildOptions options = AndroidBuildOptions.Default;
            options.OutputFolder = EditorPrefs.GetString(OutputFolderPrefKey, GetDefaultOutputFolder());
            options.AppendEnvironmentSuffix = EditorPrefs.GetBool(AppendSuffixPrefKey, true);
            options = ApplyConfiguredSettings(options);
            // A quick store build must not silently stage changes or create a Git tag.
            BuildAndroid(CreateStoreOptions(options));
        }

        private static AndroidBuildOptions CreateStoreOptions(AndroidBuildOptions options)
        {
            options.StoreBundle = true;
            options.Environment = BuildEnvironment.Production;
            options.DevelopmentBuild = false;
            options.ConnectProfiler = false;
            options.AutoRunPlayer = false;
            options.Il2CppConfiguration = Il2CppBuildConfiguration.Release;
            return options;
        }

        private void OnEnable()
        {
            LoadSettings();
            FirebaseDistributionUploader.Changed += Repaint;
        }

        private void OnDisable() => FirebaseDistributionUploader.Changed -= Repaint;

        private void OnGUI()
        {
            if (EditorToolsProjectSettings.instance.AndroidBuild != null) LoadSettings();
            EditorGUI.BeginChangeCheck();
            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scroll.scrollPosition;
                DrawBuildControls();
            }
            if (EditorGUI.EndChangeCheck()) SaveSettings();
        }

        private void DrawBuildControls()
        {
            var assigned = EditorToolsProjectSettings.instance.AndroidBuild;
            if (assigned != null) EditorGUILayout.ObjectField("Settings Asset", assigned, typeof(AndroidBuildToolSettings), false);
            if (GUILayout.Button("Project Tool Settings"))
                SettingsService.OpenProjectSettings("Project/Bomome/Editor Tools");
            EditorGUILayout.LabelField("Android APK / Store AAB Build", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds Dev, Stage, or Production APKs. Optionally applies app ID suffixes and registered project build adapters, then restores temporary settings after the build.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            EditorGUI.BeginChangeCheck();
            buildEnvironment = (BuildEnvironment)EditorGUILayout.EnumPopup("Environment", buildEnvironment);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyEnvironmentDefaults();
                SaveSettings();
            }

            appendEnvironmentSuffix = EditorGUILayout.Toggle(new GUIContent("Append Environment Suffix",
                "Add _dev/_stage to the app ID. Turn off to keep Player Settings' exact app ID, independent of Development Build."), appendEnvironmentSuffix);
            string currentIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            string targetIdentifier = CreateEnvironmentIdentifier(currentIdentifier, buildEnvironment, appendEnvironmentSuffix);
            string nextVersion = IncrementVersion(PlayerSettings.bundleVersion, versionIncrement);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Current Settings", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Product", PlayerSettings.productName);
                EditorGUILayout.LabelField("Current Version", PlayerSettings.bundleVersion);
                EditorGUILayout.LabelField("Build Version", nextVersion);
                EditorGUILayout.LabelField("Current Android Build Number", PlayerSettings.Android.bundleVersionCode.ToString());
                EditorGUILayout.LabelField("Build Number", Mathf.Max(1, PlayerSettings.Android.bundleVersionCode + 1).ToString());
                EditorGUILayout.LabelField("Current App Id", currentIdentifier);
                EditorGUILayout.LabelField("Build App Id", targetIdentifier);
                EditorGUILayout.LabelField("APK Name", CreateApkName(PlayerSettings.productName, nextVersion, buildEnvironment));
            }

            EditorGUILayout.Space(8f);
            versionIncrement = (VersionIncrement)EditorGUILayout.EnumPopup("Version Increment", versionIncrement);
            using (new EditorGUI.DisabledScope(buildEnvironment == BuildEnvironment.Production))
            {
                if (buildEnvironment == BuildEnvironment.Production)
                {
                    developmentBuild = false;
                    connectProfiler = false;
                    il2CppConfiguration = Il2CppBuildConfiguration.Release;
                }
                developmentBuild = EditorGUILayout.Toggle("Development Build", developmentBuild);
                connectProfiler = EditorGUILayout.Toggle("Connect Profiler", connectProfiler);
            }
            autoRunPlayer = EditorGUILayout.Toggle("Auto Run Player", autoRunPlayer);
            using (new EditorGUI.DisabledScope(buildEnvironment == BuildEnvironment.Production))
                il2CppConfiguration = (Il2CppBuildConfiguration)EditorGUILayout.EnumPopup("IL2CPP Config", il2CppConfiguration);
            EditorGUILayout.HelpBox("Use Debug for Dev/Stage if Android clang crashes while compiling IL2CPP output. Production should normally use Release.", MessageType.None);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                commitAndTag = EditorGUILayout.Toggle("Commit And Tag", commitAndTag);
                using (new EditorGUI.DisabledScope(!commitAndTag))
                {
                    EditorGUILayout.HelpBox("Runs after a successful build. Stages current repo changes, creates a commit, then creates an annotated tag. Ignored APK/build output will not be committed.", MessageType.Info);
                    commitMessageFormat = EditorGUILayout.TextField("Commit Message", commitMessageFormat);
                    tagFormat = EditorGUILayout.TextField("Tag Format", tagFormat);
                    EditorGUILayout.LabelField("Preview Commit", FormatBuildText(commitMessageFormat, buildEnvironment, nextVersion, Mathf.Max(1, PlayerSettings.Android.bundleVersionCode + 1)));
                    EditorGUILayout.LabelField("Preview Tag", FormatBuildText(tagFormat, buildEnvironment, nextVersion, Mathf.Max(1, PlayerSettings.Android.bundleVersionCode + 1)));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(80f)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Android APK Output Folder", outputFolder, string.Empty);
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        outputFolder = selected;
                        SaveSettings();
                    }
                }
            }

            EditorGUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || FirebaseDistributionUploader.IsBusy ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer))
            {
                if (GUILayout.Button($"Build {GetEnvironmentLabel(buildEnvironment)} APK", GUILayout.Height(36f)))
                {
                    SaveSettings();
                    BuildAndroid(GetBuildOptions());
                }
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox("Store AAB uses Production app ID and project adapters, IL2CPP Release, and no development, profiler, or auto-run flags. Configure your upload keystore in Android Player Settings > Publishing Settings. This creates a file only; it does not upload to Google Play.", MessageType.Info);
                EditorGUILayout.LabelField("Store AAB Name", Path.ChangeExtension(CreateApkName(PlayerSettings.productName, nextVersion, BuildEnvironment.Production), ".aab"));
                if (GUILayout.Button("Build Store AAB (Production)", GUILayout.Height(36f)))
                {
                    SaveSettings();
                    BuildAndroid(CreateStoreOptions(GetBuildOptions()));
                }
            }
            EditorGUI.BeginChangeCheck();
            firebaseEnabled = EditorGUILayout.Toggle("Firebase Distribution", firebaseEnabled);
            if (EditorGUI.EndChangeCheck()) SaveSettings();
            if (firebaseEnabled) FirebaseDistributionPanel.Draw((int)buildEnvironment, GetEnvironmentLabel(buildEnvironment), targetIdentifier, () =>
            {
                SaveSettings();
                return BuildAndroid(GetBuildOptions());
            });
        }

        private AndroidBuildOptions GetBuildOptions() => new AndroidBuildOptions
        {
            OutputFolder = outputFolder,
            Environment = buildEnvironment,
            DevelopmentBuild = developmentBuild,
            AppendEnvironmentSuffix = appendEnvironmentSuffix,
            ConnectProfiler = connectProfiler,
            AutoRunPlayer = autoRunPlayer,
            Il2CppConfiguration = il2CppConfiguration,
            CommitAndTag = commitAndTag,
            CommitMessageFormat = commitMessageFormat,
            TagFormat = tagFormat,
            VersionIncrementMode = versionIncrement
        };

        private void LoadSettings()
        {
            firebaseEnabled = EditorPrefs.GetBool(FirebaseEnabledPrefKey, false);
            appendEnvironmentSuffix = EditorPrefs.GetBool(AppendSuffixPrefKey, true);
            outputFolder = EditorPrefs.GetString(OutputFolderPrefKey, GetDefaultOutputFolder());
            buildEnvironment = GetSavedEnvironment();
            developmentBuild = buildEnvironment != BuildEnvironment.Production;
            connectProfiler = false;
            il2CppConfiguration = GetSavedIl2CppConfiguration();
            commitAndTag = EditorPrefs.GetBool(CommitAndTagPrefKey, false);
            commitMessageFormat = EditorPrefs.GetString(CommitMessagePrefKey, DefaultCommitMessage);
            tagFormat = EditorPrefs.GetString(TagFormatPrefKey, DefaultTagFormat);
            var config = EditorToolsProjectSettings.instance.AndroidBuild;
            if (config != null)
            {
                outputFolder = config.OutputFolder;
                buildEnvironment = config.Environment;
                developmentBuild = config.DevelopmentBuild;
                appendEnvironmentSuffix = config.AppendEnvironmentSuffix;
                connectProfiler = config.ConnectProfiler;
                autoRunPlayer = config.AutoRunPlayer;
                versionIncrement = (VersionIncrement)config.VersionIncrement;
                il2CppConfiguration = (Il2CppBuildConfiguration)config.Il2CppConfiguration;
                firebaseEnabled = config.FirebaseDistribution;
            }
        }

        private void SaveSettings()
        {
            var config = EditorToolsProjectSettings.instance.AndroidBuild;
            if (config != null)
            {
                Undo.RecordObject(config, "Change Android build settings");
                config.OutputFolder = outputFolder;
                config.Environment = buildEnvironment;
                config.DevelopmentBuild = developmentBuild;
                config.AppendEnvironmentSuffix = appendEnvironmentSuffix;
                config.ConnectProfiler = connectProfiler;
                config.AutoRunPlayer = autoRunPlayer;
                config.VersionIncrement = (ToolVersionIncrement)versionIncrement;
                config.Il2CppConfiguration = (ToolIl2CppConfiguration)il2CppConfiguration;
                config.FirebaseDistribution = firebaseEnabled;
                EditorUtility.SetDirty(config);
            }
            else
            {
                EditorPrefs.SetBool(FirebaseEnabledPrefKey, firebaseEnabled);
                EditorPrefs.SetBool(AppendSuffixPrefKey, appendEnvironmentSuffix);
                EditorPrefs.SetString(OutputFolderPrefKey, string.IsNullOrWhiteSpace(outputFolder) ? GetDefaultOutputFolder() : outputFolder);
                EditorPrefs.SetInt(EnvironmentPrefKey, (int)buildEnvironment);
                EditorPrefs.SetInt(Il2CppConfigurationPrefKey, (int)il2CppConfiguration);
            }
            EditorPrefs.SetBool(CommitAndTagPrefKey, commitAndTag);
            EditorPrefs.SetString(CommitMessagePrefKey, string.IsNullOrWhiteSpace(commitMessageFormat) ? DefaultCommitMessage : commitMessageFormat);
            EditorPrefs.SetString(TagFormatPrefKey, string.IsNullOrWhiteSpace(tagFormat) ? DefaultTagFormat : tagFormat);
        }

        private static AndroidBuildOptions ApplyConfiguredSettings(AndroidBuildOptions options)
        {
            var config = EditorToolsProjectSettings.instance.AndroidBuild;
            if (config == null) return options;
            options.OutputFolder = config.OutputFolder;
            options.Environment = config.Environment;
            options.DevelopmentBuild = config.DevelopmentBuild;
            options.AppendEnvironmentSuffix = config.AppendEnvironmentSuffix;
            options.ConnectProfiler = config.ConnectProfiler;
            options.AutoRunPlayer = config.AutoRunPlayer;
            options.VersionIncrementMode = (VersionIncrement)config.VersionIncrement;
            options.Il2CppConfiguration = (Il2CppBuildConfiguration)config.Il2CppConfiguration;
            return options;
        }

        private void ApplyEnvironmentDefaults()
        {
            if (buildEnvironment == BuildEnvironment.Production)
            {
                developmentBuild = false;
                connectProfiler = false;
            }
            else if (buildEnvironment == BuildEnvironment.Development)
            {
                developmentBuild = true;
                il2CppConfiguration = Il2CppBuildConfiguration.Debug;
            }
            else if (buildEnvironment == BuildEnvironment.Stage)
            {
                developmentBuild = true;
                connectProfiler = false;
                il2CppConfiguration = Il2CppBuildConfiguration.Debug;
            }

            if (buildEnvironment == BuildEnvironment.Production)
            {
                il2CppConfiguration = Il2CppBuildConfiguration.Release;
            }
        }

        private static string BuildAndroid(AndroidBuildOptions options)
        {
            if (EditorApplication.isCompiling || FirebaseDistributionUploader.IsBusy || BuildPipeline.isBuildingPlayer || EditorApplication.isPlayingOrWillChangePlaymode)
                return null;
            if (!EnsureAndroidTarget())
            {
                return null;
            }

            if (options.StoreBundle)
            {
                options = CreateStoreOptions(options);
                if (!ValidateStoreSettings()) return null;
            }

            // Quick builds start from development defaults, so enforce release policy here too.
            if (options.Environment == BuildEnvironment.Production)
            {
                options.DevelopmentBuild = false;
                options.ConnectProfiler = false;
                options.Il2CppConfiguration = Il2CppBuildConfiguration.Release;
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Android Build Failed", "No enabled scenes were found in Build Settings.", "OK");
                return null;
            }

            string originalIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            IDisposable environmentScope = null;
            string originalIl2CppArgs = null;
            string nextVersion = IncrementVersion(PlayerSettings.bundleVersion, options.VersionIncrementMode);
            int nextBuildNumber = Mathf.Max(1, PlayerSettings.Android.bundleVersionCode + 1);
            string buildIdentifier = CreateEnvironmentIdentifier(originalIdentifier, options.Environment, options.AppendEnvironmentSuffix);
            string outputFolder = string.IsNullOrWhiteSpace(options.OutputFolder) ? GetDefaultOutputFolder() : options.OutputFolder;
            outputFolder = EditorToolsProjectSettings.ResolveOutputFolder(outputFolder, GetDefaultOutputFolder());
            string fileName = CreateApkName(PlayerSettings.productName, nextVersion, options.Environment);
            if (options.StoreBundle) fileName = Path.ChangeExtension(fileName, ".aab");
            string buildPath = Path.Combine(outputFolder, fileName);
            string format = options.StoreBundle ? "AAB" : "APK";
            bool buildSucceeded = false;
            bool originalBundle = EditorUserBuildSettings.buildAppBundle;
            bool originalExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            bool originalDevelopment = EditorUserBuildSettings.development;
            bool originalProfiler = EditorUserBuildSettings.connectProfiler;

            Directory.CreateDirectory(outputFolder);

            try
            {
                EditorUserBuildSettings.buildAppBundle = options.StoreBundle;
                EditorUserBuildSettings.development = options.DevelopmentBuild;
                EditorUserBuildSettings.connectProfiler = options.DevelopmentBuild && options.ConnectProfiler;
                if (options.StoreBundle) EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                PlayerSettings.bundleVersion = nextVersion;
                PlayerSettings.Android.bundleVersionCode = nextBuildNumber;
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, buildIdentifier);
                environmentScope = AndroidBuildHooks.Begin(options.Environment);
                originalIl2CppArgs = ApplyIl2CppConfiguration(options.Il2CppConfiguration);
                AssetDatabase.SaveAssets();

                BuildPlayerOptions buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = buildPath,
                    target = BuildTarget.Android,
                    options = CreateBuildOptions(options)
                };

                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    buildSucceeded = true;
                    if (!options.StoreBundle) FirebaseDistributionPanel.RememberApk((int)options.Environment, buildPath);
                    EditorPrefs.SetString(OutputFolderPrefKey, outputFolder);
                    EditorUtility.RevealInFinder(buildPath);
                    Debug.Log($"{GetEnvironmentLabel(options.Environment)} {format} built: {buildPath} | version {nextVersion} | build {nextBuildNumber} | id {buildIdentifier} | build options {summary.options}");
                }
                else
                {
                    Debug.LogError($"{GetEnvironmentLabel(options.Environment)} {format} build failed: {summary.result}");
                }
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = originalBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = originalExport;
                EditorUserBuildSettings.development = originalDevelopment;
                EditorUserBuildSettings.connectProfiler = originalProfiler;
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, originalIdentifier);
                try { environmentScope?.Dispose(); }
                finally
                {
                    RestoreIl2CppArgs(originalIl2CppArgs);
                    AssetDatabase.SaveAssets();
                }
            }

            if (buildSucceeded && options.CommitAndTag)
            {
                CommitAndTagBuild(options, nextVersion, nextBuildNumber);
            }
            return buildSucceeded ? Path.GetFullPath(buildPath) : null;
        }

        private static bool ValidateStoreSettings()
        {
            string issue = null;
            string manifestPath = Path.Combine(Application.dataPath, "Plugins", "Android", "AndroidManifest.xml");
            if (File.Exists(manifestPath))
            {
                var manifest = new XmlDocument { XmlResolver = null };
                manifest.Load(manifestPath);
                XmlElement application = manifest.DocumentElement?["application"];
                string debuggable = application?.GetAttribute("debuggable", "http://schemas.android.com/apk/res/android");
                if (!string.IsNullOrEmpty(debuggable) && !string.Equals(debuggable, "false", StringComparison.OrdinalIgnoreCase))
                {
                    EditorUtility.DisplayDialog("Store AAB Setup Required",
                        "Remove the android:debuggable override from Assets/Plugins/Android/AndroidManifest.xml. Store bundles must not force debugging on.", "OK");
                    return false;
                }
            }
            if (!PlayerSettings.Android.useCustomKeystore ||
                string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName) ||
                string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
                issue = "Configure a custom upload keystore and key alias in Android Player Settings > Publishing Settings. Store bundles must not use the debug keystore.";
            else if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
                (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                issue = "Set the Android scripting backend to IL2CPP and enable ARM64 in Player Settings before building a store bundle.";

            if (issue == null) return true;
            EditorUtility.DisplayDialog("Store AAB Setup Required", issue, "OK");
            return false;
        }

        private static bool EnsureAndroidTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                return true;
            }

            bool switchTarget = EditorUtility.DisplayDialog(
                "Switch Build Target",
                "Android is not the active build target. Switch to Android now?",
                "Switch",
                "Cancel");

            return switchTarget && EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        private static BuildOptions CreateBuildOptions(AndroidBuildOptions options)
        {
            BuildOptions buildOptions = BuildOptions.None;
            if (options.Environment != BuildEnvironment.Production && options.DevelopmentBuild)
            {
                buildOptions |= BuildOptions.Development;
            }

            if (options.Environment != BuildEnvironment.Production && options.DevelopmentBuild && options.ConnectProfiler)
            {
                buildOptions |= BuildOptions.ConnectWithProfiler;
            }

            if (options.AutoRunPlayer)
            {
                buildOptions |= BuildOptions.AutoRunPlayer;
            }

            return buildOptions;
        }

        private static string IncrementVersion(string version, VersionIncrement increment)
        {
            if (increment == VersionIncrement.None)
            {
                return string.IsNullOrWhiteSpace(version) ? "0.1.0" : version;
            }

            int[] parts = ParseVersion(version);
            switch (increment)
            {
                case VersionIncrement.Major:
                    parts[0]++;
                    parts[1] = 0;
                    parts[2] = 0;
                    break;
                case VersionIncrement.Minor:
                    parts[1]++;
                    parts[2] = 0;
                    break;
                default:
                    parts[2]++;
                    break;
            }

            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        }

        private static int[] ParseVersion(string version)
        {
            int[] parts = { 0, 1, 0 };
            if (string.IsNullOrWhiteSpace(version))
            {
                return parts;
            }

            string[] tokens = version.Split('.');
            for (int i = 0; i < Mathf.Min(parts.Length, tokens.Length); i++)
            {
                if (int.TryParse(tokens[i], out int value))
                {
                    parts[i] = Mathf.Max(0, value);
                }
            }

            return parts;
        }

        private static string CreateEnvironmentIdentifier(string identifier, BuildEnvironment environment, bool appendSuffix)
        {
            if (!appendSuffix) return identifier;
            string baseIdentifier = RemoveEnvironmentSuffix(identifier);
            if (string.IsNullOrWhiteSpace(identifier))
            {
                baseIdentifier = "com.bomome.game";
            }

            switch (environment)
            {
                case BuildEnvironment.Development:
                    return $"{baseIdentifier}{DevSuffix}";
                case BuildEnvironment.Stage:
                    return $"{baseIdentifier}{StageSuffix}";
                default:
                    return baseIdentifier;
            }
        }

        private static string RemoveEnvironmentSuffix(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return string.Empty;
            }

            if (identifier.EndsWith(DevSuffix, StringComparison.Ordinal))
            {
                return identifier.Substring(0, identifier.Length - DevSuffix.Length);
            }

            if (identifier.EndsWith(StageSuffix, StringComparison.Ordinal))
            {
                return identifier.Substring(0, identifier.Length - StageSuffix.Length);
            }

            return identifier;
        }

        private static string CreateApkName(string productName, string version, BuildEnvironment environment)
        {
            string safeProductName = SanitizeFileName(string.IsNullOrWhiteSpace(productName) ? "Game" : productName);
            string prefix = GetEnvironmentFilePrefix(environment);
            return $"{prefix}_{safeProductName}_{version}.apk";
        }

        private static string GetEnvironmentFilePrefix(BuildEnvironment environment)
        {
            switch (environment)
            {
                case BuildEnvironment.Development:
                    return "dev";
                case BuildEnvironment.Stage:
                    return "stage";
                default:
                    return "production";
            }
        }

        private static string GetEnvironmentLabel(BuildEnvironment environment)
        {
            switch (environment)
            {
                case BuildEnvironment.Development:
                    return "Dev";
                case BuildEnvironment.Stage:
                    return "Stage";
                default:
                    return "Production";
            }
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "Game" : sanitized;
        }

        private static string ApplyIl2CppConfiguration(Il2CppBuildConfiguration configuration)
        {
            string originalArgs = GetAdditionalIl2CppArgs();
            if (configuration == Il2CppBuildConfiguration.Auto)
            {
                return originalArgs;
            }

            string args = RemoveIl2CppConfigurationArg(originalArgs);
            string configurationArg = $"--configuration={configuration}";
            string updatedArgs = string.IsNullOrWhiteSpace(args)
                ? configurationArg
                : $"{args} {configurationArg}";
            SetAdditionalIl2CppArgs(updatedArgs);
            Debug.Log($"Temporarily using IL2CPP {configuration} configuration for Android build.");
            return originalArgs;
        }

        private static void RestoreIl2CppArgs(string originalArgs)
        {
            if (originalArgs == null)
            {
                return;
            }

            SetAdditionalIl2CppArgs(originalArgs);
        }

        private static string RemoveIl2CppConfigurationArg(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                return string.Empty;
            }

            return Regex.Replace(args, @"(^|\s)--configuration=(Debug|Release)(?=\s|$)", " ").Trim();
        }

        private static string GetAdditionalIl2CppArgs()
        {
            MethodInfo method = typeof(PlayerSettings).GetMethod(
                "GetAdditionalIl2CppArgs",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);

            return method?.Invoke(null, null) as string ?? string.Empty;
        }

        private static void SetAdditionalIl2CppArgs(string args)
        {
            MethodInfo method = typeof(PlayerSettings).GetMethod(
                "SetAdditionalIl2CppArgs",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);

            if (method == null)
            {
                Debug.LogWarning("PlayerSettings.SetAdditionalIl2CppArgs was not found. IL2CPP config override was skipped.");
                return;
            }

            method.Invoke(null, new object[] { args ?? string.Empty });
        }

        private static void CommitAndTagBuild(AndroidBuildOptions options, string version, int buildNumber)
        {
            string commitMessage = FormatBuildText(options.CommitMessageFormat, options.Environment, version, buildNumber);
            string tagName = FormatBuildText(options.TagFormat, options.Environment, version, buildNumber);

            if (string.IsNullOrWhiteSpace(commitMessage))
            {
                commitMessage = FormatBuildText(DefaultCommitMessage, options.Environment, version, buildNumber);
            }

            if (string.IsNullOrWhiteSpace(tagName))
            {
                tagName = FormatBuildText(DefaultTagFormat, options.Environment, version, buildNumber);
            }

            tagName = SanitizeGitRefName(tagName);
            if (string.IsNullOrWhiteSpace(tagName))
            {
                Debug.LogError("Git tag was empty after sanitization. Commit/tag step skipped.");
                return;
            }

            if (!RunGit("add -A", out string addOutput))
            {
                Debug.LogError($"Git add failed:\n{addOutput}");
                return;
            }

            int diffExitCode = RunGitRaw("diff --cached --quiet", out string diffOutput);
            if (diffExitCode == 1)
            {
                if (!RunGit($"commit -m \"{EscapeGitArgument(commitMessage)}\"", out string commitOutput))
                {
                    Debug.LogError($"Git commit failed:\n{commitOutput}");
                    return;
                }
            }
            else if (diffExitCode != 0)
            {
                Debug.LogError($"Git diff check failed:\n{diffOutput}");
                return;
            }
            else
            {
                Debug.Log("No staged changes found after build; tag will be created on the current HEAD.");
            }

            if (!RunGit($"tag -a \"{EscapeGitArgument(tagName)}\" -m \"{EscapeGitArgument(commitMessage)}\"", out string tagOutput))
            {
                Debug.LogError($"Git tag failed:\n{tagOutput}");
                return;
            }

            Debug.Log($"Git commit/tag complete: {tagName}");
        }

        private static string FormatBuildText(string format, BuildEnvironment environment, string version, int buildNumber)
        {
            string value = string.IsNullOrWhiteSpace(format) ? string.Empty : format;
            return value
                .Replace("{env}", GetEnvironmentFilePrefix(environment))
                .Replace("{Env}", GetEnvironmentLabel(environment))
                .Replace("{version}", version ?? string.Empty)
                .Replace("{build}", buildNumber.ToString());
        }

        private static string SanitizeGitRefName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] invalidCharacters = { ' ', '~', '^', ':', '?', '*', '[', '\\' };
            string sanitized = value.Trim();
            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                sanitized = sanitized.Replace(invalidCharacters[i], '-');
            }

            while (sanitized.Contains(".."))
            {
                sanitized = sanitized.Replace("..", ".");
            }

            sanitized = sanitized.Trim('/', '.', '-');
            return sanitized.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
                ? sanitized.Substring(0, sanitized.Length - 5)
                : sanitized;
        }

        private static string EscapeGitArgument(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool RunGit(string arguments, out string output)
        {
            return RunGitRaw(arguments, out output) == 0;
        }

        private static int RunGitRaw(string arguments, out string output)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    output = "Git process could not be started.";
                    return -1;
                }

                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                string standardOutput = stdout.GetAwaiter().GetResult();
                string standardError = stderr.GetAwaiter().GetResult();
                output = string.IsNullOrWhiteSpace(standardError)
                    ? standardOutput
                    : $"{standardOutput}\n{standardError}";
                return process.ExitCode;
            }
        }

        private static string GetDefaultOutputFolder()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android"));
        }

        private static BuildEnvironment GetSavedEnvironment()
        {
            return (BuildEnvironment)Mathf.Clamp(
                EditorPrefs.GetInt(EnvironmentPrefKey, (int)BuildEnvironment.Development),
                0,
                Enum.GetValues(typeof(BuildEnvironment)).Length - 1);
        }

        private static Il2CppBuildConfiguration GetSavedIl2CppConfiguration()
        {
            return (Il2CppBuildConfiguration)Mathf.Clamp(
                EditorPrefs.GetInt(Il2CppConfigurationPrefKey, (int)Il2CppBuildConfiguration.Debug),
                0,
                Enum.GetValues(typeof(Il2CppBuildConfiguration)).Length - 1);
        }

        private struct AndroidBuildOptions
        {
            public string OutputFolder;
            public BuildEnvironment Environment;
            public bool DevelopmentBuild;
            public bool AppendEnvironmentSuffix;
            public bool ConnectProfiler;
            public bool AutoRunPlayer;
            public bool StoreBundle;
            public Il2CppBuildConfiguration Il2CppConfiguration;
            public bool CommitAndTag;
            public string CommitMessageFormat;
            public string TagFormat;
            public VersionIncrement VersionIncrementMode;

            public static AndroidBuildOptions Default => new AndroidBuildOptions
            {
                OutputFolder = GetDefaultOutputFolder(),
                Environment = BuildEnvironment.Development,
                DevelopmentBuild = true,
                AppendEnvironmentSuffix = true,
                ConnectProfiler = false,
                AutoRunPlayer = false,
                Il2CppConfiguration = Il2CppBuildConfiguration.Debug,
                CommitAndTag = false,
                CommitMessageFormat = DefaultCommitMessage,
                TagFormat = DefaultTagFormat,
                VersionIncrementMode = VersionIncrement.Patch
            };
        }
    }
}
