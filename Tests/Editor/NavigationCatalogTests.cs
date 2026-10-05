using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GirikGarg.SceneNavigationUtility.Tests
{
    /// <summary>
    /// Covers ordered scene filtering, build/custom sources, and persistent recent-scene history.
    /// </summary>
    [TestFixture]
    public sealed class NavigationCatalogTests : NavigationTestFixture
    {
        private const string WhitespacePath = " \t\r\n";
        private const string CorruptHistoryJson = "this is not JSON";
        private const string IndexedSceneFileNameFormat = "Recent{0:D2}.unity";
        private const string UniqueGuidFormat = "N";
        private const int MaximumRecentScenes = 12;
        private const int OneChange = 1;
        private const int TwoChanges = 2;
        private const int ThreeChanges = 3;

        /// <summary>
        /// Verifies null, empty, blank, missing, and non-scene input is safely excluded.
        /// </summary>
        [Test]
        public void FilterValidScenePaths_RejectsNullEmptyAndInvalidInput()
        {
            NavigationSceneList nonNavigationSceneAsset = CreateSceneListAsset();
            string nonScenePath = AssetDatabase.GetAssetPath(nonNavigationSceneAsset);
            string missingScenePath = TemporaryAssetPath(MissingSceneFileName);

            Assert.That(NavigationCatalog.FilterValidScenePaths(null), Is.Empty);
            Assert.That(NavigationCatalog.FilterValidScenePaths(Array.Empty<string>()), Is.Empty);
            Assert.That(NavigationCatalog.FilterValidScenePaths(new[]
            {
                null, string.Empty, WhitespacePath, missingScenePath, nonScenePath
            }), Is.Empty);
        }

        /// <summary>
        /// Verifies filtering preserves first occurrence order and deduplicates normalized scene paths.
        /// </summary>
        [Test]
        public void FilterValidScenePaths_PreservesCanonicalOrderWithoutDuplicates()
        {
            string firstPath = CreateSceneAsset(FirstSceneFileName);
            string secondPath = CreateSceneAsset(SecondSceneFileName);
            string missingPath = TemporaryAssetPath(MissingSceneFileName);

            IReadOnlyList<string> result = NavigationCatalog.FilterValidScenePaths(new[]
            {
                secondPath.Replace('/', '\\'), missingPath, secondPath,
                firstPath, null, firstPath.Replace('/', '\\')
            });

            CollectionAssert.AreEqual(new[] { secondPath, firstPath }, result);
        }

        /// <summary>
        /// Verifies build order and deduplication while ignoring disabled and missing scene entries.
        /// </summary>
        [Test]
        public void BuildScenes_PreserveEnabledOrderAndIgnoreDisabledMissingDuplicates()
        {
            string firstPath = CreateSceneAsset(FirstSceneFileName);
            string secondPath = CreateSceneAsset(SecondSceneFileName);
            SetBuildScenes(
                new EditorBuildSettingsScene(firstPath, false),
                new EditorBuildSettingsScene(TemporaryAssetPath(MissingSceneFileName), true),
                new EditorBuildSettingsScene(secondPath, true),
                new EditorBuildSettingsScene(firstPath, true),
                new EditorBuildSettingsScene(secondPath, true));

            CollectionAssert.AreEqual(new[] { secondPath, firstPath }, NavigationCatalog.GetBuildScenePaths());
            Assert.That(NavigationCatalog.GetFirstBuildScenePath(), Is.EqualTo(secondPath));
        }

        /// <summary>
        /// Verifies empty or entirely invalid/disabled build settings have no first navigable scene.
        /// </summary>
        [Test]
        public void BuildScenes_WithNoValidEnabledEntryReturnNoFirstScene()
        {
            SetBuildScenes(Array.Empty<EditorBuildSettingsScene>());
            Assert.That(NavigationCatalog.GetBuildScenePaths(), Is.Empty);
            Assert.That(NavigationCatalog.GetFirstBuildScenePath(), Is.Empty);

            string firstPath = CreateSceneAsset(FirstSceneFileName);
            SetBuildScenes(
                new EditorBuildSettingsScene(firstPath, false),
                new EditorBuildSettingsScene(TemporaryAssetPath(MissingSceneFileName), true));
            Assert.That(NavigationCatalog.GetBuildScenePaths(), Is.Empty);
            Assert.That(NavigationCatalog.GetFirstBuildScenePath(), Is.Empty);
        }

        /// <summary>
        /// Verifies absent, empty, or deleted selected list assets safely yield an empty custom catalog.
        /// </summary>
        [Test]
        public void CustomScenes_WithNoListEmptyListOrMissingListReturnEmpty()
        {
            Assert.That(NavigationPreferences.NavigationSceneList, Is.Null);
            Assert.That(NavigationCatalog.GetCustomScenePaths(), Is.Empty);

            NavigationSceneList navigationSceneList = CreateSceneListAsset();
            NavigationPreferences.NavigationSceneList = navigationSceneList;
            Assert.That(navigationSceneList.Scenes, Is.Empty);
            Assert.That(NavigationCatalog.GetCustomScenePaths(), Is.Empty);

            string selectedGuid = EditorPrefs.GetString(SceneListKey);
            Assert.That(AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(navigationSceneList)), Is.True);
            Assert.That(EditorPrefs.GetString(SceneListKey), Is.EqualTo(selectedGuid));
            Assert.That(NavigationPreferences.NavigationSceneList, Is.Null);
            Assert.That(NavigationCatalog.GetCustomScenePaths(), Is.Empty);
        }

        /// <summary>
        /// Verifies null and deleted scene references are filtered while list order and deduplication remain intact.
        /// </summary>
        [Test]
        public void CustomScenes_IgnoreNullMissingEntriesAndKeepUniqueOrder()
        {
            string firstPath = CreateSceneAsset(FirstSceneFileName);
            string secondPath = CreateSceneAsset(SecondSceneFileName);
            string deletedPath = CreateSceneAsset(DeletedSceneFileName);
            SceneAsset firstScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(firstPath);
            SceneAsset secondScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(secondPath);
            SceneAsset deletedScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(deletedPath);
            NavigationSceneList navigationSceneList = CreateSceneListAsset(secondScene, null, deletedScene, firstScene, secondScene);
            NavigationPreferences.NavigationSceneList = navigationSceneList;

            Assert.That(AssetDatabase.DeleteAsset(deletedPath), Is.True);
            CollectionAssert.AreEqual(new[] { secondPath, firstPath }, NavigationCatalog.GetCustomScenePaths());
        }

        /// <summary>
        /// Verifies invalid recording requests neither create history nor send change notifications.
        /// </summary>
        [Test]
        public void RecentScenes_InvalidPathsDoNotWriteOrNotify()
        {
            NavigationSceneList nonNavigationSceneAsset = CreateSceneListAsset();
            ClearHistoryAfterAssetCreation();
            foreach (string invalidPath in new[]
            {
                null, string.Empty, WhitespacePath, TemporaryAssetPath(MissingSceneFileName),
                AssetDatabase.GetAssetPath(nonNavigationSceneAsset)
            })
            {
                NavigationCatalog.RecordRecentScene(invalidPath);
            }

            Assert.That(NavigationCatalog.GetRecentScenePaths(), Is.Empty);
            Assert.That(EditorPrefs.HasKey(HistoryKey), Is.False);
            Assert.That(ChangeCount, Is.Zero);
        }

        /// <summary>
        /// Verifies re-recording promotes one GUID and an already-most-recent scene causes no change event.
        /// </summary>
        [Test]
        public void RecentScenes_ReRecordingPromotesDeduplicatesAndSkipsUnchangedNotification()
        {
            string firstPath = CreateSceneAsset(FirstSceneFileName);
            string secondPath = CreateSceneAsset(SecondSceneFileName);
            ClearHistoryAfterAssetCreation();

            NavigationCatalog.RecordRecentScene(firstPath);
            Assert.That(ChangeCount, Is.EqualTo(OneChange));
            NavigationCatalog.RecordRecentScene(secondPath);
            Assert.That(ChangeCount, Is.EqualTo(TwoChanges));
            NavigationCatalog.RecordRecentScene(firstPath);
            Assert.That(ChangeCount, Is.EqualTo(ThreeChanges));
            CollectionAssert.AreEqual(new[] { firstPath, secondPath }, NavigationCatalog.GetRecentScenePaths());
            string storedHistory = EditorPrefs.GetString(HistoryKey);

            NavigationCatalog.RecordRecentScene(firstPath);
            CollectionAssert.AreEqual(new[] { firstPath, secondPath }, NavigationCatalog.GetRecentScenePaths());
            Assert.That(EditorPrefs.GetString(HistoryKey), Is.EqualTo(storedHistory));
            Assert.That(ChangeCount, Is.EqualTo(ThreeChanges));
        }

        /// <summary>
        /// Verifies recording beyond the bound evicts the oldest scene and keeps the newest twelve in order.
        /// </summary>
        [Test]
        public void RecentScenes_KeepOnlyTwelveMostRecentScenes()
        {
            List<string> scenePaths = new List<string>();
            for (int sceneIndex = 0; sceneIndex <= MaximumRecentScenes; sceneIndex++)
            {
                string sceneName = string.Format(CultureInfo.InvariantCulture, IndexedSceneFileNameFormat, sceneIndex);
                scenePaths.Add(CreateSceneAsset(sceneName));
            }

            ClearHistoryAfterAssetCreation();
            foreach (string scenePath in scenePaths)
            {
                NavigationCatalog.RecordRecentScene(scenePath);
            }

            scenePaths.RemoveAt(0);
            scenePaths.Reverse();
            IReadOnlyList<string> recentPaths = NavigationCatalog.GetRecentScenePaths();
            Assert.That(recentPaths.Count, Is.EqualTo(MaximumRecentScenes));
            CollectionAssert.AreEqual(scenePaths, recentPaths);
            RecentSceneJson storedHistory = JsonUtility.FromJson<RecentSceneJson>(EditorPrefs.GetString(HistoryKey));
            Assert.That(storedHistory.sceneGuids.Count, Is.EqualTo(MaximumRecentScenes));
        }

        /// <summary>
        /// Verifies history resolves a renamed scene by its unchanged GUID rather than a stale path.
        /// </summary>
        [Test]
        public void RecentScenes_GuidsSurviveSceneAssetRename()
        {
            string originalPath = CreateSceneAsset(FirstSceneFileName);
            ClearHistoryAfterAssetCreation();
            NavigationCatalog.RecordRecentScene(originalPath);
            string originalGuid = AssetDatabase.AssetPathToGUID(originalPath);
            string storedHistory = EditorPrefs.GetString(HistoryKey);
            string renamedPath = TemporaryAssetPath(MovedSceneFileName);

            Assert.That(AssetDatabase.MoveAsset(originalPath, renamedPath), Is.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(renamedPath), Is.EqualTo(originalGuid));
            CollectionAssert.AreEqual(new[] { renamedPath }, NavigationCatalog.GetRecentScenePaths());
            Assert.That(EditorPrefs.GetString(HistoryKey), Is.EqualTo(storedHistory));
        }

        /// <summary>
        /// Verifies stale, empty, non-scene, and duplicate GUIDs are pruned and repaired history is stable.
        /// </summary>
        [Test]
        public void RecentScenes_PruneDeletedNonSceneAndDuplicateGuids()
        {
            string validPath = CreateSceneAsset(FirstSceneFileName);
            string deletedPath = CreateSceneAsset(DeletedSceneFileName);
            string validGuid = AssetDatabase.AssetPathToGUID(validPath);
            string deletedGuid = AssetDatabase.AssetPathToGUID(deletedPath);
            NavigationSceneList nonNavigationSceneAsset = CreateSceneListAsset();
            string nonSceneGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(nonNavigationSceneAsset));
            Assert.That(AssetDatabase.DeleteAsset(deletedPath), Is.True);
            RecentSceneJson dirtyHistory = new RecentSceneJson
            {
                sceneGuids = new List<string>
                {
                    deletedGuid, null, string.Empty, WhitespacePath, nonSceneGuid,
                    Guid.NewGuid().ToString(UniqueGuidFormat), validGuid, validGuid
                }
            };
            EditorPrefs.SetString(HistoryKey, JsonUtility.ToJson(dirtyHistory));
            ResetChangeCount();

            CollectionAssert.AreEqual(new[] { validPath }, NavigationCatalog.GetRecentScenePaths());
            string repairedHistory = JsonUtility.ToJson(new RecentSceneJson
            {
                sceneGuids = new List<string> { validGuid }
            });
            Assert.That(EditorPrefs.GetString(HistoryKey), Is.EqualTo(repairedHistory));
            Assert.That(ChangeCount, Is.EqualTo(OneChange));

            CollectionAssert.AreEqual(new[] { validPath }, NavigationCatalog.GetRecentScenePaths());
            Assert.That(ChangeCount, Is.EqualTo(OneChange));
        }

        /// <summary>
        /// Verifies corrupt or empty stored JSON is cleared and subsequent valid recording recovers normally.
        /// </summary>
        [Test]
        public void RecentScenes_CorruptJsonIsClearedAndRecordingRecovers()
        {
            string validPath = CreateSceneAsset(FirstSceneFileName);
            foreach (string invalidJson in new[] { CorruptHistoryJson, string.Empty })
            {
                EditorPrefs.SetString(HistoryKey, invalidJson);
                ResetChangeCount();

                Assert.That(NavigationCatalog.GetRecentScenePaths(), Is.Empty);
                Assert.That(EditorPrefs.HasKey(HistoryKey), Is.False);
                Assert.That(ChangeCount, Is.EqualTo(OneChange));

                NavigationCatalog.RecordRecentScene(validPath);
                CollectionAssert.AreEqual(new[] { validPath }, NavigationCatalog.GetRecentScenePaths());
                Assert.That(ChangeCount, Is.EqualTo(TwoChanges));
                RecentSceneJson recoveredHistory = JsonUtility.FromJson<RecentSceneJson>(EditorPrefs.GetString(HistoryKey));
                CollectionAssert.AreEqual(new[] { AssetDatabase.AssetPathToGUID(validPath) }, recoveredHistory.sceneGuids);
            }
        }

        /// <summary>
        /// Verifies clearing absent history is a no-op and clearing present history notifies exactly once.
        /// </summary>
        [Test]
        public void ClearRecentScenes_OnlyNotifiesWhenStoredKeyExists()
        {
            string validPath = CreateSceneAsset(FirstSceneFileName);
            ClearHistoryAfterAssetCreation();
            NavigationCatalog.ClearRecentScenes();
            Assert.That(ChangeCount, Is.Zero);

            NavigationCatalog.RecordRecentScene(validPath);
            ResetChangeCount();
            NavigationCatalog.ClearRecentScenes();
            Assert.That(EditorPrefs.HasKey(HistoryKey), Is.False);
            Assert.That(NavigationCatalog.GetRecentScenePaths(), Is.Empty);
            Assert.That(ChangeCount, Is.EqualTo(OneChange));

            NavigationCatalog.ClearRecentScenes();
            Assert.That(ChangeCount, Is.EqualTo(OneChange));
        }

        [Serializable]
        private sealed class RecentSceneJson
        {
            public List<string> sceneGuids = new List<string>();
        }
    }
}
