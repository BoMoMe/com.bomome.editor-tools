using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bomome.EditorTools.Build
{
    // Editor-only process runner. Never invokes a shell or passes credentials as arguments.
    [InitializeOnLoad]
    internal static class FirebaseDistributionUploader
    {
        private static Process process;
        public static bool IsBusy { get; private set; }
        public static string Status { get; private set; } = "No upload started.";
        public static string TesterUrl { get; private set; } = "";
        public static event Action Changed;

        static FirebaseDistributionUploader()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            EditorApplication.quitting += Cancel;
        }

        public static string Validate(string appId, string groups)
        {
            if (!Regex.IsMatch(appId ?? "", @"^1:\d+:android:[a-zA-Z0-9]+$"))
                return "Enter the Firebase Android App ID (1:...:android:...), not the package name.";
            if (!Regex.IsMatch(groups ?? "", @"^[a-zA-Z0-9_-]+(,[a-zA-Z0-9_-]+)*$"))
                return "Enter at least one tester group alias, separated by commas with no spaces.";
            return null;
        }

        public static ProcessStartInfo CreateCliStartInfo(string cliPath, string arguments)
        {
            cliPath = (cliPath ?? "").Trim();
            if (string.IsNullOrEmpty(cliPath)) cliPath = "firebase.exe";
            if (cliPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                cliPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                cliPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Select firebase.exe or firebase-tools/lib/bin/firebase.js, not a shell wrapper.");

            return new ProcessStartInfo
            {
                FileName = cliPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ? "node" : cliPath,
                Arguments = (cliPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ? Quote(cliPath) + " " : "") + arguments,
                WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        public static string ValidateCli(string cliPath)
        {
            var info = CreateCliStartInfo(cliPath, "--version");
            if ((cliPath ?? "").EndsWith(".js", StringComparison.OrdinalIgnoreCase) && !File.Exists(cliPath))
                return "Firebase JavaScript entry point was not found. Install Firebase CLI first.";
            if (File.Exists(info.FileName)) return null;
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                string candidate = Path.Combine(directory.Trim(' ', '\"'), info.FileName);
                if (File.Exists(candidate) || File.Exists(candidate + ".exe")) return null;
            }
            return "Could not locate " + info.FileName + ". Install Firebase CLI, select its file, and restart Unity if PATH changed.";
        }

        // Windows command-line quoting, also compatible with Unity's Mono argument parser.
        internal static string Quote(string value)
        {
            value = value ?? "";
            if (value.IndexOf('\0') >= 0) throw new ArgumentException("Arguments cannot contain null characters.");
            var quoted = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in value)
            {
                if (character == '\\') { slashes++; continue; }
                quoted.Append('\\', character == '\"' ? slashes * 2 + 1 : slashes);
                quoted.Append(character);
                slashes = 0;
            }
            quoted.Append('\\', slashes * 2);
            quoted.Append('\"');
            return quoted.ToString();
        }

        public static async void Upload(string cliPath, string apkPath, string appId, string groups, string notes)
        {
            if (IsBusy) return;
            string notesPath = null;
            try
            {
                string error = Validate(appId, groups);
                if (error != null) throw new ArgumentException(error);
                if (!File.Exists(apkPath) || !string.Equals(Path.GetExtension(apkPath), ".apk", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Select an existing APK file.");
                notesPath = Path.GetTempFileName();
                File.WriteAllText(notesPath, notes ?? "", new UTF8Encoding(false));
                TesterUrl = "";
                ProcessStartInfo startInfo = CreateCliStartInfo(cliPath,
                    "appdistribution:distribute " + Quote(Path.GetFullPath(apkPath)) +
                    " --app " + Quote(appId) + " --groups " + Quote(groups) +
                    " --release-notes-file " + Quote(notesPath) + " --non-interactive");
                var result = await Run(startInfo, "Uploading " + Path.GetFileName(apkPath) + "...");
                if (result.exitCode != 0)
                    throw new InvalidOperationException("Firebase upload failed (exit " + result.exitCode + ").\n" + result.output);
                Match link = Regex.Match(result.output, @"https://appdistribution\.firebase\.google\.com/[^\s\x1b]+?(?=\s|\x1b|$)");
                TesterUrl = link.Success ? link.Value.TrimEnd(')', '.', '\"') : "";
                Status = "Upload complete. Distributed to: " + groups;
                Debug.Log("[Firebase Distribution] " + Status + "\n" + result.output);
            }
            catch (Exception exception)
            {
                Status = exception.Message + "\nThe APK is unchanged. Use Upload Existing APK to retry.";
                Debug.LogError("[Firebase Distribution] " + Status);
            }
            finally
            {
                try { if (notesPath != null && File.Exists(notesPath)) File.Delete(notesPath); }
                catch (IOException exception) { Debug.LogWarning("Could not remove temporary release notes: " + exception.Message); }
                Changed?.Invoke();
            }
        }

        public static async void CheckConnection(string cliPath)
        {
            if (IsBusy) return;
            try
            {
                var result = await Run(CreateCliStartInfo(cliPath, "projects:list --non-interactive"), "Checking Firebase CLI and authentication...");
                Status = result.exitCode == 0 ? "Firebase CLI is connected. Project access verified." : "Connection check failed:\n" + result.output;
            }
            catch (Exception exception) { Status = exception.Message; }
            Changed?.Invoke();
        }

        public static async void GetCommitNotes(Action<string> apply)
        {
            if (IsBusy) return;
            try
            {
                var info = CreateCliStartInfo("git", "log -10 --pretty=format:%h:%s");
                var result = await Run(info, "Reading latest 10 commits...");
                if (result.exitCode == 0) { apply(result.output.Trim()); Status = "Release notes filled from latest 10 commits. Review before uploading."; }
                else Status = "Could not read commits: " + result.output;
            }
            catch (Exception exception) { Status = exception.Message; }
            Changed?.Invoke();
        }

        private static async Task<(int exitCode, string output)> Run(ProcessStartInfo info, string status)
        {
            IsBusy = true;
            Status = status;
            Changed?.Invoke();
            try
            {
                using (var current = new Process { StartInfo = info })
                {
                    process = current;
                    current.Start();
                    Task<string> stdout = current.StandardOutput.ReadToEndAsync();
                    Task<string> stderr = current.StandardError.ReadToEndAsync();
                    var deadline = DateTime.UtcNow.AddMinutes(20);
                    while (!current.HasExited)
                    {
                        if (DateTime.UtcNow > deadline) { current.Kill(); throw new TimeoutException("Firebase command timed out."); }
                        await Task.Delay(100);
                    }
                    string output = await stdout;
                    string errors = await stderr;
                    return (current.ExitCode, output + "\n" + errors);
                }
            }
            finally
            {
                process = null;
                IsBusy = false;
                Changed?.Invoke();
            }
        }

        public static void Cancel()
        {
            try { if (process != null && !process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
