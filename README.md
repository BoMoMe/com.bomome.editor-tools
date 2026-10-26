# Bomome Editor Tools (Beta 0.1.0)

## Features
- Android APK builds: Development, Stage and Production environments, app-ID
  suffixes, version/build-code increment, IL2CPP configuration and enabled scenes.
- Store AAB command: forces production, release compilation, no development,
  profiler or auto-run flags; validates custom signing, IL2CPP and ARM64 settings.
- Screenshot capture: output folder, PNG/JPG, 1-4x capture scale, JPEG quality,
  filename template and configurable Ctrl/Cmd+F12 shortcut.
- Asset Reference Explorer: time-sliced reverse dependency scan, direct/indirect
  chains, build-scene/Resources/preloaded inclusion hints, ping and selection.
- Optional Firebase CLI distribution panel: environment destinations, group aliases,
  release notes, explicit upload confirmation, process cancellation and download link.

Everything is in Editor-only assemblies. No runtime code, analytics SDK, Firebase
SDK, Bomome service package, game assets or credentials are dependencies.

## Commands
- Bomome > Build > Android APK Build...
- Bomome > Build > Build Selected Android APK
- Bomome > Build > Build Store AAB
- Bomome > Tools > Game Screenshot...
- Bomome > Tools > Capture Game Screenshot
- Tools > Bomome > Asset Reference Explorer
- Assets > Bomome > Explore References

Unity Shortcuts identifies the capture shortcut as Bomome/Capture Game Screenshot.
Screenshot templates accept {product}, {scene}, {yyyyMMdd_HHmmss}, {date}, {time}.
Capture requires a rendered Game view; it is not a headless camera-render exporter.
JPG uses a uniquely named intermediate PNG and waits for it before conversion.
An interrupted editor/reload can leave an intermediate PNG; no background service
persists capture requests across domain reload.

## Configuration and Safety
### Settings Assets
Open Project Settings > Bomome > Editor Tools (or Bomome > Tools > Editor Tool
Settings). Create/assign ScreenshotCaptureSettings, AndroidBuildToolSettings,
AssetReferenceExplorerSettings and FirebaseDistributionToolSettings. Assigned
assets are authoritative; tools read them even when their windows are closed.
Keep assets under an Editor folder, normally Assets/_GameAssets/Editor/Settings
in the consumers. The generic creation button defaults to Assets/Editor/BomomeTools.
Existing assets are reused, never overwritten. Initial creation imports previous
project-local build/capture preferences and existing Firebase destinations.

Capture settings include folder, file format, scale, JPEG quality, filename and
hotkey key/modifiers/enabled state. Relative output folders resolve from the
project root; absolute folders are allowed but machine-specific. The capture
window edits the same asset rather than maintaining another active copy.

The capture binding is applied through Unity Shortcut Manager in a project-specific
profile. Other bindings are copied from the active user profile; the original
profile is not edited. Inspector changes apply the binding, and Project Settings
has an explicit Apply button. The previous profile is restored on editor quit.
Shortcut profiles are local Unity preferences, not assets shipped in the player.
Selecting a different Unity shortcut profile manually requires applying the
configured capture hotkey again. Use Unity Shortcuts to resolve binding conflicts.

Build assets configure environment, output, version increment, development/profiler/
auto-run flags, IL2CPP mode and Firebase panel visibility. Production/store safety
overrides still win. Git commit/tag choices, message/tag templates, CLI paths,
release notes and credentials deliberately remain local, not shared asset settings.
Reference settings configure the scan budget and whether packages are included.
Firebase settings hold only project app IDs and groups, never login credentials.

### Unassigned Fallback
Without assigned assets, build/capture choices use EditorPrefs scoped by Application.dataPath. A fresh
project starts with no inherited output paths, no Commit And Tag, and the Firebase
panel disabled. Output defaults are Builds/Android and Captures/Screenshots.

Enabling Firebase Distribution in the build window reveals its optional workflow.
Destinations/groups use the assigned FirebaseDistributionToolSettings asset;
without it, ProjectSettings/FirebaseDistributionSettings.asset remains the legacy
fallback. CLI location and notes remain local EditorPrefs. Configure destinations
yourself; the package ships none.
Authenticate outside Unity with firebase login. Choose firebase.exe or the npm
firebase-tools/lib/bin/firebase.js entry point (Node on PATH), not shell wrappers.
The upload workflow is currently APK-only and never uploads to Google Play.
No command uploads, starts a CLI process or builds just by opening its window.

Custom keystore configuration remains in Unity Publishing Settings. Do not commit
keystore files or passwords. Dev/Stage append _dev/_stage; production removes them.
Existing game product names and identifiers are used; generic fallbacks contain
no BattleTroops branding.

Version/build numbers increment before the build attempt and remain afterward,
matching the original tool. Temporary identifier, build flags, bundle/export flags,
IL2CPP arguments and registered project state are restored in finally blocks.
The active Android build target is not switched back automatically.

Commit And Tag is an explicit opt-in after a successful build. It stages the
project repository's current changes, commits and tags; it never pushes. Review
your working tree first. Store quick-build does not opt into Git actions.

## Application ID Suffix
Use Append Environment Suffix in the Android build window or its assigned Android
Build Settings asset. Disable it to use the exact Android application ID in Player
Settings for every environment. This also applies to quick APK and store AAB builds.
It does not disable Development Build or profiler flags, and it does not remove a
suffix already entered manually in Player Settings. With no assigned asset, this
choice is saved in project-scoped editor preferences. Existing behavior remains
enabled by default for compatibility.

## Project Build Adapters
Register project-specific temporary changes with AndroidBuildHooks.Register,
using a stable unique ID. The callback accepts BuildEnvironment and returns an
IDisposable that restores exactly the assets/state it changed. Hooks run in
ordinal ID order and restore in reverse order, even when a later hook fails.
A callback that fails before returning must restore its own partial work.
Registrations are Editor-domain scoped, not persistent game services.

BattleTroops uses this for its GameAnalytics/AppsFlyer environment assets and
migrates its legacy preferences in game-owned editor code. The package does not
search for SDK configuration assets. ProtoFactory needs no adapter by default.

## Migration, Validation and Limits
Original tool script GUIDs, namespaces, menu paths and shortcut ID are retained.
MovedFrom attributes record the old Assembly-CSharp-Editor assembly for Unity
windows/settings. Remove old tool source files when installing this package.
Legacy Firebase settings retain their original serialized project path and script
GUID; creating assigned assets copies existing destinations without deleting that fallback.

Tests cover policy flags, store overrides, version/ID transforms, adapter rollback,
capture naming/conversion, CLI argument validation and dependency graph traversal.
Tests do not run builds, uploads, login, Git commits or tags. Device/build/signing
and Firebase distribution checks require explicit project-specific validation.
Dependency graph hints are not an authoritative APK/AAB build-content report.

## Play-Mode Time Scale
Unity 6.3+ automatically adds three native toolbar elements immediately after
Play/Pause: a 0-5x slider with the actual multiplier, reset to 1x and a speed
preset menu (0, 0.25, 0.5, 1, 1.5, 2, 3, 4 and 5x). Controls are disabled outside
Play mode. Use the toolbar customization menu to show/reorder them if an existing
editor layout hides them. No scene component or runtime dependency is required.

The label observes actual Time.timeScale, including changes made by game pause
or slow-motion systems; this tool does not continuously override game state.
Changing the slider deliberately changes Time.timeScale. At 0x scaled simulation
stops; Unity's Pause button is independent. Unscaled timers and audio are unaffected.
Time.fixedDeltaTime is never changed. After using the controls, leaving Play mode
resets time scale to 1x, including with domain reload disabled.

The package is published at https://github.com/BoMoMe/com.bomome.editor-tools.git.
Existing consumers should update their manifest Git revision to a published commit
containing this feature and let Package Manager resolve it. Pinned older commits
do not receive new tools automatically. Do not copy scripts into consumer projects
or edit Library/PackageCache. The package currently requires Unity 6000.4.
