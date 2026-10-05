using UnityEditor;
using UnityEngine;

namespace GirikGarg.SceneNavigationUtility
{
    [CustomEditor(typeof(NavigationSceneList))]
    internal sealed class NavigationSceneListInspector : UnityEditor.Editor
    {
        /// <summary>Draws the shareable scene list and a local selection shortcut.</summary>
        public override void OnInspectorGUI()
        {
            bool sceneListChanged = DrawDefaultInspector();
            EditorGUILayout.Space();
            NavigationSceneList navigationSceneList = (NavigationSceneList)target;
            bool isSelected = NavigationPreferences.NavigationSceneList == navigationSceneList;
            if (sceneListChanged && isSelected)
            {
                // Keeps File > Open Scene Selection List and the toolbar in sync with Inspector edits.
                NavigationPreferences.NotifyChanged();
            }

            EditorGUILayout.HelpBox(isSelected
                ? "This is your selected navigation list for this project."
                : "Scene references are shared with the asset. Selecting this list for navigation is local to you.", MessageType.Info);
            using (new EditorGUI.DisabledScope(isSelected || !AssetDatabase.Contains(navigationSceneList)))
            {
                if (GUILayout.Button("Use This List for Navigation"))
                {
                    NavigationPreferences.NavigationSceneList = navigationSceneList;
                }
            }
        }
    }
}
