using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Bomome.EditorTools
{
    public enum ScreenshotFileFormat { Png, Jpg }

    [CreateAssetMenu(menuName = "Bomome/Editor Tools/Screenshot Capture Settings")]
    public sealed class ScreenshotCaptureSettings : ScriptableObject
    {
        [Tooltip("Project-relative or absolute output path. Prefer a relative path for shared settings.")]
        public string OutputFolder = "Captures/Screenshots";
        public ScreenshotFileFormat Format;
        [Range(1, 4)] public int Scale = 1;
        [Range(1, 100)] public int JpegQuality = 92;
        public string FileNameFormat = "{product}_{scene}_{yyyyMMdd_HHmmss}";
        public bool HotkeyEnabled = true;
        public KeyCode Hotkey = KeyCode.F12;
        public ShortcutModifiers Modifiers = ShortcutModifiers.Action;
    }
}
