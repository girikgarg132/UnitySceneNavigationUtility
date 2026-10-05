using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GirikGarg.SceneNavigationUtility
{
    [InitializeOnLoad]
    internal static class NavigationSceneHistory
    {
        static NavigationSceneHistory()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RecordIfSafe(scene);
        }

        private static void OnSceneSaved(Scene scene)
        {
            RecordIfSafe(scene);
        }

        private static void RecordIfSafe(Scene scene)
        {
            if (NavigationSceneOpener.CanNavigate && scene.IsValid())
            {
                NavigationCatalog.RecordRecentScene(scene.path);
            }
        }
    }
}
