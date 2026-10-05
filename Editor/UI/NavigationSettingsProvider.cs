using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GirikGarg.SceneNavigationUtility
{
    internal static class NavigationSettingsProvider
    {
        private const float SectionSpacing = 12f;
        private const string StartLabel = "Start from first enabled build scene";
        private const string ReturnLabel = "Return to previous scene setup after play";
        private const string LocalStorageHelp = "These preferences are local to you and this project. They are stored in EditorPrefs, not in project files or version control.";
        private const string ListHelp = "Scene list contents are shareable assets. Only your selected list is stored locally. Create a list using Assets > Create > Girik Garg > Navigation > Scene List, then drag it here.";
        private const string ReturnHelp = "When enabled, Unity restores the complete original edit-mode scene setup. When disabled, the startup scene is opened alone after a successful play session, with an unsaved-change prompt if needed. This option applies only when this package sets the startup scene.";
        private const string ToolbarHelp = "On Unity 6.3+, right-click the main toolbar and enable Girik Garg/Scene Navigation if hidden. On every version, File > Open Build Scene and File > Open Scene Selection List sit beneath File > Open Recent Scene. Assign a key binding in Edit > Shortcuts under Navigation Utility.";

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(NavigationPreferences.SettingsPath, SettingsScope.User)
            {
                label = "Navigation Utility",
                guiHandler = DrawPreferences,
                keywords = new HashSet<string> { "scene", "navigation", "play", "startup", "build", "toolbar", "Girik", "return" }
            };
        }

        private static void DrawPreferences(string searchContext)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(LocalStorageHelp, MessageType.Info);
            EditorGUILayout.Space(SectionSpacing);
            EditorGUILayout.LabelField("Play Mode", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!NavigationSceneOpener.CanNavigate))
            {
                NavigationPreferences.StartFromFirstBuildScene = EditorGUILayout.ToggleLeft(
                    new GUIContent(StartLabel, "Use the first enabled, valid scene in the current build scene list, not the custom navigation list."),
                    NavigationPreferences.StartFromFirstBuildScene);

                using (new EditorGUI.DisabledScope(!NavigationPreferences.StartFromFirstBuildScene))
                {
                    NavigationPreferences.ReturnToPreviousScene = EditorGUILayout.ToggleLeft(
                        new GUIContent(ReturnLabel, ReturnHelp), NavigationPreferences.ReturnToPreviousScene);
                }
            }

            EditorGUILayout.HelpBox(ReturnHelp, MessageType.None);
            string startupPath = NavigationCatalog.GetFirstBuildScenePath();
            EditorGUILayout.LabelField("Startup Scene", string.IsNullOrEmpty(startupPath) ? "No enabled build scenes" : startupPath);
            if (NavigationPreferences.StartFromFirstBuildScene && string.IsNullOrEmpty(startupPath))
            {
                EditorGUILayout.HelpBox("Add an enabled scene to Build Settings / Build Profiles. Until then, Play Mode behaves normally.", MessageType.Warning);
            }

            if (NavigationPreferences.StartFromFirstBuildScene && EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != EnterPlayModeOptions.None)
            {
                EditorGUILayout.HelpBox("Scene reload is disabled. Startup-scene switching is skipped; your Enter Play Mode settings are never changed.", MessageType.Warning);
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode && EditorSceneManager.playModeStartScene != null)
            {
                EditorGUILayout.HelpBox("Another play-mode start-scene override is configured. Navigation Utility leaves that override in control.", MessageType.Info);
            }

            EditorGUILayout.Space(SectionSpacing);
            EditorGUILayout.LabelField("Custom Scene List", EditorStyles.boldLabel);
            NavigationPreferences.NavigationSceneList = (NavigationSceneList)EditorGUILayout.ObjectField(
                "Scene List Asset", NavigationPreferences.NavigationSceneList, typeof(NavigationSceneList), false);
            EditorGUILayout.HelpBox(ListHelp, MessageType.None);

            EditorGUILayout.Space(SectionSpacing);
            EditorGUILayout.LabelField("Navigation", EditorStyles.boldLabel);
            if (GUILayout.Button("Open Scene Menu"))
            {
                NavigationMenu.ShowContextMenu();
            }
            using (new EditorGUI.DisabledScope(NavigationCatalog.GetRecentScenePaths().Count == 0))
            {
                if (GUILayout.Button("Clear Local Recent Scenes"))
                {
                    NavigationCatalog.ClearRecentScenes();
                }
            }
            EditorGUILayout.HelpBox(ToolbarHelp, MessageType.None);
        }
    }
}
