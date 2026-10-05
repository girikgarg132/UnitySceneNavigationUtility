using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GirikGarg.SceneNavigationUtility
{
    internal static class NavigationSceneOpener
    {
        private const string MissingSceneWarningPrefix = "Navigation Utility: the scene is missing or is not a SceneAsset: ";
        private const string FailedSceneWarningPrefix = "Navigation Utility: could not open scene: ";

        /// <summary>
        /// Gets whether scene navigation is safe in the current editor state.
        /// </summary>
        internal static bool CanNavigate => !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling
            && !EditorApplication.isUpdating;

        /// <summary>
        /// Opens a scene, honoring save cancellation for single mode and retaining loaded scenes in additive mode.
        /// An already loaded scene is activated without unloading other scenes.
        /// </summary>
        internal static bool Open(string scenePath, bool additive = false)
        {
            if (!CanNavigate)
            {
                return false;
            }

            string validatedScenePath = GetValidScenePath(scenePath);
            if (string.IsNullOrEmpty(validatedScenePath))
            {
                Debug.LogWarning(MissingSceneWarningPrefix + scenePath);
                return false;
            }

            Scene openedScene;
            try
            {
                openedScene = SceneManager.GetSceneByPath(validatedScenePath);
                if (!openedScene.IsValid() || !openedScene.isLoaded)
                {
                    // Activating an already-loaded scene never unloads work and needs no save prompt.
                    if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        return false;
                    }

                    // Dialogs may run callbacks, so revalidate the state and asset before opening.
                    if (!CanNavigate)
                    {
                        return false;
                    }

                    validatedScenePath = GetValidScenePath(validatedScenePath);
                    if (string.IsNullOrEmpty(validatedScenePath))
                    {
                        Debug.LogWarning(MissingSceneWarningPrefix + scenePath);
                        return false;
                    }

                    openedScene = SceneManager.GetSceneByPath(validatedScenePath);
                    if (!openedScene.IsValid() || !openedScene.isLoaded)
                    {
                        openedScene = EditorSceneManager.OpenScene(
                            validatedScenePath,
                            additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
                    }
                }

                if (!CanNavigate || !openedScene.IsValid() || !openedScene.isLoaded || !SceneManager.SetActiveScene(openedScene))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(FailedSceneWarningPrefix + validatedScenePath + "\n" + exception.Message);
                return false;
            }

            NavigationCatalog.RecordRecentScene(openedScene.path);
            return true;
        }

        /// <summary>
        /// Pings a valid scene asset in the Project window without opening or modifying scenes.
        /// </summary>
        internal static void Ping(string scenePath)
        {
            string validatedScenePath = GetValidScenePath(scenePath);
            if (string.IsNullOrEmpty(validatedScenePath))
            {
                Debug.LogWarning(MissingSceneWarningPrefix + scenePath);
                return;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(validatedScenePath);
            if (sceneAsset != null)
            {
                EditorGUIUtility.PingObject(sceneAsset);
            }
        }

        private static string GetValidScenePath(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return string.Empty;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath.Replace('\\', '/'));
            return sceneAsset == null ? string.Empty : AssetDatabase.GetAssetPath(sceneAsset);
        }
    }
}
