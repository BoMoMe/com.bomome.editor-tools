using UnityEditor;
using UnityEngine;

namespace Bomome.EditorTools
{
    internal static class EditorToolsSettingsProvider
    {
        [MenuItem("Bomome/Tools/Editor Tool Settings...")]
        private static void Open() => SettingsService.OpenProjectSettings("Project/Bomome/Editor Tools");

        [SettingsProvider]
        private static SettingsProvider CreateProvider() => new SettingsProvider("Project/Bomome/Editor Tools", SettingsScope.Project)
        {
            guiHandler = context =>
            {
                var settings = EditorToolsProjectSettings.instance;
                EditorGUILayout.HelpBox("Assigned assets are authoritative. Keep them in an Editor folder. Credentials, CLI paths and Git actions remain local.", MessageType.Info);
                var data = new SerializedObject(settings);
                data.Update();
                foreach (string name in new[] { "Capture", "AndroidBuild", "References", "Firebase" })
                    EditorGUILayout.PropertyField(data.FindProperty(name));
                if (data.ApplyModifiedProperties()) settings.SaveSettings();
                if (GUILayout.Button("Create And Assign Default Assets"))
                    EditorToolsProjectSettings.CreateDefaults();
                if (settings.Capture != null && GUILayout.Button("Edit Capture Settings"))
                    Selection.activeObject = settings.Capture;
                if (GUILayout.Button("Apply Capture Hotkey")) CaptureShortcutSettings.Apply();
            }
        };
    }

    [CustomEditor(typeof(ScreenshotCaptureSettings))]
    internal sealed class ScreenshotCaptureSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector()) CaptureShortcutSettings.Apply();
            if (EditorToolsProjectSettings.instance.Capture != target)
                EditorGUILayout.HelpBox("Assign this asset under Project Settings > Bomome > Editor Tools to use it.", MessageType.Warning);
            EditorGUILayout.HelpBox("The assigned capture asset controls a project-specific Unity shortcut profile. Your original profile is unchanged; use Unity Shortcuts to inspect conflicts.", MessageType.Info);
            if (GUILayout.Button("Apply Capture Hotkey")) CaptureShortcutSettings.Apply();
        }
    }
}
