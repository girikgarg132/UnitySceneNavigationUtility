using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GirikGarg.SceneNavigationUtility.Tests
{
    /// <summary>
    /// Isolates project preferences and temporary assets without replacing the user's scene setup.
    /// </summary>
    public abstract class NavigationTestFixture
    {
        protected const string StartFromFirstBuildSceneSuffix = "StartFromFirstBuildScene";
        protected const string ReturnToPreviousSceneSuffix = "ReturnToPreviousScene";
        protected const string SceneListGuidSuffix = "SceneListGuid";
        protected const string RecentSceneGuidsSuffix = "RecentSceneGuids";
        protected const string FirstSceneFileName = "First.unity";
        protected const string SecondSceneFileName = "Second.unity";
        protected const string DeletedSceneFileName = "Deleted.unity";
        protected const string MissingSceneFileName = "Missing.unity";
        protected const string MovedSceneFileName = "Moved.unity";
        protected const string MovedSceneListFileName = "MovedSceneList.asset";
        private const string AssetsRoot = "Assets";
        private const string TemporaryFolderPrefix = "NavigationUtilityTests_";
        private const string UniqueNameFormat = "N";
        private const string SceneListFileName = "SceneList.asset";
        private const string ScenesPropertyName = "scenes";
        private const string CleanupFailureMessage = "Navigation Utility test cleanup failed.";
        // Catalog tests need an imported SceneAsset, not a loaded scene. This longstanding scene-level
        // object has no external dependencies and avoids all scene-open/save callbacks and prompts.
        private const string EmptySceneYaml = "%YAML 1.1\n"
            + "%TAG !u! tag:unity3d.com,2011:\n"
            + "--- !u!29 &1\n"
            + "OcclusionCullingSettings:\n"
            + "  m_ObjectHideFlags: 0\n"
            + "  serializedVersion: 2\n"
            + "  m_OcclusionBakeSettings:\n"
            + "    smallestOccluder: 5\n"
            + "    smallestHole: 0.25\n"
            + "    backfaceThreshold: 100\n"
            + "  m_SceneGUID: 00000000000000000000000000000000\n"
            + "  m_OcclusionCullingData: {fileID: 0}\n";

        private readonly List<PreferenceSnapshot> preferenceSnapshots = new List<PreferenceSnapshot>();
        private Scene originalActiveScene;
        private EditorBuildSettingsScene[] originalBuildScenes;
        private bool buildScenesWereChanged;
        private bool observingChanges;
        private string temporaryFolderPath;

        protected int ChangeCount { get; private set; }
        protected string HistoryKey => NavigationPreferences.MakeProjectKey(RecentSceneGuidsSuffix);
        protected string SceneListKey => NavigationPreferences.MakeProjectKey(SceneListGuidSuffix);

        /// <summary>
        /// Captures raw preference values and key presence before clearing only the tested keys.
        /// </summary>
        [SetUp]
        public void SetUpNavigationState()
        {
            preferenceSnapshots.Clear();
            temporaryFolderPath = null;
            originalBuildScenes = null;
            buildScenesWereChanged = false;
            observingChanges = false;
            originalActiveScene = SceneManager.GetActiveScene();

            try
            {
                preferenceSnapshots.Add(PreferenceSnapshot.CaptureBoolean(StartFromFirstBuildSceneSuffix));
                preferenceSnapshots.Add(PreferenceSnapshot.CaptureBoolean(ReturnToPreviousSceneSuffix));
                preferenceSnapshots.Add(PreferenceSnapshot.CaptureString(SceneListGuidSuffix));
                preferenceSnapshots.Add(PreferenceSnapshot.CaptureString(RecentSceneGuidsSuffix));
                foreach (PreferenceSnapshot snapshot in preferenceSnapshots)
                {
                    EditorPrefs.DeleteKey(snapshot.Key);
                }

                ResetChangeCount();
                NavigationPreferences.Changed += CountChange;
                observingChanges = true;
            }
            catch
            {
                RestoreNavigationState();
                throw;
            }
        }

        /// <summary>
        /// Deletes temporary assets and restores original editor state without closing any user scenes.
        /// </summary>
        [TearDown]
        public void TearDownNavigationState()
        {
            RestoreNavigationState();
        }

        protected void ResetChangeCount()
        {
            ChangeCount = 0;
        }

        /// <summary>
        /// Clears history recorded by production scene-save callbacks before measuring test behavior.
        /// </summary>
        protected void ClearHistoryAfterAssetCreation()
        {
            NavigationCatalog.ClearRecentScenes();
            ResetChangeCount();
        }

        protected string TemporaryAssetPath(string fileName)
        {
            if (temporaryFolderPath == null)
            {
                string folderName = TemporaryFolderPrefix + Guid.NewGuid().ToString(UniqueNameFormat);
                temporaryFolderPath = AssetsRoot + "/" + folderName;
                string folderGuid = AssetDatabase.CreateFolder(AssetsRoot, folderName);
                Assert.That(folderGuid, Is.Not.Empty, "The isolated test folder must be created.");
            }

            return temporaryFolderPath + "/" + fileName;
        }

        /// <summary>
        /// Imports an isolated scene asset without loading a regular or preview scene.
        /// </summary>
        protected string CreateSceneAsset(string fileName)
        {
            string scenePath = TemporaryAssetPath(fileName);
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            string absoluteScenePath = Path.Combine(projectPath, scenePath);
            try
            {
                File.WriteAllText(absoluteScenePath, EmptySceneYaml, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalActiveScene),
                    "Importing test scene assets must not change the user's active scene.");
                return scenePath;
            }
            catch
            {
                // Also remove a partial file if import failed before Unity assigned an asset GUID.
                try
                {
                    AssetDatabase.DeleteAsset(scenePath);
                }
                finally
                {
                    if (File.Exists(absoluteScenePath))
                    {
                        File.Delete(absoluteScenePath);
                    }
                }

                throw;
            }
        }

        protected NavigationSceneList CreateSceneListAsset(params SceneAsset[] scenes)
        {
            string assetPath = TemporaryAssetPath(SceneListFileName);
            NavigationSceneList navigationSceneList = ScriptableObject.CreateInstance<NavigationSceneList>();
            try
            {
                using (SerializedObject serializedList = new SerializedObject(navigationSceneList))
                {
                    SerializedProperty sceneArray = serializedList.FindProperty(ScenesPropertyName);
                    Assert.That(sceneArray, Is.Not.Null);
                    sceneArray.arraySize = scenes.Length;
                    for (int sceneIndex = 0; sceneIndex < scenes.Length; sceneIndex++)
                    {
                        sceneArray.GetArrayElementAtIndex(sceneIndex).objectReferenceValue = scenes[sceneIndex];
                    }

                    serializedList.ApplyModifiedPropertiesWithoutUndo();
                }

                AssetDatabase.CreateAsset(navigationSceneList, assetPath);
                Assert.That(AssetDatabase.LoadAssetAtPath<NavigationSceneList>(assetPath), Is.Not.Null);
                return navigationSceneList;
            }
            catch
            {
                if (!EditorUtility.IsPersistent(navigationSceneList))
                {
                    UnityEngine.Object.DestroyImmediate(navigationSceneList);
                }

                throw;
            }
        }

        protected void SetBuildScenes(params EditorBuildSettingsScene[] scenes)
        {
            if (!buildScenesWereChanged)
            {
                originalBuildScenes = EditorBuildSettings.scenes;
                buildScenesWereChanged = true;
            }

            EditorBuildSettings.scenes = scenes;
        }

        private void CountChange()
        {
            ChangeCount++;
        }

        private static void RestoreActiveScene(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded && SceneManager.GetActiveScene() != scene)
            {
                Assert.That(SceneManager.SetActiveScene(scene), Is.True, "The user's active scene must be restored.");
            }
        }

        private void RestoreNavigationState()
        {
            List<Exception> failures = new List<Exception>();
            if (observingChanges)
            {
                NavigationPreferences.Changed -= CountChange;
                observingChanges = false;
            }

            AttemptCleanup(() => RestoreActiveScene(originalActiveScene), failures);

            if (buildScenesWereChanged)
            {
                AttemptCleanup(() => EditorBuildSettings.scenes = originalBuildScenes, failures);
            }

            if (temporaryFolderPath != null)
            {
                AttemptCleanup(() =>
                {
                    if (AssetDatabase.IsValidFolder(temporaryFolderPath))
                    {
                        Assert.That(AssetDatabase.DeleteAsset(temporaryFolderPath), Is.True,
                            "The isolated test folder must be deleted.");
                    }
                }, failures);
            }

            AttemptCleanup(() => RestoreActiveScene(originalActiveScene), failures);
            // Restore prefs last: asset/scene callbacks during cleanup must not overwrite the user's values.
            foreach (PreferenceSnapshot snapshot in preferenceSnapshots)
            {
                AttemptCleanup(snapshot.Restore, failures);
            }

            if (failures.Count > 0)
            {
                throw new AggregateException(CleanupFailureMessage, failures);
            }
        }

        private static void AttemptCleanup(Action cleanup, List<Exception> failures)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        private sealed class PreferenceSnapshot
        {
            internal string Key;
            private bool wasPresent;
            private bool isBoolean;
            private bool booleanValue;
            private string stringValue;

            internal static PreferenceSnapshot CaptureBoolean(string suffix)
            {
                string key = NavigationPreferences.MakeProjectKey(suffix);
                return new PreferenceSnapshot
                {
                    Key = key,
                    wasPresent = EditorPrefs.HasKey(key),
                    isBoolean = true,
                    booleanValue = EditorPrefs.GetBool(key)
                };
            }

            internal static PreferenceSnapshot CaptureString(string suffix)
            {
                string key = NavigationPreferences.MakeProjectKey(suffix);
                return new PreferenceSnapshot
                {
                    Key = key,
                    wasPresent = EditorPrefs.HasKey(key),
                    stringValue = EditorPrefs.GetString(key, string.Empty)
                };
            }

            internal void Restore()
            {
                if (!wasPresent)
                {
                    EditorPrefs.DeleteKey(Key);
                }
                else if (isBoolean)
                {
                    EditorPrefs.SetBool(Key, booleanValue);
                }
                else
                {
                    EditorPrefs.SetString(Key, stringValue);
                }
            }
        }
    }
}
