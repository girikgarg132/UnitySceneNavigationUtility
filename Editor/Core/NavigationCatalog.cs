using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GirikGarg.SceneNavigationUtility
{
    internal static class NavigationCatalog
    {
        private const int MaximumRecentScenes = 12;
        private const string RecentSceneGuidsSuffix = "RecentSceneGuids";

        [Serializable]
        private sealed class RecentSceneState
        {
            public List<string> sceneGuids = new List<string>();
        }

        /// <summary>
        /// Gets valid, enabled build scenes in their build-settings order.
        /// </summary>
        internal static IReadOnlyList<string> GetBuildScenePaths()
        {
            List<string> scenePaths = new List<string>();
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes != null)
            {
                foreach (EditorBuildSettingsScene buildScene in buildScenes)
                {
                    if (buildScene != null && buildScene.enabled)
                    {
                        scenePaths.Add(buildScene.path);
                    }
                }
            }

            return FilterValidScenePaths(scenePaths);
        }

        /// <summary>
        /// Gets valid scenes from the selected custom list in their configured order.
        /// </summary>
        internal static IReadOnlyList<string> GetCustomScenePaths()
        {
            NavigationSceneList navigationSceneList = NavigationPreferences.NavigationSceneList;
            if (navigationSceneList == null)
            {
                return Array.Empty<string>();
            }

            List<string> scenePaths = new List<string>();
            foreach (SceneAsset sceneAsset in navigationSceneList.Scenes)
            {
                if (sceneAsset != null)
                {
                    scenePaths.Add(AssetDatabase.GetAssetPath(sceneAsset));
                }
            }

            return FilterValidScenePaths(scenePaths);
        }

        /// <summary>
        /// Gets valid recently opened scenes, most recent first, resolving asset moves by GUID.
        /// </summary>
        internal static IReadOnlyList<string> GetRecentScenePaths()
        {
            List<string> scenePaths = new List<string>();
            foreach (string sceneGuid in ReadRecentSceneGuids())
            {
                scenePaths.Add(AssetDatabase.GUIDToAssetPath(sceneGuid));
            }

            return FilterValidScenePaths(scenePaths);
        }

        /// <summary>
        /// Gets the first valid, enabled build scene, or an empty string when none exists.
        /// </summary>
        internal static string GetFirstBuildScenePath()
        {
            IReadOnlyList<string> scenePaths = GetBuildScenePaths();
            return scenePaths.Count > 0 ? scenePaths[0] : string.Empty;
        }

        /// <summary>
        /// Records a valid editor scene at the front of the bounded local history.
        /// </summary>
        internal static void RecordRecentScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            IReadOnlyList<string> validPaths = FilterValidScenePaths(new[] { path });
            if (validPaths.Count == 0)
            {
                return;
            }

            string sceneGuid = AssetDatabase.AssetPathToGUID(validPaths[0]);
            if (string.IsNullOrEmpty(sceneGuid))
            {
                return;
            }

            List<string> recentSceneGuids = ReadRecentSceneGuids();
            recentSceneGuids.RemoveAll(existingGuid => string.Equals(existingGuid, sceneGuid, StringComparison.OrdinalIgnoreCase));
            recentSceneGuids.Insert(0, sceneGuid);
            if (recentSceneGuids.Count > MaximumRecentScenes)
            {
                recentSceneGuids.RemoveRange(MaximumRecentScenes, recentSceneGuids.Count - MaximumRecentScenes);
            }

            WriteRecentSceneGuids(recentSceneGuids);
        }

        /// <summary>
        /// Clears this project's local recent-scene history.
        /// </summary>
        internal static void ClearRecentScenes()
        {
            string preferenceKey = NavigationPreferences.MakeProjectKey(RecentSceneGuidsSuffix);
            if (!EditorPrefs.HasKey(preferenceKey))
            {
                return;
            }

            EditorPrefs.DeleteKey(preferenceKey);
            NavigationPreferences.NotifyChanged();
        }

        /// <summary>
        /// Filters invalid scene paths and duplicates without changing the source ordering.
        /// </summary>
        internal static IReadOnlyList<string> FilterValidScenePaths(IEnumerable<string> scenePaths)
        {
            List<string> validScenePaths = new List<string>();
            if (scenePaths == null)
            {
                return validScenePaths;
            }

            HashSet<string> seenPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string scenePath in scenePaths)
            {
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath.Replace('\\', '/'));
                if (sceneAsset == null)
                {
                    continue;
                }

                string canonicalScenePath = AssetDatabase.GetAssetPath(sceneAsset);
                if (!string.IsNullOrEmpty(canonicalScenePath) && seenPaths.Add(canonicalScenePath))
                {
                    validScenePaths.Add(canonicalScenePath);
                }
            }

            return validScenePaths;
        }

        private static List<string> ReadRecentSceneGuids()
        {
            string preferenceKey = NavigationPreferences.MakeProjectKey(RecentSceneGuidsSuffix);
            string storedJson = EditorPrefs.GetString(preferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(storedJson))
            {
                ClearRecentScenes();
                return new List<string>();
            }

            RecentSceneState storedState;
            try
            {
                storedState = JsonUtility.FromJson<RecentSceneState>(storedJson);
            }
            catch (ArgumentException)
            {
                ClearRecentScenes();
                return new List<string>();
            }

            if (storedState == null || storedState.sceneGuids == null)
            {
                ClearRecentScenes();
                return new List<string>();
            }

            List<string> validSceneGuids = new List<string>();
            HashSet<string> seenGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string sceneGuid in storedState.sceneGuids)
            {
                if (string.IsNullOrWhiteSpace(sceneGuid) || !seenGuids.Add(sceneGuid))
                {
                    continue;
                }

                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    continue;
                }

                validSceneGuids.Add(sceneGuid);
                if (validSceneGuids.Count == MaximumRecentScenes)
                {
                    break;
                }
            }

            if (!HaveSameGuids(storedState.sceneGuids, validSceneGuids))
            {
                WriteRecentSceneGuids(validSceneGuids);
            }

            return validSceneGuids;
        }

        private static bool HaveSameGuids(List<string> firstGuids, List<string> secondGuids)
        {
            if (firstGuids.Count != secondGuids.Count)
            {
                return false;
            }

            for (int guidIndex = 0; guidIndex < firstGuids.Count; guidIndex++)
            {
                if (!string.Equals(firstGuids[guidIndex], secondGuids[guidIndex], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteRecentSceneGuids(List<string> sceneGuids)
        {
            if (sceneGuids.Count == 0)
            {
                ClearRecentScenes();
                return;
            }

            string preferenceKey = NavigationPreferences.MakeProjectKey(RecentSceneGuidsSuffix);
            string sceneGuidsJson = JsonUtility.ToJson(new RecentSceneState { sceneGuids = sceneGuids });
            if (string.Equals(EditorPrefs.GetString(preferenceKey, string.Empty), sceneGuidsJson, StringComparison.Ordinal))
            {
                return;
            }

            EditorPrefs.SetString(preferenceKey, sceneGuidsJson);
            NavigationPreferences.NotifyChanged();
        }
    }
}
