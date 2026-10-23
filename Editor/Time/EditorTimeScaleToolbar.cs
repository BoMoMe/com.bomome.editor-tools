#if UNITY_6000_3_OR_NEWER
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Bomome.EditorTools.TimeControls
{
    [InitializeOnLoad]
    internal static class EditorTimeScaleToolbar
    {
        private const string SliderPath = "Bomome/Time Scale/Slider";
        private const string ResetPath = "Bomome/Time Scale/Reset";
        private const string PresetsPath = "Bomome/Time Scale/Presets";
        private static readonly float[] Presets = { 0f, 0.25f, 0.5f, 1f, 1.5f, 2f, 3f, 4f, 5f };

        static EditorTimeScaleToolbar()
        {
            EditorTimeScale.Changed += Refresh;
        }

        [MainToolbarElement(SliderPath, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 1)]
        private static MainToolbarElement CreateSlider()
        {
            return new MainToolbarSlider(
                new MainToolbarContent("Time " + EditorTimeScale.Format(EditorTimeScale.Current),
                    "Play-mode time scale (0-5x). Unscaled timers and audio are unaffected."),
                EditorTimeScale.Normalize(EditorTimeScale.Current), EditorTimeScale.Minimum,
                EditorTimeScale.Maximum, EditorTimeScale.Set)
            { enabled = EditorTimeScale.CanChange };
        }

        [MainToolbarElement(ResetPath, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 2)]
        private static MainToolbarElement CreateReset()
        {
            return new MainToolbarButton(
                new MainToolbarContent(EditorGUIUtility.IconContent("Refresh").image as Texture2D,
                    "Reset time scale to 1x"), () => EditorTimeScale.Set(1f))
            { enabled = EditorTimeScale.CanChange };
        }

        [MainToolbarElement(PresetsPath, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 3)]
        private static MainToolbarElement CreatePresets()
        {
            return new MainToolbarDropdown(new MainToolbarContent("Speed", "Time scale presets"), ShowPresets)
            { enabled = EditorTimeScale.CanChange };
        }

        private static void ShowPresets(Rect buttonRect)
        {
            var menu = new GenericMenu();
            foreach (float preset in Presets)
            {
                float value = preset;
                menu.AddItem(new GUIContent(EditorTimeScale.Format(value)),
                    Mathf.Approximately(EditorTimeScale.Current, value), () => EditorTimeScale.Set(value));
            }
            menu.DropDown(buttonRect);
        }

        private static void Refresh()
        {
            MainToolbar.Refresh(SliderPath);
            MainToolbar.Refresh(ResetPath);
            MainToolbar.Refresh(PresetsPath);
        }
    }
}
#endif
