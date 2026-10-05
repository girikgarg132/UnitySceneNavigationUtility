using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GirikGarg.SceneNavigationUtility
{
    /// <summary>
    /// Temporarily selects a play-mode startup scene without replacing the edit-mode scene setup.
    /// </summary>
    [InitializeOnLoad]
    internal static class NavigationPlayModeController
    {
        private const string SessionKeyPrefix = "PlayMode.";
        private const string ActiveSessionSuffix = SessionKeyPrefix + "Active";
        private const string TargetGuidSuffix = SessionKeyPrefix + "TargetGuid";
        private const string PreviousOverrideGuidSuffix = SessionKeyPrefix + "PreviousOverrideGuid";
        private const string ReturnToPreviousSceneSuffix = SessionKeyPrefix + "ReturnToPreviousScene";
        private const string EnteredPlayModeSuffix = SessionKeyPrefix + "EnteredPlayMode";
        private const string WarningPrefix = "[Navigation Utility] ";
        private const string ExternalOverrideWarning =
            "Start From First Build Scene was skipped because another tool has already configured a play-mode start scene. Its override was left unchanged.";
        private const string MissingBuildSceneWarning =
            "Start From First Build Scene was skipped because there is no enabled, valid scene in Build Settings. Normal Play Mode behavior was left unchanged.";
        private const string DisabledSceneReloadWarning =
            "Start From First Build Scene was skipped because Enter Play Mode Options disables scene reload. The startup scene cannot be safely guaranteed; your editor settings and normal Play Mode behavior were left unchanged.";
        private const string MissingTargetWarning =
            "The configured startup scene no longer exists. The restored edit-mode scene setup was left unchanged.";
        private const string RestoreOverrideError = "Could not restore the temporary play-mode start-scene override";
        private const string StartOverrideError = "Could not configure the temporary play-mode start-scene override";
        private const string OpenSceneError = "Could not open the startup scene after Play Mode";
        private const string CallbackError = "Could not process a play-mode lifecycle callback";

        private static readonly string ActiveSessionKey = NavigationPreferences.MakeProjectKey(ActiveSessionSuffix);
        private static readonly string TargetGuidKey = NavigationPreferences.MakeProjectKey(TargetGuidSuffix);
        private static readonly string PreviousOverrideGuidKey = NavigationPreferences.MakeProjectKey(PreviousOverrideGuidSuffix);
        private static readonly string ReturnToPreviousSceneKey = NavigationPreferences.MakeProjectKey(ReturnToPreviousSceneSuffix);
        private static readonly string EnteredPlayModeKey = NavigationPreferences.MakeProjectKey(EnteredPlayModeSuffix);

        private static bool completionQueued;
        private static bool restoreErrorReported;

        static NavigationPlayModeController()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // SessionState survives domain reload, but is erased when the editor exits.
            // Do not reconstruct a session from assets or preferences on editor startup.
            if (HasActiveSession)
            {
                ObserveSession();
                QueueSessionCompletion();
            }
        }

        private static bool HasActiveSession => SessionState.GetBool(ActiveSessionKey, false);

        private static bool IsStableEditMode =>
            !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating;

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            try
            {
                switch (state)
                {
                    case PlayModeStateChange.ExitingEditMode:
                        BeginStartupOverride();
                        break;
                    case PlayModeStateChange.EnteredPlayMode:
                        if (HasActiveSession)
                        {
                            // Never infer successful entry merely from an attempted transition.
                            SessionState.SetBool(EnteredPlayModeKey, true);
                        }
                        break;
                    case PlayModeStateChange.EnteredEditMode:
                        if (HasActiveSession)
                        {
                            QueueSessionCompletion();
                        }
                        break;
                }
            }
            catch (Exception exception)
            {
                WarnException(CallbackError, exception);
            }
        }

        private static void BeginStartupOverride()
        {
            // A rapid second Play request can precede the previous deferred completion.
            // Release only our override; never open an edit scene during this transition.
            if (HasActiveSession)
            {
                if (!TryRestorePreviousOverride())
                {
                    return;
                }

                ClearSession();
            }

            if (!NavigationPreferences.StartFromFirstBuildScene)
            {
                return;
            }

            if (EditorSceneManager.playModeStartScene != null)
            {
                Debug.LogWarning(WarningPrefix + ExternalOverrideWarning);
                return;
            }

            if (EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != EnterPlayModeOptions.None)
            {
                Debug.LogWarning(WarningPrefix + DisabledSceneReloadWarning);
                return;
            }

            string targetPath = NavigationCatalog.GetFirstBuildScenePath();
            SceneAsset targetScene = string.IsNullOrEmpty(targetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath);
            string targetGuid = targetScene == null ? string.Empty : AssetDatabase.AssetPathToGUID(targetPath);
            if (targetScene == null || string.IsNullOrEmpty(targetGuid))
            {
                Debug.LogWarning(WarningPrefix + MissingBuildSceneWarning);
                return;
            }

            // Recheck after resolving the target; never replace an external non-null override.
            SceneAsset previousOverride = EditorSceneManager.playModeStartScene;
            if (previousOverride != null)
            {
                Debug.LogWarning(WarningPrefix + ExternalOverrideWarning);
                return;
            }

            try
            {
                string previousOverrideGuid = previousOverride == null
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(previousOverride));
                SessionState.SetString(PreviousOverrideGuidKey, previousOverrideGuid);
                SessionState.SetString(TargetGuidKey, targetGuid);
                SessionState.SetBool(ReturnToPreviousSceneKey, NavigationPreferences.ReturnToPreviousScene);
                SessionState.SetBool(EnteredPlayModeKey, false);
                SessionState.SetBool(ActiveSessionKey, true);
                ObserveSession();
                EditorSceneManager.playModeStartScene = targetScene;
            }
            catch (Exception exception)
            {
                WarnException(StartOverrideError, exception);
                if (HasActiveSession && TryRestorePreviousOverride())
                {
                    ClearSession();
                }
            }
        }

        private static void ObserveSession()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            // Canceled entry does not necessarily produce a complete state-change sequence.
            // This observer exists only for our session and releases it once edit mode is stable.
            if (HasActiveSession && IsStableEditMode)
            {
                QueueSessionCompletion();
            }
        }

        private static void QueueSessionCompletion()
        {
            if (completionQueued)
            {
                return;
            }

            completionQueued = true;
            EditorApplication.delayCall += CompleteSessionWhenStable;
        }

        private static void CompleteSessionWhenStable()
        {
            completionQueued = false;
            if (!HasActiveSession || !IsStableEditMode)
            {
                return;
            }

            bool shouldOpenStartupScene = SessionState.GetBool(EnteredPlayModeKey, false) &&
                !SessionState.GetBool(ReturnToPreviousSceneKey, true);
            string targetGuid = SessionState.GetString(TargetGuidKey, string.Empty);
            if (!TryRestorePreviousOverride())
            {
                // Keep ownership data so a transient setter failure can be retried safely.
                return;
            }

            ClearSession();

            // Unity restores its complete original edit-mode setup, including additive scenes.
            // Only an actually entered session with the captured return toggle off replaces it.
            if (shouldOpenStartupScene)
            {
                OpenStartupSceneAsSingle(targetGuid);
            }
        }

        private static bool TryRestorePreviousOverride()
        {
            try
            {
                SceneAsset currentOverride = EditorSceneManager.playModeStartScene;
                string ownedGuid = SessionState.GetString(TargetGuidKey, string.Empty);
                if (currentOverride == null || string.IsNullOrEmpty(ownedGuid))
                {
                    return true;
                }

                string currentGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(currentOverride));
                if (!string.Equals(currentGuid, ownedGuid, StringComparison.Ordinal))
                {
                    // Another tool changed the override after entry. Its current value wins.
                    return true;
                }

                string previousGuid = SessionState.GetString(PreviousOverrideGuidKey, string.Empty);
                SceneAsset previousOverride = string.IsNullOrEmpty(previousGuid)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(previousGuid));
                EditorSceneManager.playModeStartScene = previousOverride;
                restoreErrorReported = false;
                return true;
            }
            catch (Exception exception)
            {
                if (!restoreErrorReported)
                {
                    WarnException(RestoreOverrideError, exception);
                    restoreErrorReported = true;
                }

                return false;
            }
        }

        private static void OpenStartupSceneAsSingle(string targetGuid)
        {
            try
            {
                if (!IsStableEditMode)
                {
                    return;
                }

                string targetPath = ResolveScenePath(targetGuid);
                if (string.IsNullOrEmpty(targetPath))
                {
                    Debug.LogWarning(WarningPrefix + MissingTargetWarning);
                    return;
                }

                // Explicit confirmation protects all modified scenes, including unsaved scenes.
                // Cancel leaves Unity's restored setup untouched; Don't Save is an explicit choice.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !IsStableEditMode)
                {
                    return;
                }

                // The scene can be moved or deleted while playing or during the save dialog.
                targetPath = ResolveScenePath(targetGuid);
                if (string.IsNullOrEmpty(targetPath))
                {
                    Debug.LogWarning(WarningPrefix + MissingTargetWarning);
                    return;
                }

                if (!IsStableEditMode)
                {
                    return;
                }

                // Do not merely focus a scene that is already open: Single must close others.
                EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);
            }
            catch (Exception exception)
            {
                WarnException(OpenSceneError, exception);
            }
        }

        private static string ResolveScenePath(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
            {
                return string.Empty;
            }

            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            return !string.IsNullOrEmpty(scenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null
                ? scenePath
                : string.Empty;
        }

        private static void OnBeforeAssemblyReload()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.delayCall -= CompleteSessionWhenStable;
            completionQueued = false;

            // A reload while playing/entering preserves the snapshot for the next domain.
            // A canceled entry recompiling in edit mode needs only override cleanup, not a scene open.
            if (HasActiveSession && !SessionState.GetBool(EnteredPlayModeKey, false) &&
                !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode &&
                TryRestorePreviousOverride())
            {
                ClearSession();
            }
        }

        private static void OnEditorQuitting()
        {
            if (HasActiveSession)
            {
                // Only release the temporary pointer; never open or save scenes during shutdown.
                TryRestorePreviousOverride();
                ClearSession();
            }
        }

        private static void ClearSession()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.delayCall -= CompleteSessionWhenStable;
            completionQueued = false;
            restoreErrorReported = false;
            SessionState.EraseBool(ActiveSessionKey);
            SessionState.EraseString(TargetGuidKey);
            SessionState.EraseString(PreviousOverrideGuidKey);
            SessionState.EraseBool(ReturnToPreviousSceneKey);
            SessionState.EraseBool(EnteredPlayModeKey);
        }

        private static void WarnException(string context, Exception exception)
        {
            Debug.LogWarning(WarningPrefix + context + ": " + exception.Message);
        }
    }
}
