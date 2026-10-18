using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Bomome.EditorTools
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Bomome.EditorTools", sourceAssembly: "Assembly-CSharp-Editor")]
    public sealed class AssetReferenceExplorerWindow : EditorWindow
    {
        private Object target;
        private string targetPath;
        private string[] paths;
        private int scanIndex;
        private bool scanning;
        private bool completed;
        private string filter = "";
        private Vector2 scroll;
        private readonly Dictionary<string, List<string>> referrers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> nextHop = new(StringComparer.Ordinal);
        private readonly List<string> results = new();
        private readonly HashSet<string> buildScenes = new(StringComparer.Ordinal);
        private double scanBudgetSeconds = 0.008;

        [MenuItem("Tools/Bomome/Asset Reference Explorer")]
        private static void Open()
        {
            var window = GetWindow<AssetReferenceExplorerWindow>("Asset References");
            window.minSize = new Vector2(600, 350);
            if (Selection.activeObject != null && AssetDatabase.Contains(Selection.activeObject))
                window.target = Selection.activeObject;
        }

        [MenuItem("Assets/Bomome/Explore References", false, 2000)]
        private static void OpenSelected() => Open();

        [MenuItem("Assets/Bomome/Explore References", true)]
        private static bool ValidateSelection() => Selection.activeObject != null && AssetDatabase.Contains(Selection.activeObject);

        private void OnDisable() => StopScan();

        private void OnGUI()
        {
            if (GUILayout.Button("Project Tool Settings"))
                SettingsService.OpenProjectSettings("Project/Bomome/Editor Tools");
            EditorGUI.BeginChangeCheck();
            target = EditorGUILayout.ObjectField("Asset", target, typeof(Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                StopScan();
                completed = false;
                results.Clear();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(target == null || scanning))
                    if (GUILayout.Button("Scan References")) StartScan();
                if (scanning && GUILayout.Button("Cancel", GUILayout.Width(80))) StopScan();
            }
            if (scanning)
            {
                EditorGUILayout.LabelField($"Scanning {scanIndex:N0} / {paths.Length:N0} assets");
                return;
            }
            if (!completed) return;

            EditorGUILayout.HelpBox("References are checked at asset-file level (all sub-assets included). Build paths are potential inclusion paths, not proof of what a particular APK contains. Addressables/AssetBundles, runtime string loads and custom build processors may add other assets. Sprite atlas packing can also include sprites without a scene reference.", MessageType.Info);
            DrawRootReason(targetPath);
            EditorGUILayout.LabelField($"{results.Count} referencing assets", EditorStyles.boldLabel);
            filter = EditorGUILayout.TextField("Filter", filter);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string path in results)
            {
                if (filter.Length > 0 && path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool direct = nextHop[path] == targetPath;
                    EditorGUILayout.LabelField(direct ? "Direct reference" : "Indirect reference", EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(path, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    DrawRootReason(path);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Ping", GUILayout.Width(60))) EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
                        if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
                        if (GUILayout.Button("Open", GUILayout.Width(60))) AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(path));
                        if (GUILayout.Button("Copy Chain", GUILayout.Width(100))) EditorGUIUtility.systemCopyBuffer = GetChain(path);
                    }
                    if (!direct) EditorGUILayout.LabelField(GetChain(path), EditorStyles.wordWrappedMiniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRootReason(string path)
        {
            if (buildScenes.Contains(path)) EditorGUILayout.LabelField("Build root: enabled build scene", EditorStyles.boldLabel);
            if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
                EditorGUILayout.LabelField("Build root candidate: Resources (editor-only assets excluded)");
            if (path.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
                EditorGUILayout.LabelField("Build root: StreamingAssets (copied into player)");
            if (path.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase))
                EditorGUILayout.LabelField("Sprite atlas: inspect Include in Build and packables");
        }

        private void StartScan()
        {
            targetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(targetPath) || AssetDatabase.IsValidFolder(targetPath))
            {
                ShowNotification(new GUIContent("Select an asset file, not a folder."));
                return;
            }
            referrers.Clear();
            nextHop.Clear();
            results.Clear();
            buildScenes.Clear();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) buildScenes.Add(scene.path);
            var config = EditorToolsProjectSettings.instance.References;
            scanBudgetSeconds = config != null ? Mathf.Clamp(config.ScanMillisecondsPerTick, 1, 50) / 1000d : 0.008;
            bool includePackages = config == null || config.IncludePackages;
            paths = AssetDatabase.GetAllAssetPaths().Where(p =>
                (p.StartsWith("Assets/", StringComparison.Ordinal) || (includePackages && p.StartsWith("Packages/", StringComparison.Ordinal))) &&
                !AssetDatabase.IsValidFolder(p)).ToArray();
            scanIndex = 0;
            completed = false;
            scanning = true;
            EditorApplication.update += ScanTick;
        }

        private void StopScan()
        {
            EditorApplication.update -= ScanTick;
            scanning = false;
        }

        private void ScanTick()
        {
            // Limit editor work per tick; dependency imports may still take longer individually.
            double deadline = EditorApplication.timeSinceStartup + scanBudgetSeconds;
            try
            {
                while (scanIndex < paths.Length && EditorApplication.timeSinceStartup < deadline)
                {
                    string path = paths[scanIndex++];
                    foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                    {
                        if (dependency == path) continue;
                        if (!referrers.TryGetValue(dependency, out var list))
                            referrers.Add(dependency, list = new List<string>());
                        list.Add(path);
                    }
                }
                if (scanIndex == paths.Length)
                {
                    StopScan();
                    ResolveResults();
                    completed = true;
                }
            }
            catch (Exception exception)
            {
                StopScan();
                Debug.LogException(exception);
                ShowNotification(new GUIContent("Scan failed. See Console."));
            }
            Repaint();
        }

        private void ResolveResults()
        {
            var queue = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal) { targetPath };
            queue.Enqueue(targetPath);
            while (queue.Count > 0)
            {
                string dependency = queue.Dequeue();
                if (!referrers.TryGetValue(dependency, out var parents)) continue;
                foreach (string parent in parents)
                {
                    if (!visited.Add(parent)) continue;
                    nextHop.Add(parent, dependency);
                    results.Add(parent);
                    queue.Enqueue(parent);
                }
            }
            results.Sort((a, b) =>
            {
                int rootOrder = buildScenes.Contains(b).CompareTo(buildScenes.Contains(a));
                if (rootOrder != 0) return rootOrder;
                int directOrder = (nextHop[b] == targetPath).CompareTo(nextHop[a] == targetPath);
                return directOrder != 0 ? directOrder : string.Compare(a, b, StringComparison.Ordinal);
            });
        }

        private string GetChain(string path)
        {
            var chain = new List<string> { path };
            while (nextHop.TryGetValue(path, out string dependency))
            {
                chain.Add(dependency);
                path = dependency;
            }
            return string.Join(" -> ", chain);
        }
    }
}
