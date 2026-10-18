using System;
using System.IO;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Bomome.EditorTools.Capture
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Bomome.EditorTools.Capture", sourceAssembly: "Assembly-CSharp-Editor")]
    public sealed class GameScreenshotCaptureWindow : EditorWindow
    {
        private const string WindowTitle = "Game Screenshot";
        private const string MenuPath = "Bomome/Tools/Game Screenshot...";
        private const string QuickCaptureMenuPath = "Bomome/Tools/Capture Game Screenshot";
        private const string ShortcutId = "Bomome/Capture Game Screenshot";
        private static string OutputFolderPrefKey => EditorToolPreferences.Key("Bomome.GameScreenshot.OutputFolder");
        private static string FormatPrefKey => EditorToolPreferences.Key("Bomome.GameScreenshot.Format");
        private static string ScalePrefKey => EditorToolPreferences.Key("Bomome.GameScreenshot.Scale");
        private static string JpegQualityPrefKey => EditorToolPreferences.Key("Bomome.GameScreenshot.JpegQuality");
        private static string FileNameFormatPrefKey => EditorToolPreferences.Key("Bomome.GameScreenshot.FileNameFormat");
        private const string DefaultFileNameFormat = "{product}_{scene}_{yyyyMMdd_HHmmss}";

        private string outputFolder;
        private ScreenshotFormat outputFormat = ScreenshotFormat.Png;
        private int scale = 1;
        private int jpegQuality = 92;
        private string fileNameFormat = DefaultFileNameFormat;

        private enum ScreenshotFormat
        {
            Png,
            Jpg
        }

        [MenuItem(MenuPath)]
        public static void Open()
        {
            GameScreenshotCaptureWindow window = GetWindow<GameScreenshotCaptureWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(440f, 260f);
            window.LoadSettings();
            window.Show();
        }

        [MenuItem(QuickCaptureMenuPath)]
        public static void CaptureFromMenu()
        {
            CaptureWithSavedSettings();
        }

        [Shortcut(ShortcutId, KeyCode.F12, ShortcutModifiers.Action)]
        private static void CaptureShortcut()
        {
            var config = EditorToolsProjectSettings.instance.Capture;
            if (config != null && (!config.HotkeyEnabled || config.Hotkey == KeyCode.None)) return;
            CaptureWithSavedSettings();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void OnGUI()
        {
            var config = EditorToolsProjectSettings.instance.Capture;
            if (config != null)
            {
                EditorGUILayout.ObjectField("Settings Asset", config, typeof(ScreenshotCaptureSettings), false);
                var data = new SerializedObject(config);
                data.Update();
                foreach (string field in new[] { "OutputFolder", "Format", "Scale", "JpegQuality", "FileNameFormat", "HotkeyEnabled", "Hotkey", "Modifiers" })
                    EditorGUILayout.PropertyField(data.FindProperty(field));
                if (data.ApplyModifiedProperties()) CaptureShortcutSettings.Apply();
                if (GUILayout.Button("Capture")) CaptureWithSavedSettings();
                if (GUILayout.Button("Reveal Folder"))
                {
                    string folder = GetOutputFolder(config.OutputFolder);
                    Directory.CreateDirectory(folder);
                    EditorUtility.RevealInFinder(folder);
                }
                if (GUILayout.Button("Project Tool Settings"))
                    SettingsService.OpenProjectSettings("Project/Bomome/Editor Tools");
                return;
            }
            if (GUILayout.Button("Assign A Settings Asset..."))
                SettingsService.OpenProjectSettings("Project/Bomome/Editor Tools");
            EditorGUILayout.LabelField("Game Screenshot", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Captures the current Game view frame. PNG is written directly; JPG is captured as a temporary PNG and converted after Unity writes the frame.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(80f)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Screenshot Output Folder", outputFolder, string.Empty);
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        outputFolder = selected;
                        SaveSettings();
                    }
                }
            }

            outputFormat = (ScreenshotFormat)EditorGUILayout.EnumPopup("Format", outputFormat);
            scale = EditorGUILayout.IntSlider("Scale", scale, 1, 4);

            using (new EditorGUI.DisabledScope(outputFormat != ScreenshotFormat.Jpg))
            {
                jpegQuality = EditorGUILayout.IntSlider("JPG Quality", jpegQuality, 1, 100);
            }

            fileNameFormat = EditorGUILayout.TextField("File Name", fileNameFormat);
            EditorGUILayout.LabelField("Preview", CreateOutputPath(DateTime.Now, outputFormat));

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Hotkey", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Default", "Ctrl/Cmd + F12");
                EditorGUILayout.LabelField("Shortcut Id", ShortcutId);
                if (GUILayout.Button("Open Unity Shortcuts"))
                {
                    if (!EditorApplication.ExecuteMenuItem("Edit/Shortcuts..."))
                    {
                        EditorUtility.DisplayDialog("Shortcuts", "Open Edit > Shortcuts and search for Bomome/Capture Game Screenshot.", "OK");
                    }
                }
            }

            EditorGUILayout.Space(12f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture", GUILayout.Height(34f)))
                {
                    SaveSettings();
                    Capture(outputFolder, outputFormat, scale, jpegQuality, fileNameFormat);
                }

                if (GUILayout.Button("Reveal Folder", GUILayout.Height(34f), GUILayout.Width(120f)))
                {
                    Directory.CreateDirectory(GetOutputFolder(outputFolder));
                    EditorUtility.RevealInFinder(GetOutputFolder(outputFolder));
                }
            }
        }

        private void LoadSettings()
        {
            outputFolder = EditorPrefs.GetString(OutputFolderPrefKey, GetDefaultOutputFolder());
            outputFormat = (ScreenshotFormat)Mathf.Clamp(
                EditorPrefs.GetInt(FormatPrefKey, (int)ScreenshotFormat.Png),
                0,
                Enum.GetValues(typeof(ScreenshotFormat)).Length - 1);
            scale = Mathf.Clamp(EditorPrefs.GetInt(ScalePrefKey, 1), 1, 4);
            jpegQuality = Mathf.Clamp(EditorPrefs.GetInt(JpegQualityPrefKey, 92), 1, 100);
            fileNameFormat = EditorPrefs.GetString(FileNameFormatPrefKey, DefaultFileNameFormat);
        }

        private void SaveSettings()
        {
            EditorPrefs.SetString(OutputFolderPrefKey, GetOutputFolder(outputFolder));
            EditorPrefs.SetInt(FormatPrefKey, (int)outputFormat);
            EditorPrefs.SetInt(ScalePrefKey, Mathf.Clamp(scale, 1, 4));
            EditorPrefs.SetInt(JpegQualityPrefKey, Mathf.Clamp(jpegQuality, 1, 100));
            EditorPrefs.SetString(FileNameFormatPrefKey, string.IsNullOrWhiteSpace(fileNameFormat) ? DefaultFileNameFormat : fileNameFormat);
        }

        private static void CaptureWithSavedSettings()
        {
            var config = EditorToolsProjectSettings.instance.Capture;
            if (config != null)
            {
                Capture(config.OutputFolder, (ScreenshotFormat)config.Format,
                    Mathf.Clamp(config.Scale, 1, 4), Mathf.Clamp(config.JpegQuality, 1, 100), config.FileNameFormat);
                return;
            }
            string folder = EditorPrefs.GetString(OutputFolderPrefKey, GetDefaultOutputFolder());
            ScreenshotFormat format = (ScreenshotFormat)Mathf.Clamp(
                EditorPrefs.GetInt(FormatPrefKey, (int)ScreenshotFormat.Png),
                0,
                Enum.GetValues(typeof(ScreenshotFormat)).Length - 1);
            int savedScale = Mathf.Clamp(EditorPrefs.GetInt(ScalePrefKey, 1), 1, 4);
            int savedQuality = Mathf.Clamp(EditorPrefs.GetInt(JpegQualityPrefKey, 92), 1, 100);
            string savedFormat = EditorPrefs.GetString(FileNameFormatPrefKey, DefaultFileNameFormat);
            Capture(folder, format, savedScale, savedQuality, savedFormat);
        }

        private static void Capture(
            string folder,
            ScreenshotFormat format,
            int captureScale,
            int quality,
            string nameFormat)
        {
            string outputPath = CreateOutputPath(DateTime.Now, format, folder, nameFormat);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? GetDefaultOutputFolder());

            if (format == ScreenshotFormat.Png)
            {
                ScreenCapture.CaptureScreenshot(outputPath, Mathf.Clamp(captureScale, 1, 4));
                Debug.Log($"Game screenshot capture requested: {outputPath}");
                return;
            }

            string temporaryPngPath = Path.ChangeExtension(outputPath, ".capture_tmp." + Guid.NewGuid().ToString("N") + ".png");
            ScreenCapture.CaptureScreenshot(temporaryPngPath, Mathf.Clamp(captureScale, 1, 4));
            PendingJpgConversion.WaitForFile(temporaryPngPath, outputPath, Mathf.Clamp(quality, 1, 100));
            Debug.Log($"Game screenshot JPG capture requested: {outputPath}");
        }

        private string CreateOutputPath(DateTime timestamp, ScreenshotFormat format)
        {
            return CreateOutputPath(timestamp, format, outputFolder, fileNameFormat);
        }

        private static string CreateOutputPath(DateTime timestamp, ScreenshotFormat format, string folder, string nameFormat)
        {
            string cleanName = SanitizeFileName(FormatFileName(timestamp, nameFormat));
            string extension = format == ScreenshotFormat.Jpg ? ".jpg" : ".png";
            return Path.Combine(GetOutputFolder(folder), cleanName + extension);
        }

        private static string FormatFileName(DateTime timestamp, string nameFormat)
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            string template = string.IsNullOrWhiteSpace(nameFormat) ? DefaultFileNameFormat : nameFormat;
            return template
                .Replace("{product}", string.IsNullOrWhiteSpace(PlayerSettings.productName) ? "Game" : PlayerSettings.productName)
                .Replace("{scene}", string.IsNullOrWhiteSpace(activeSceneName) ? "NoScene" : activeSceneName)
                .Replace("{yyyyMMdd_HHmmss}", timestamp.ToString("yyyyMMdd_HHmmss"))
                .Replace("{date}", timestamp.ToString("yyyyMMdd"))
                .Replace("{time}", timestamp.ToString("HHmmss"));
        }

        private static string SanitizeFileName(string value)
        {
            string clean = string.IsNullOrWhiteSpace(value) ? DefaultFileNameFormat : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                clean = clean.Replace(invalid, '_');
            }

            return clean;
        }

        private static string GetOutputFolder(string folder)
        {
            return EditorToolsProjectSettings.ResolveOutputFolder(folder, GetDefaultOutputFolder());
        }

        private static string GetDefaultOutputFolder()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Captures", "Screenshots");
        }

        private sealed class PendingJpgConversion
        {
            private readonly string temporaryPngPath;
            private readonly string jpgPath;
            private readonly int quality;
            private readonly double expiresAt;

            private PendingJpgConversion(string temporaryPngPath, string jpgPath, int quality)
            {
                this.temporaryPngPath = temporaryPngPath;
                this.jpgPath = jpgPath;
                this.quality = quality;
                expiresAt = EditorApplication.timeSinceStartup + 5d;
            }

            public static void WaitForFile(string temporaryPngPath, string jpgPath, int quality)
            {
                PendingJpgConversion conversion = new PendingJpgConversion(temporaryPngPath, jpgPath, quality);
                EditorApplication.update += conversion.Tick;
            }

            private void Tick()
            {
                if (File.Exists(temporaryPngPath))
                {
                    try
                    {
                        if (TryConvert())
                        {
                            EditorApplication.update -= Tick;
                            return;
                        }
                    }
                    catch (IOException) { /* Unity may still be writing the capture. */ }
                    catch (Exception exception)
                    {
                        EditorApplication.update -= Tick;
                        Debug.LogException(exception);
                        return;
                    }
                }

                if (EditorApplication.timeSinceStartup > expiresAt)
                {
                    EditorApplication.update -= Tick;
                    Debug.LogWarning($"Timed out waiting for screenshot capture: {temporaryPngPath}");
                }
            }

            private bool TryConvert()
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!texture.LoadImage(File.ReadAllBytes(temporaryPngPath)))
                    {
                        return false;
                    }

                    File.WriteAllBytes(jpgPath, texture.EncodeToJPG(quality));
                    File.Delete(temporaryPngPath);
                    AssetDatabase.Refresh();
                    Debug.Log($"Game screenshot saved: {jpgPath}");
                    return true;
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
