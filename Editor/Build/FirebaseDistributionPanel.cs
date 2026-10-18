using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bomome.EditorTools.Build
{
    internal static class FirebaseDistributionPanel
    {
        private static string Key => "Bomome.FirebaseDistribution." + Application.dataPath + ".";

        public static void RememberApk(int environment, string path) =>
            EditorPrefs.SetString(Key + environment + ".Apk", Path.GetFullPath(path));

        private static string CliDefault
        {
            get
            {
                string npmScript = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "npm", "node_modules", "firebase-tools", "lib", "bin", "firebase.js");
                return File.Exists(npmScript) ? npmScript : "firebase.exe";
            }
        }

        public static void Draw(int environment, string label, string packageId, Func<string> build)
        {
            var settings = FirebaseDistributionSettings.instance;
            var destination = settings.GetDestination(environment);
            string prefix = Key + environment + ".";
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Firebase App Distribution", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Destination", label + " / " + packageId);
            EditorGUI.BeginChangeCheck();
            destination.appId = EditorGUILayout.TextField(new GUIContent("Firebase App ID", "Firebase Project Settings > Your apps. Must match this environment's Android package."), destination.appId);
            destination.groups = EditorGUILayout.TextField(new GUIContent("Tester Groups", "Group aliases from App Distribution > Testers & Groups. Comma-separated, e.g. designers,qa."), destination.groups);
            if (EditorGUI.EndChangeCheck()) settings.SaveSettings();

            string cli = EditorPrefs.GetString(Key + "Cli", CliDefault);
            using (new EditorGUILayout.HorizontalScope())
            {
                cli = EditorGUILayout.TextField(new GUIContent("Firebase CLI", "Standalone firebase.exe or npm firebase-tools/lib/bin/firebase.js. Node must be on PATH for the latter."), cli);
                if (GUILayout.Button("Browse", GUILayout.Width(65)))
                {
                    string selected = EditorUtility.OpenFilePanel("Firebase CLI executable or JavaScript entry point", "", "");
                    if (!string.IsNullOrEmpty(selected)) cli = selected;
                }
            }
            EditorPrefs.SetString(Key + "Cli", cli);
            EditorGUILayout.HelpBox("One-time setup: install Firebase CLI, run firebase login in a terminal, and create tester groups in Firebase App Distribution. No credentials belong in this config.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Setup Guide")) Application.OpenURL("https://firebase.google.com/docs/app-distribution/android/distribute-cli");
                using (new EditorGUI.DisabledScope(FirebaseDistributionUploader.IsBusy))
                    if (GUILayout.Button("Check Connection")) FirebaseDistributionUploader.CheckConnection(cli);
            }

            string notes = EditorPrefs.GetString(prefix + "Notes", "");
            EditorGUILayout.LabelField("Release Notes");
            notes = EditorGUILayout.TextArea(notes, GUILayout.MinHeight(60));
            EditorPrefs.SetString(prefix + "Notes", notes);
            using (new EditorGUI.DisabledScope(FirebaseDistributionUploader.IsBusy))
                if (GUILayout.Button("Use Latest 10 Commit Messages"))
                    FirebaseDistributionUploader.GetCommitNotes(value => EditorPrefs.SetString(prefix + "Notes", value));

            string apk = EditorPrefs.GetString(prefix + "Apk", "");
            using (new EditorGUILayout.HorizontalScope())
            {
                apk = EditorGUILayout.TextField("Existing APK", apk);
                if (GUILayout.Button("Browse", GUILayout.Width(65)))
                {
                    string selected = EditorUtility.OpenFilePanel("Select " + label + " APK", "", "apk");
                    if (!string.IsNullOrEmpty(selected)) apk = selected;
                }
            }
            EditorPrefs.SetString(prefix + "Apk", apk);
            using (new EditorGUI.DisabledScope(FirebaseDistributionUploader.IsBusy || EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build & Upload " + label, GUILayout.Height(30)) && Confirm(cli, destination, label, packageId, "New APK"))
                {
                    // Snapshot the destination before the build; a failed upload can reuse this APK.
                    string appId = destination.appId;
                    string groups = destination.groups;
                    string builtApk = build();
                    if (!string.IsNullOrEmpty(builtApk))
                    {
                        RememberApk(environment, builtApk);
                        FirebaseDistributionUploader.Upload(cli, builtApk, appId, groups, notes);
                    }
                }
                using (new EditorGUI.DisabledScope(!File.Exists(apk)))
                    if (GUILayout.Button("Upload Existing APK", GUILayout.Height(30)) && Confirm(cli, destination, label, packageId, apk))
                        FirebaseDistributionUploader.Upload(cli, apk, destination.appId, destination.groups, notes);
            }
            EditorGUILayout.HelpBox(FirebaseDistributionUploader.Status, MessageType.None);
            if (FirebaseDistributionUploader.IsBusy && GUILayout.Button("Cancel Upload / Check")) FirebaseDistributionUploader.Cancel();
            if (!string.IsNullOrEmpty(FirebaseDistributionUploader.TesterUrl) && GUILayout.Button("Open Tester Download Link"))
                Application.OpenURL(FirebaseDistributionUploader.TesterUrl);
        }

        private static bool Confirm(string cli, FirebaseDistributionSettings.Destination destination, string label, string packageId, string apk)
        {
            string error = FirebaseDistributionUploader.Validate(destination.appId, destination.groups);
            try
            {
                error = error ?? FirebaseDistributionUploader.ValidateCli(cli);
            }
            catch (Exception exception) { error = exception.Message; }
            if (error != null)
            {
                EditorUtility.DisplayDialog("Firebase Setup Required", error, "OK");
                return false;
            }
            return EditorUtility.DisplayDialog("Distribute " + label + " Build?",
                "APK: " + apk + "\nExpected package: " + packageId + "\nFirebase App: " + destination.appId +
                "\nTester groups: " + destination.groups + "\n\nThis will upload the APK and notify these testers. Verify an existing APK belongs to this environment.", "Continue", "Cancel");
        }
    }
}
