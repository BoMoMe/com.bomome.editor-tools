# Changelog

## Unreleased
- Add Append Environment Suffix to Android build settings and the build window.
  All build entry points can preserve the exact Player Settings application ID,
  independently of development/profiler flags. Retain existing defaults.

## 0.2.0 - 2026-10-23
- Add native Unity 6.3+ Play-mode time-scale toolbar controls: 0-5x slider,
  live multiplier, reset and speed presets beside Play/Pause.
- Observe external time-scale changes and reset editor-owned changes on Play exit,
  including when domain reload is disabled. Never change fixedDeltaTime.

## 0.1.0
- Add assigned ScriptableObject settings for capture/hotkey, builds, reference
  scanning and Firebase destinations, with a project settings provider.
- Keep local credentials/CLI/Git actions outside shared settings assets.
- Extract APK/AAB builder, screenshot capture, reference explorer and optional
  Firebase CLI workflow from BattleTroops into an Editor-only assembly.
- Isolate game environment changes behind scoped build hooks.
- Add per-project preferences, neutral defaults and serialization migration metadata.
- Give JPG intermediates unique paths and retry writes not yet readable.
- Drain Git process stdout/stderr concurrently to avoid pipe deadlocks.
- Add non-destructive policy and integration tests.
