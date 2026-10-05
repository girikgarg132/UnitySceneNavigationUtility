# Navigation Utility

Fast scene navigation and predictable Play Mode startup for the Unity Editor.

Navigation Utility adds **Open Build Scene** and **Open Scene Selection List** submenus to Unity's **File** menu, directly beneath the built-in **Open Recent Scene**. It also adds a scene dropdown to the main toolbar (Unity 6.3+) and an optional "start Play Mode from the first build scene" preference.

```
File
├── New Scene
├── Open Scene
├── Open Recent Scene            >   (Unity built-in)
├── Open Build Scene             >   Enabled build scenes, in build order
├── Open Scene Selection List    >   Scenes from your selected Scene List asset
├── Save
└── ...
```

| | |
| --- | --- |
| **Author** | Girik Garg |
| **Unity** | 6.0 LTS or newer (developed and verified in Unity 6000.6) |
| **Render pipeline** | Any (Built-in, URP, HDRP) |
| **Runtime footprint** | None. Every assembly is Editor-only and excluded from player builds |
| **Dependencies** | None (Unity Test Framework only to run the tests) |
| **License** | MIT |

## Features

- **File > Open Build Scene**: every enabled scene from Build Settings / the active Build Profile, in build order.
- **File > Open Scene Selection List**: an ordered, shareable list of scenes you curate in a `NavigationSceneList` asset.
- Both submenus update automatically when build scenes, the selected list, or assets change.
- The active scene is check-marked. Scenes with the same name show their folder, for example `Main  [Assets > Levels]`.
- Unsaved-change prompt before switching scenes. Cancel leaves your scene setup alone.
- Already-loaded scenes are activated instead of being reopened or duplicated.
- Scene opening is disabled during Play Mode and compilation.
- **Main toolbar dropdown** (Unity 6.3+) and a **bindable shortcut**. These open a popup with build, custom-list, and recent scenes, plus *Open Additively* and *Ping in Project*.
- **Optional Play Mode startup**: always enter Play Mode from the first enabled build scene, then return to your editing setup.
- Settings are per user and per project, so they never pollute version control.

## Installation

### Option 1: Unity package file

Download the `.unitypackage` from the repository's Releases page, then use **Assets > Import Package > Custom Package...**.

To create that file yourself, right-click `Assets/NavigationUtility` in the Project window and choose **Export Package...**.

### Option 2: Package Manager (Git URL)

The folder also contains a `package.json`, so you can install it as a UPM package without copying files. In **Window > Package Manager**, choose **+ > Install package from git URL** and enter:

```text
https://github.com/girikgarg132/SceneNavigationUtiility.git
```

Append a tag to pin a version, for example `https://github.com/girikgarg132/SceneNavigationUtiility.git#v1.0.0`. Git must be installed on your machine.

## Usage

### Open a build scene

Choose **File > Open Build Scene** and pick a scene. If the submenu shows *No enabled build scenes*, add scenes in **File > Build Profiles** (or **Build Settings** on older versions).

### Open a scene from your own list

1. Create a list: **Assets > Create > Girik Garg > Navigation > Scene List**.
2. In the Inspector, add scene assets to the **Scenes** list in the order you want.
3. Click **Use This List for Navigation**. You can also assign the list in **Preferences > Navigation Utility > Scene List Asset**.
4. Choose **File > Open Scene Selection List** and pick a scene.

Commit the list asset (and its `.meta`) to share its contents with your team. Each teammate selects their active list locally, so different people can use different lists. Lists only drive navigation; they never change your build configuration.

> Keep scene-list assets out of `Resources` and Addressables. They reference `SceneAsset`, which is Editor-only.

### Toolbar and shortcut

- **Unity 6.3+**: the main toolbar shows a dropdown labeled with the active scene name. If it's hidden, right-click the toolbar and enable **Girik Garg/Scene Navigation**.
- **Shortcut**: in **Edit > Shortcuts**, search for **Scene Navigation Utility/Open Scene Menu** and assign a key. No default key is bound, so it can't conflict with your layout.

Both open the same popup:

| Entry | Behavior |
| --- | --- |
| Build Scenes / Custom Scene List / Recent Scenes | Opens the scene alone (Single mode), or activates it if it's already loaded. |
| Open Additively | Adds the scene to the currently open scenes and makes it active. |
| Ping in Project | Highlights the scene asset without opening it. |
| Select Scene List Asset | Selects your active list for editing. |
| Clear Recent Scenes | Clears this tool's recent history only. Unity's own recent list is unaffected. |
| Preferences... | Opens the Navigation Utility preferences page. |

### Preferences

Open **Edit > Preferences > Navigation Utility** (macOS: **Unity > Settings > Navigation Utility**).

| Preference | Default | Effect |
| --- | --- | --- |
| Start from first enabled build scene | Off | Play Mode starts from the first enabled build scene, whatever scene you're editing. |
| Return to previous scene setup after play | On | After Stop, Unity restores your original editing scenes. When off, the startup scene stays open on its own (with a save prompt if needed). |
| Scene List Asset | None | The list shown under **File > Open Scene Selection List**. |

The startup option uses Unity's `EditorSceneManager.playModeStartScene`, so your open scenes aren't swapped out before entering Play Mode. It handles edge cases safely:

- No enabled build scenes: logs a warning and enters Play Mode normally.
- Another tool already set a start scene: that tool keeps control.
- **Reload Scene** is disabled in Enter Play Mode Options: switching is skipped. Your settings are never modified.
- Domain reload on or off: both are supported.

## Where data is stored

| Data | Location | In version control? |
| --- | --- | --- |
| Preferences, selected list, recent scenes | `EditorPrefs`, keyed per project path | No |
| Temporary Play Mode state | `SessionState` (cleared when Unity exits) | No |
| Scene list contents | Your `NavigationSceneList` `.asset` files | Yes, if you want to share them |

## How the File menu works

Unity has no public API for main-menu items that change at runtime. Unity builds its own **File > Open Recent Scene** submenu with the internal `UnityEditor.Menu.AddMenuItem` method, and Navigation Utility calls that same method through reflection. All of this code is in `Editor/UI/NavigationFileMenu.cs`.

If a future Unity version removes or changes that method, the package logs one warning and skips the File submenus. The toolbar dropdown, shortcut, preferences, and Play Mode startup are built on public APIs only, so they keep working.

## Compatibility

| Unity version | File submenus, preferences, scene lists | Main toolbar dropdown |
| --- | --- | --- |
| 2022.3 LTS | Supported | Not available |
| 6000.0 – 6000.2 | Supported | Not available |
| 6000.3+ | Supported | Supported (`MainToolbarElement`) |

The toolbar source is compiled only on Unity 6.3+ (`#if UNITY_6000_3_OR_NEWER`).

## Running the tests

1. Make sure **Test Framework** is installed in the Package Manager.
2. Open **Window > General > Test Runner**, then select **EditMode**.
3. Run `GirikGarg.SceneNavigationUtility.Editor.Tests`.

The tests create temporary scene assets, restore your preferences and build-scene list, and clean up afterward.

## Contributing

Issues and pull requests are welcome. When you report a bug, include:

- Your Unity version and OS.
- Your Enter Play Mode options.
- Clear steps to reproduce the problem.

For pull requests:

- Keep all code Editor-only and preserve the unsaved-change prompts.
- Keep per-user settings out of project files.
- Add or update tests where possible.
- Isolate any internal Unity API usage the way `NavigationFileMenu.cs` does, with a graceful fallback.

## License

MIT © Girik Garg. See [LICENSE](LICENSE).
