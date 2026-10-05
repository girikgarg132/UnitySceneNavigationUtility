#if UNITY_6000_3_OR_NEWER
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GirikGarg.SceneNavigationUtility
{
    [InitializeOnLoad]
    internal static class NavigationToolbar
    {
        private const string ElementPath = "Girik Garg/Scene Navigation";
        private const string UntitledSceneLabel = "Untitled";
        private const string IconName = "SceneAsset Icon";
        private const string Tooltip = "Navigate build, custom, and recent scenes. Opening is disabled during Play Mode.";
        private static bool refreshQueued;

        static NavigationToolbar()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.sceneSaved += OnSceneClosed;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.projectChanged += QueueRefresh;
            NavigationPreferences.Changed += QueueRefresh;
        }

        /// <summary>Creates the optional native main-toolbar scene dropdown on Unity 6.3 or newer.</summary>
        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement CreateSceneDropdown()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = UntitledSceneLabel;
            }

            Texture2D icon = EditorGUIUtility.IconContent(IconName).image as Texture2D;
            MainToolbarContent content = new MainToolbarContent(sceneName, icon, Tooltip);
            return new MainToolbarDropdown(content, NavigationMenu.ShowDropdown);
        }

        private static void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            QueueRefresh();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            QueueRefresh();
        }

        private static void OnSceneClosed(Scene scene)
        {
            QueueRefresh();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            QueueRefresh();
        }

        private static void QueueRefresh()
        {
            if (refreshQueued)
            {
                return;
            }

            refreshQueued = true;
            EditorApplication.delayCall += Refresh;
        }

        private static void Refresh()
        {
            refreshQueued = false;
            MainToolbar.Refresh(ElementPath);
        }
    }
}
#endif
