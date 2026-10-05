using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GirikGarg.SceneNavigationUtility
{
    /// <summary>
    /// Maintains the dynamic File > Open Build Scene and File > Open Scene Selection List submenus,
    /// placed directly beneath Unity's built-in File > Open Recent Scene submenu.
    /// </summary>
    /// <remarks>
    /// Unity has no public API for runtime-generated main-menu items. Unity builds its own
    /// Open Recent Scene submenu through the internal <c>UnityEditor.Menu.AddMenuItem</c> method, so this
    /// class uses the same method through reflection. When that API is unavailable, the class logs a single
    /// warning and the toolbar dropdown and shortcut keep working.
    /// </remarks>
    [InitializeOnLoad]
    internal static class NavigationFileMenu
    {
        internal const string BuildSceneMenuPath = "File/Open Build Scene";
        internal const string SceneListMenuPath = "File/Open Scene Selection List";

        // Unity's built-in File > Open Recent Scene uses priority 152; keeping these adjacent avoids a separator.
        private const int BuildSceneMenuPriority = 153;
        private const int SceneListMenuPriority = 154;
        private const string FileMenuRoot = "File";
        private const string MenuPathSeparator = "/";
        private const string NoShortcut = "";
        private const string NoBuildScenesLabel = "No enabled build scenes";
        private const string NoSceneListLabel = "No scene list selected (Preferences > Navigation Utility)";
        private const string EmptySceneListLabel = "Selected scene list has no valid scenes";
        private const string AddMenuItemMethodName = "AddMenuItem";
        private const string RemoveMenuItemMethodName = "RemoveMenuItem";
        private const string UpdateAllMenusMethodName = "Internal_UpdateAllMenus";
        private const string MenuChangedEventName = "menuChanged";
        private const string MenuApiUnavailableWarning = "Navigation Utility: this Unity version does not expose the menu API used for File > Open Build Scene and File > Open Scene Selection List. Use the main-toolbar dropdown or the Scene Navigation Utility/Open Scene Menu shortcut instead.";
        private const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly MethodInfo AddMenuItemMethod = typeof(Menu).GetMethod(
            AddMenuItemMethodName,
            StaticMemberFlags,
            null,
            new[] { typeof(string), typeof(string), typeof(bool), typeof(int), typeof(Action), typeof(Func<bool>) },
            null);

        private static readonly MethodInfo RemoveMenuItemMethod = typeof(Menu).GetMethod(
            RemoveMenuItemMethodName,
            StaticMemberFlags,
            null,
            new[] { typeof(string) },
            null);

        private static readonly MethodInfo UpdateAllMenusMethod = typeof(EditorUtility).GetMethod(
            UpdateAllMenusMethodName,
            StaticMemberFlags,
            null,
            Type.EmptyTypes,
            null);

        private static string appliedMenuSignature = string.Empty;
        private static bool refreshQueued;
        private static bool warningLogged;

        private readonly struct MenuEntry
        {
            internal readonly string MenuPath;
            internal readonly string ScenePath;
            internal readonly int Priority;
            internal readonly bool IsChecked;

            internal MenuEntry(string menuPath, string scenePath, int priority, bool isChecked)
            {
                MenuPath = menuPath;
                ScenePath = scenePath;
                Priority = priority;
                IsChecked = isChecked;
            }

            internal bool IsPlaceholder => string.IsNullOrEmpty(ScenePath);
        }

        static NavigationFileMenu()
        {
            EditorBuildSettings.sceneListChanged += QueueRefresh;
            EditorApplication.projectChanged += QueueRefresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            Undo.undoRedoPerformed += QueueRefresh;
            NavigationPreferences.Changed += QueueRefresh;
            SubscribeToMenuChanges();
            QueueRefresh();
        }

        /// <summary>
        /// Schedules a rebuild of both File submenus on the next editor update.
        /// </summary>
        internal static void QueueRefresh()
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
            if (AddMenuItemMethod == null || RemoveMenuItemMethod == null)
            {
                LogUnavailableApiOnce();
                return;
            }

            List<MenuEntry> menuEntries = CreateMenuEntries();
            string menuSignature = CreateSignature(menuEntries);
            if (menuSignature == appliedMenuSignature && AreMenusPresent())
            {
                return;
            }

            try
            {
                RemoveExistingItems();
                foreach (MenuEntry menuEntry in menuEntries)
                {
                    AddMenuEntry(menuEntry);
                }

                UpdateAllMenusMethod?.Invoke(null, null);
                appliedMenuSignature = menuSignature;
            }
            catch (Exception exception)
            {
                appliedMenuSignature = string.Empty;
                LogUnavailableApiOnce();
                Debug.LogException(exception);
            }
        }

        private static List<MenuEntry> CreateMenuEntries()
        {
            List<MenuEntry> menuEntries = new List<MenuEntry>();
            string activeScenePath = SceneManager.GetActiveScene().path;

            AddSceneEntries(menuEntries, BuildSceneMenuPath, BuildSceneMenuPriority,
                NavigationCatalog.GetBuildScenePaths(), NoBuildScenesLabel, activeScenePath);

            string emptySceneListLabel = NavigationPreferences.NavigationSceneList == null ? NoSceneListLabel : EmptySceneListLabel;
            AddSceneEntries(menuEntries, SceneListMenuPath, SceneListMenuPriority,
                NavigationCatalog.GetCustomScenePaths(), emptySceneListLabel, activeScenePath);

            return menuEntries;
        }

        private static void AddSceneEntries(List<MenuEntry> menuEntries, string rootPath, int rootPriority,
            IReadOnlyList<string> scenePaths, string emptyLabel, string activeScenePath)
        {
            if (scenePaths.Count == 0)
            {
                menuEntries.Add(new MenuEntry(rootPath + MenuPathSeparator + emptyLabel, null, rootPriority, false));
                return;
            }

            foreach (string scenePath in scenePaths)
            {
                string menuPath = rootPath + MenuPathSeparator + NavigationMenu.CreateSceneLabel(scenePath);
                menuEntries.Add(new MenuEntry(menuPath, scenePath, rootPriority, scenePath == activeScenePath));
            }
        }

        private static void AddMenuEntry(MenuEntry menuEntry)
        {
            Action execute;
            Func<bool> validate;
            if (menuEntry.IsPlaceholder)
            {
                execute = () => { };
                validate = () => false;
            }
            else
            {
                string scenePath = menuEntry.ScenePath;
                execute = () => NavigationSceneOpener.Open(scenePath);
                validate = () => NavigationSceneOpener.CanNavigate;
            }

            AddMenuItemMethod.Invoke(null, new object[]
            {
                menuEntry.MenuPath, NoShortcut, menuEntry.IsChecked, menuEntry.Priority, execute, validate
            });
        }

        private static void RemoveExistingItems()
        {
            foreach (string menuPath in Unsupported.GetSubmenus(FileMenuRoot))
            {
                if (IsOwnedMenuPath(menuPath))
                {
                    RemoveMenuItemMethod.Invoke(null, new object[] { menuPath });
                }
            }
        }

        private static bool AreMenusPresent()
        {
            bool hasBuildSceneMenu = false;
            bool hasSceneListMenu = false;
            foreach (string menuPath in Unsupported.GetSubmenus(FileMenuRoot))
            {
                hasBuildSceneMenu |= menuPath.StartsWith(BuildSceneMenuPath + MenuPathSeparator, StringComparison.Ordinal);
                hasSceneListMenu |= menuPath.StartsWith(SceneListMenuPath + MenuPathSeparator, StringComparison.Ordinal);
            }

            return hasBuildSceneMenu && hasSceneListMenu;
        }

        private static bool IsOwnedMenuPath(string menuPath)
        {
            return menuPath.StartsWith(BuildSceneMenuPath + MenuPathSeparator, StringComparison.Ordinal)
                || menuPath.StartsWith(SceneListMenuPath + MenuPathSeparator, StringComparison.Ordinal);
        }

        private static string CreateSignature(List<MenuEntry> menuEntries)
        {
            StringBuilder signature = new StringBuilder();
            foreach (MenuEntry menuEntry in menuEntries)
            {
                signature.Append(menuEntry.MenuPath).Append('|').Append(menuEntry.ScenePath)
                    .Append('|').Append(menuEntry.IsChecked).Append('\n');
            }

            return signature.ToString();
        }

        private static void SubscribeToMenuChanges()
        {
            // Unity rebuilds the main menu after some editor events, which discards dynamic items.
            try
            {
                EventInfo menuChangedEvent = typeof(Menu).GetEvent(MenuChangedEventName, StaticMemberFlags);
                MethodInfo addHandlerMethod = menuChangedEvent?.GetAddMethod(true);
                if (addHandlerMethod == null || menuChangedEvent.EventHandlerType != typeof(Action))
                {
                    return;
                }

                Action handler = QueueRefresh;
                addHandlerMethod.Invoke(null, new object[] { handler });
            }
            catch (Exception)
            {
                // Optional hook: explicit refresh triggers still keep the menus current.
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            QueueRefresh();
        }

        private static void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            QueueRefresh();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            QueueRefresh();
        }

        private static void LogUnavailableApiOnce()
        {
            if (warningLogged)
            {
                return;
            }

            warningLogged = true;
            Debug.LogWarning(MenuApiUnavailableWarning);
        }
    }
}
