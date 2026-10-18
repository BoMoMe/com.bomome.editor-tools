# Changelog

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
