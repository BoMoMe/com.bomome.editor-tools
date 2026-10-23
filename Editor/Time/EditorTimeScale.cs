using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Bomome.EditorTools.TimeControls
{
    [InitializeOnLoad]
    internal static class EditorTimeScale
    {
        internal const float Minimum = 0f;
        internal const float Maximum = 5f;
        private const string ChangedKey = "Bomome.EditorTools.TimeScale.Changed";
        private const double RefreshIntervalSeconds = 0.1;
        private static double nextRefresh;
        private static float observedScale = float.NaN;
        private static bool observedPlaying;

        internal static event Action Changed;
        internal static bool CanChange => EditorApplication.isPlaying && !EditorApplication.isCompiling;
        internal static float Current => Time.timeScale;

        static EditorTimeScale()
        {
            EditorApplication.update += Observe;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        internal static float Normalize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, Minimum, Maximum);
        }

        internal static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture) + "x";
        }

        internal static void Set(float value)
        {
            if (!CanChange) return;
            SessionState.SetBool(ChangedKey, true);
            Time.timeScale = Normalize(value);
        }

        private static void Observe()
        {
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
            bool playing = CanChange;
            if (Current.Equals(observedScale) && playing == observedPlaying) return;
            observedScale = Current;
            observedPlaying = playing;
            Changed?.Invoke();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // SessionState survives script reloads and disabled domain reload.
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ChangedKey, false))
            {
                Time.timeScale = 1f;
                SessionState.EraseBool(ChangedKey);
            }
            nextRefresh = 0;
            Observe();
        }
    }
}
