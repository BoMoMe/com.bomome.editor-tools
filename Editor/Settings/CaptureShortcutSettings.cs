using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Bomome.EditorTools
{
    [InitializeOnLoad]
    public static class CaptureShortcutSettings
    {
        internal const string ShortcutId = "Bomome/Capture Game Screenshot";
        private const string PreviousProfileKey = "Bomome.EditorTools.PreviousShortcutProfile";

        static CaptureShortcutSettings()
        {
            EditorApplication.delayCall += Apply;
            EditorApplication.quitting += RestorePreviousProfile;
        }

        internal static string ProfileId(string projectPath)
        {
            using (var hash = SHA256.Create())
                return "Bomome Capture " + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(projectPath)))
                    .Replace("-", "").Substring(0, 12);
        }

        public static void Apply()
        {
            var manager = ShortcutManager.instance;
            var settings = EditorToolsProjectSettings.instance.Capture;
            if (settings == null) { RestorePreviousProfile(); return; }
            if (!manager.GetAvailableShortcutIds().Contains(ShortcutId)) return;
            string id = ProfileId(Application.dataPath);
            if (manager.activeProfileId != id)
            {
                string previous = manager.activeProfileId;
                var bindings = manager.GetAvailableShortcutIds().ToDictionary(key => key, manager.GetShortcutBinding);
                if (!manager.GetAvailableProfileIds().Contains(id)) manager.CreateProfile(id);
                manager.activeProfileId = id;
                foreach (var binding in bindings)
                    if (binding.Key != ShortcutId && !manager.GetShortcutBinding(binding.Key).Equals(binding.Value))
                        manager.RebindShortcut(binding.Key, binding.Value);
                SessionState.SetString(PreviousProfileKey, previous);
            }
            var desired = Binding(settings);
            if (!manager.GetShortcutBinding(ShortcutId).Equals(desired)) manager.RebindShortcut(ShortcutId, desired);
        }

        internal static ShortcutBinding Binding(ScreenshotCaptureSettings settings) =>
            settings.HotkeyEnabled && settings.Hotkey != KeyCode.None
                ? new ShortcutBinding(new KeyCombination(settings.Hotkey, settings.Modifiers))
                : ShortcutBinding.empty;

        private static void RestorePreviousProfile()
        {
            var manager = ShortcutManager.instance;
            if (manager.activeProfileId != ProfileId(Application.dataPath)) return;
            string previous = SessionState.GetString(PreviousProfileKey, ShortcutManager.defaultProfileId);
            if (manager.GetAvailableProfileIds().Contains(previous)) manager.activeProfileId = previous;
        }
    }
}
