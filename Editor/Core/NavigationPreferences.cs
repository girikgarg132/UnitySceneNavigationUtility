using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GirikGarg.SceneNavigationUtility
{
    internal static class NavigationPreferences
    {
        internal const string SettingsPath = "Preferences/Navigation Utility";

        private const string PreferenceKeyPrefix = "GirikGarg.NavigationUtility.";
        private const string StartFromFirstBuildSceneSuffix = "StartFromFirstBuildScene";
        private const string ReturnToPreviousSceneSuffix = "ReturnToPreviousScene";
        private const string SceneListGuidSuffix = "SceneListGuid";
        private const string HashByteFormat = "x2";
        private const int HashCharactersPerByte = 2;
        private const bool DefaultStartFromFirstBuildScene = false;
        private const bool DefaultReturnToPreviousScene = true;
        private const string UnsavedSceneListWarning = "Navigation Utility: save the scene list as an asset before selecting it.";

        private static readonly string ProjectKeyPrefix = CreateProjectKeyPrefix();

        /// <summary>
        /// Occurs when a stored navigation preference or recent-scene history changes.
        /// </summary>
        internal static event Action Changed;

        internal static bool StartFromFirstBuildScene
        {
            get => EditorPrefs.GetBool(MakeProjectKey(StartFromFirstBuildSceneSuffix), DefaultStartFromFirstBuildScene);
            set
            {
                if (StartFromFirstBuildScene == value)
                {
                    return;
                }

                EditorPrefs.SetBool(MakeProjectKey(StartFromFirstBuildSceneSuffix), value);
                NotifyChanged();
            }
        }

        internal static bool ReturnToPreviousScene
        {
            get => EditorPrefs.GetBool(MakeProjectKey(ReturnToPreviousSceneSuffix), DefaultReturnToPreviousScene);
            set
            {
                if (ReturnToPreviousScene == value)
                {
                    return;
                }

                EditorPrefs.SetBool(MakeProjectKey(ReturnToPreviousSceneSuffix), value);
                NotifyChanged();
            }
        }

        internal static NavigationSceneList NavigationSceneList
        {
            get
            {
                string sceneListGuid = EditorPrefs.GetString(MakeProjectKey(SceneListGuidSuffix), string.Empty);
                if (string.IsNullOrWhiteSpace(sceneListGuid))
                {
                    return null;
                }

                string sceneListPath = AssetDatabase.GUIDToAssetPath(sceneListGuid);
                return string.IsNullOrEmpty(sceneListPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<NavigationSceneList>(sceneListPath);
            }
            set
            {
                string sceneListGuid = string.Empty;
                if (value != null)
                {
                    string sceneListPath = AssetDatabase.GetAssetPath(value);
                    sceneListGuid = AssetDatabase.AssetPathToGUID(sceneListPath);
                    if (string.IsNullOrEmpty(sceneListGuid))
                    {
                        Debug.LogWarning(UnsavedSceneListWarning);
                        return;
                    }
                }

                string preferenceKey = MakeProjectKey(SceneListGuidSuffix);
                string previousGuid = EditorPrefs.GetString(preferenceKey, string.Empty);
                if (string.Equals(previousGuid, sceneListGuid, StringComparison.Ordinal))
                {
                    return;
                }

                if (string.IsNullOrEmpty(sceneListGuid))
                {
                    EditorPrefs.DeleteKey(preferenceKey);
                }
                else
                {
                    EditorPrefs.SetString(preferenceKey, sceneListGuid);
                }

                NotifyChanged();
            }
        }

        /// <summary>
        /// Creates a project-scoped key suitable for EditorPrefs or SessionState.
        /// </summary>
        internal static string MakeProjectKey(string suffix)
        {
            if (string.IsNullOrWhiteSpace(suffix))
            {
                throw new ArgumentException("A nonempty preference-key suffix is required.", nameof(suffix));
            }

            return ProjectKeyPrefix + suffix;
        }

        /// <summary>
        /// Notifies listeners after a navigation preference has actually changed.
        /// </summary>
        internal static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static string CreateProjectKeyPrefix()
        {
            string absoluteAssetsPath = Path.GetFullPath(Application.dataPath);
            string projectPath = Path.GetDirectoryName(absoluteAssetsPath) ?? absoluteAssetsPath;
            projectPath = projectPath.Replace('\\', '/');
            if (Path.DirectorySeparatorChar == '\\')
            {
                projectPath = projectPath.ToUpperInvariant();
            }

            byte[] projectPathBytes = Encoding.UTF8.GetBytes(projectPath);
            byte[] projectPathHash;
            using (SHA256 hashAlgorithm = SHA256.Create())
            {
                projectPathHash = hashAlgorithm.ComputeHash(projectPathBytes);
            }

            StringBuilder hashText = new StringBuilder(projectPathHash.Length * HashCharactersPerByte);
            foreach (byte hashByte in projectPathHash)
            {
                hashText.Append(hashByte.ToString(HashByteFormat, CultureInfo.InvariantCulture));
            }

            return PreferenceKeyPrefix + hashText + ".";
        }
    }
}
