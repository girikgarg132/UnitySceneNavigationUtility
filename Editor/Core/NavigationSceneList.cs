using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GirikGarg.SceneNavigationUtility
{
    /// <summary>
    /// Stores an ordered collection of scene assets for editor navigation.
    /// </summary>
    [CreateAssetMenu(fileName = "NavigationSceneList", menuName = "Girik Garg/Scene Navigation/Scene List")]
    public sealed class NavigationSceneList : ScriptableObject
    {
        [SerializeField]
        private List<SceneAsset> scenes = new List<SceneAsset>();

        /// <summary>
        /// Gets the configured scene assets in their serialized order, including any empty entries.
        /// </summary>
        public IReadOnlyList<SceneAsset> Scenes => scenes ?? (IReadOnlyList<SceneAsset>)Array.Empty<SceneAsset>();
    }
}
