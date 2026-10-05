using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GirikGarg.SceneNavigationUtility
{
    internal static class NavigationMenu
    {
        private const string ShortcutId = "Scene Navigation Utility/Open Scene Menu";
        private const string FolderSeparator = " > ";
        private const string BuildGroup = "Build Scenes";
        private const string CustomGroup = "Custom Scene List";
        private const string RecentGroup = "Recent Scenes";
        private const string AdditiveGroup = "Open Additively/";
        private const string PingGroup = "Ping in Project/";
        private const string EmptyGroupLabel = "/(No scenes)";

        private enum SceneAction
        {
            Open,
            Additive,
            Ping
        }

        [Shortcut(ShortcutId)]
        private static void OpenFromShortcut()
        {
            ShowContextMenu();
        }

        /// <summary>Opens the shared scene menu at the current pointer position.</summary>
        internal static void ShowContextMenu()
        {
            CreateMenu().ShowAsContext();
        }

        /// <summary>Opens the shared scene menu below the main-toolbar button.</summary>
        internal static void ShowDropdown(Rect anchor)
        {
            CreateMenu().DropDown(anchor);
        }

        /// <summary>
        /// Creates a folder-qualified menu label, such as "Main  [Assets > Scenes]", so identically named scenes stay distinct.
        /// </summary>
        internal static string CreateSceneLabel(string scenePath)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            string directory = (Path.GetDirectoryName(scenePath) ?? string.Empty).Replace('\\', '/').Replace("/", FolderSeparator);
            return sceneName + "  [" + directory + "]";
        }

        private static GenericMenu CreateMenu()
        {
            GenericMenu menu = new GenericMenu();
            IReadOnlyList<string> buildScenes = NavigationCatalog.GetBuildScenePaths();
            IReadOnlyList<string> customScenes = NavigationCatalog.GetCustomScenePaths();
            IReadOnlyList<string> recentScenes = NavigationCatalog.GetRecentScenePaths();
            bool canNavigate = NavigationSceneOpener.CanNavigate;
            string activeScenePath = SceneManager.GetActiveScene().path;

            if (!canNavigate)
            {
                menu.AddDisabledItem(new GUIContent("Scene opening is unavailable during Play Mode or compilation"));
                menu.AddSeparator(string.Empty);
            }

            AddSceneGroup(menu, BuildGroup, buildScenes, SceneAction.Open, canNavigate, activeScenePath);
            AddSceneGroup(menu, CustomGroup, customScenes, SceneAction.Open, canNavigate, activeScenePath);
            AddSceneGroup(menu, RecentGroup, recentScenes, SceneAction.Open, canNavigate, activeScenePath);
            menu.AddSeparator(string.Empty);
            AddSceneGroup(menu, AdditiveGroup + BuildGroup, buildScenes, SceneAction.Additive, canNavigate, activeScenePath);
            AddSceneGroup(menu, AdditiveGroup + CustomGroup, customScenes, SceneAction.Additive, canNavigate, activeScenePath);
            AddSceneGroup(menu, AdditiveGroup + RecentGroup, recentScenes, SceneAction.Additive, canNavigate, activeScenePath);
            AddSceneGroup(menu, PingGroup + BuildGroup, buildScenes, SceneAction.Ping, true, activeScenePath);
            AddSceneGroup(menu, PingGroup + CustomGroup, customScenes, SceneAction.Ping, true, activeScenePath);
            AddSceneGroup(menu, PingGroup + RecentGroup, recentScenes, SceneAction.Ping, true, activeScenePath);
            menu.AddSeparator(string.Empty);

            NavigationSceneList navigationSceneList = NavigationPreferences.NavigationSceneList;
            if (navigationSceneList != null)
            {
                menu.AddItem(new GUIContent("Select Scene List Asset"), false, () =>
                {
                    Selection.activeObject = navigationSceneList;
                    EditorGUIUtility.PingObject(navigationSceneList);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("No custom scene list selected"));
            }

            if (recentScenes.Count > 0)
            {
                menu.AddItem(new GUIContent("Clear Recent Scenes"), false, NavigationCatalog.ClearRecentScenes);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Clear Recent Scenes"));
            }

            menu.AddItem(new GUIContent("Preferences..."), false, () => SettingsService.OpenUserPreferences(NavigationPreferences.SettingsPath));
            return menu;
        }

        private static void AddSceneGroup(GenericMenu menu, string group, IReadOnlyList<string> scenePaths,
            SceneAction action, bool enabled, string activeScenePath)
        {
            if (scenePaths.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent(group + EmptyGroupLabel));
                return;
            }

            foreach (string scenePath in scenePaths)
            {
                string capturedPath = scenePath;
                GUIContent label = new GUIContent(group + "/" + CreateSceneLabel(capturedPath), capturedPath);
                bool isActive = capturedPath == activeScenePath;
                if (!enabled)
                {
                    menu.AddDisabledItem(label, isActive);
                    continue;
                }

                menu.AddItem(label, isActive, () =>
                {
                    if (action == SceneAction.Ping)
                    {
                        NavigationSceneOpener.Ping(capturedPath);
                    }
                    else
                    {
                        NavigationSceneOpener.Open(capturedPath, action == SceneAction.Additive);
                    }
                });
            }
        }
    }
}
