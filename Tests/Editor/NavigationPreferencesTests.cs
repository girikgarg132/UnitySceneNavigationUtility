using System;
using NUnit.Framework;
using UnityEditor;

namespace GirikGarg.SceneNavigationUtility.Tests
{
    /// <summary>
    /// Covers project-scoped keys, change notifications, and GUID-backed scene-list selection.
    /// </summary>
    [TestFixture]
    public sealed class NavigationPreferencesTests : NavigationTestFixture
    {
        private const string FirstTestSuffix = "FirstTestPreference";
        private const string SecondTestSuffix = "SecondTestPreference";
        private const string ProjectKeyPattern = @"^GirikGarg\.NavigationUtility\.[0-9a-f]{64}\.";
        private const string WhitespaceSuffix = " \t\r\n";
        private const string SuffixParameterName = "suffix";
        private const int OneChange = 1;
        private const int TwoChanges = 2;
        private const bool DefaultStartFromFirstBuildScene = false;
        private const bool DefaultReturnToPreviousScene = true;

        /// <summary>
        /// Verifies a stable project fingerprint and distinct keys for distinct suffixes.
        /// </summary>
        [Test]
        public void ProjectKeys_AreStableAndSuffixSpecific()
        {
            string firstKey = NavigationPreferences.MakeProjectKey(FirstTestSuffix);
            string secondKey = NavigationPreferences.MakeProjectKey(SecondTestSuffix);

            Assert.That(NavigationPreferences.MakeProjectKey(FirstTestSuffix), Is.EqualTo(firstKey));
            StringAssert.IsMatch(ProjectKeyPattern, firstKey);
            Assert.That(firstKey, Does.EndWith(FirstTestSuffix));
            Assert.That(secondKey, Does.EndWith(SecondTestSuffix));
            Assert.That(secondKey, Is.Not.EqualTo(firstKey));
            Assert.That(firstKey.Substring(0, firstKey.Length - FirstTestSuffix.Length),
                Is.EqualTo(secondKey.Substring(0, secondKey.Length - SecondTestSuffix.Length)));
        }

        /// <summary>
        /// Verifies null, empty, and whitespace-only suffixes cannot create ambiguous keys.
        /// </summary>
        [Test]
        public void ProjectKeys_RejectInvalidSuffixes()
        {
            foreach (string invalidSuffix in new[] { null, string.Empty, WhitespaceSuffix })
            {
                ArgumentException exception = Assert.Throws<ArgumentException>(
                    () => NavigationPreferences.MakeProjectKey(invalidSuffix));
                Assert.That(exception.ParamName, Is.EqualTo(SuffixParameterName));
            }
        }

        /// <summary>
        /// Verifies both Boolean preferences notify only for real changes, not default or repeated assignments.
        /// </summary>
        [Test]
        public void BooleanPreferences_OnlyNotifyForActualChanges()
        {
            AssertBooleanPreferenceChanges(
                () => NavigationPreferences.StartFromFirstBuildScene,
                value => NavigationPreferences.StartFromFirstBuildScene = value,
                DefaultStartFromFirstBuildScene,
                StartFromFirstBuildSceneSuffix);
            AssertBooleanPreferenceChanges(
                () => NavigationPreferences.ReturnToPreviousScene,
                value => NavigationPreferences.ReturnToPreviousScene = value,
                DefaultReturnToPreviousScene,
                ReturnToPreviousSceneSuffix);
        }

        /// <summary>
        /// Verifies selecting the same list or repeatedly clearing it does not notify or rewrite preferences.
        /// </summary>
        [Test]
        public void SceneListPreference_OnlyNotifiesForActualChanges()
        {
            NavigationSceneList navigationSceneList = CreateSceneListAsset();
            string sceneListGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(navigationSceneList));
            ResetChangeCount();

            NavigationPreferences.NavigationSceneList = null;
            Assert.That(ChangeCount, Is.Zero);
            Assert.That(EditorPrefs.HasKey(SceneListKey), Is.False);

            NavigationPreferences.NavigationSceneList = navigationSceneList;
            Assert.That(ChangeCount, Is.EqualTo(OneChange));
            Assert.That(EditorPrefs.GetString(SceneListKey), Is.EqualTo(sceneListGuid));
            Assert.That(NavigationPreferences.NavigationSceneList, Is.SameAs(navigationSceneList));

            NavigationPreferences.NavigationSceneList = navigationSceneList;
            Assert.That(ChangeCount, Is.EqualTo(OneChange));
            Assert.That(EditorPrefs.GetString(SceneListKey), Is.EqualTo(sceneListGuid));

            NavigationPreferences.NavigationSceneList = null;
            Assert.That(ChangeCount, Is.EqualTo(TwoChanges));
            Assert.That(NavigationPreferences.NavigationSceneList, Is.Null);
            Assert.That(EditorPrefs.HasKey(SceneListKey), Is.False);

            NavigationPreferences.NavigationSceneList = null;
            Assert.That(ChangeCount, Is.EqualTo(TwoChanges));
        }

        /// <summary>
        /// Verifies a selected scene list remains selected after its asset is renamed.
        /// </summary>
        [Test]
        public void SceneListPreference_GuidSurvivesAssetRename()
        {
            NavigationSceneList navigationSceneList = CreateSceneListAsset();
            NavigationPreferences.NavigationSceneList = navigationSceneList;
            string storedGuid = EditorPrefs.GetString(SceneListKey);
            string originalPath = AssetDatabase.GetAssetPath(navigationSceneList);
            string renamedPath = TemporaryAssetPath(MovedSceneListFileName);

            Assert.That(AssetDatabase.MoveAsset(originalPath, renamedPath), Is.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(renamedPath), Is.EqualTo(storedGuid));
            Assert.That(EditorPrefs.GetString(SceneListKey), Is.EqualTo(storedGuid));
            Assert.That(NavigationPreferences.NavigationSceneList, Is.SameAs(navigationSceneList));
            Assert.That(AssetDatabase.GetAssetPath(NavigationPreferences.NavigationSceneList), Is.EqualTo(renamedPath));
        }

        private void AssertBooleanPreferenceChanges(Func<bool> read, Action<bool> write, bool defaultValue, string suffix)
        {
            string preferenceKey = NavigationPreferences.MakeProjectKey(suffix);
            ResetChangeCount();
            Assert.That(read(), Is.EqualTo(defaultValue));

            write(defaultValue);
            Assert.That(ChangeCount, Is.Zero);
            Assert.That(EditorPrefs.HasKey(preferenceKey), Is.False);

            write(!defaultValue);
            Assert.That(read(), Is.EqualTo(!defaultValue));
            Assert.That(EditorPrefs.GetBool(preferenceKey), Is.EqualTo(!defaultValue));
            Assert.That(ChangeCount, Is.EqualTo(OneChange));

            write(!defaultValue);
            Assert.That(ChangeCount, Is.EqualTo(OneChange));

            write(defaultValue);
            Assert.That(read(), Is.EqualTo(defaultValue));
            Assert.That(ChangeCount, Is.EqualTo(TwoChanges));
        }
    }
}
