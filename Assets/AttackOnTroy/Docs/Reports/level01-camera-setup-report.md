# Level 01 Camera System Setup Report

## Project and scene inspection

Unity root validation passed: `Assets/`, `Packages/`, and `ProjectSettings/` are present. The requested scene exists at `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` and contains the expected Level/World terrain and LevelBounds, Actors spawn points, and Cameras/Main Camera hierarchy. The scene was inspected read-only and was not directly edited.

`LevelBounds` currently has only a Transform. `ENV_Beach_Terrain` has an enabled SpriteRenderer with bounds approximately `67.875 × 22.636` world units at position `(18.85, 2.92, 0)`. The setup tool will use those renderer bounds to create one trigger BoxCollider2D on the existing LevelBounds object.

## Files created or modified

- Created: `Assets/AttackOnTroy/Scripts/Camera/Level01CameraController.cs`
- Created: `Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01Camera.cs`
- Created: `Assets/AttackOnTroy/Docs/Reports/level01-camera-setup-report.md`
- Existing scene, prefab, sprite, material, package, imported-art, and ProjectSettings files modified: **None**.
- Unity may generate `.meta` files for newly imported scripts/folders. No sprite importer `.meta` file was edited.

## Detected input system

- `com.unity.inputsystem` **1.20.0** is installed.
- `ProjectSettings/ProjectSettings.asset` contains `activeInputHandler: 1` (Input System Package / New).
- The runtime controller uses `UnityEngine.InputSystem` Mouse and Touchscreen controls. It does not use or enable the legacy Input Manager.
- The scene has empty `HUDCanvas` and `MinimapCanvas` GameObjects (Transform only). No EventSystem, InputSystemUIInputModule, Canvas, CanvasScaler, or GraphicRaycaster is present. UI blocking checks `EventSystem.current`; raycastable UI and an EventSystem are needed for that guard to detect UI interactions.

## Detected yellow warnings

The available project-local Console record is `Logs/Editor.log`. These warnings were already present before this camera implementation and were left unchanged:

1. `Assets/AttackOnTroy/Editor/Level01BeachHierarchySetup.cs` emits two obsolete API warnings (`CS0618`) for the `FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)` overload and `FindObjectsSortMode`. Each appears twice in the saved log from separate script-compilation passes.
2. Unity ILPP emits two duplicate assembly hint-path warnings, for `System.Runtime.InteropServices.WindowsRuntime.dll` and `System.ComponentModel.Composition.dll`; the build pipeline selects an alternate hint path for each.
3. `Main Camera` has a serialized MonoBehaviour reference with GUID `a79441f348de89743a2939f4d699eac1`, for which no matching project `.meta` GUID was found. This is a likely Missing (Mono Script) component. The component was not removed or changed, and the saved log did not confirm a corresponding Console warning in that session.

The saved Editor log showed a successful prior script build and no C# errors, but it predates these new camera scripts. The two new scripts were then compiled together with Roslyn against the installed Unity 6000.6 editor/engine assemblies plus the cached Input System 1.20.0 and UGUI assemblies: **exit code 0, no diagnostics**. The temporary output assembly was removed. Unity Editor asset import and in-Editor/device behavior still require the manual steps below.

## Camera default settings

| Setting | Inspected value |
|---|---|
| Object | `Cameras/Main Camera`, active, enabled |
| Projection | Orthographic |
| Orthographic Size | 6 |
| Position | `(0, 0, -10)` |
| Scale | `(1, 1, 1)` |
| Background color | `(0.19215687, 0.3019608, 0.4745098, 0)` |
| Culling Mask | All layers |
| LevelBounds position/scale | `(0, 0, 0)` / `(1, 1, 1)` |

The setup command preserves the camera transform, background color, and current orthographic size. It does not reposition or rescale artwork. The controller defaults are drag speed `1`, wheel sensitivity `0.002`, pinch sensitivity `1`, orthographic size range `3–12`, and movement/zoom smooth times `0.12` seconds; these are configurable on the component.

## Implemented behavior

- Mouse drag and wheel zoom in Play Mode in the Unity Editor.
- One-finger Android drag and two-finger pinch zoom through the installed Input System.
- Smooth movement and zoom, configurable input speeds and zoom limits.
- Camera viewport clamping each frame using orthographic size, `camera.aspect`, and the LevelBounds collider. When the viewport is larger than the bounds on an axis, the camera centers on that axis.
- Camera movement and zoom are canceled while the current mouse/touch pointer is over UI according to the EventSystem.
- No camera following, projectile following, parallax, minimap behavior, gameplay, or terrain destruction was added.

## Setup menu path

`AttackOnTroy > Setup > Configure Level 01 Camera`

The Editor command only operates on the active scene named `SCN_Level01_Beach`. It requires one Main Camera and one LevelBounds object, prevents duplicate controller/BoxCollider2D components, reuses a usable Collider2D when present, otherwise sizes one trigger BoxCollider2D from the terrain renderer bounds. It uses Undo and marks the scene dirty without saving it.

## Exact Unity steps

1. Open the project and wait for Unity to finish compiling scripts.
2. Open `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` and make it the active scene.
3. Choose **AttackOnTroy > Setup > Configure Level 01 Camera**.
4. Review the dialog and Hierarchy. Confirm the controller is on the existing Main Camera and LevelBounds has the correctly sized trigger collider.
5. Verify the camera position, background color, and Orthographic Size remain `(0, 0, -10)`, the inspected color, and `6`.
6. Inspect scene composition and collider bounds; press **Ctrl+S** only after reviewing the dirty scene.
7. Enter Play Mode and complete the mouse checklist. Build and deploy to Android for the touch checklist.

## Test checklist

### Unity Editor mouse (Play Mode)

- Drag over empty scene/gameplay space; confirm smooth pan opposite the pointer movement.
- Scroll up and down; confirm smooth zoom and the configured minimum/maximum limits.
- Pan to every edge and corner at minimum, middle, and maximum zoom; confirm the camera viewport remains inside LevelBounds.
- Confirm the clamp responds correctly to the Game view aspect ratio and orthographic size.
- After adding raycastable UI and an EventSystem, drag and scroll over UI; confirm the camera does not move or zoom.
- Confirm camera transform/background/size and all artwork transforms remain unchanged by setup.

### Android touch

- Drag with one finger and confirm smooth pan.
- Pinch with two fingers in both directions and confirm smooth zoom and size limits.
- Test all four bounds edges at several zoom levels.
- After adding raycastable UI and an EventSystem, test drag and pinch over UI and confirm the camera remains still.
- Transition from one finger to two and back; confirm there is no position jump.

## Manual verification still required

- Run the setup menu in Unity, inspect the BoxCollider2D fit and camera clamping in Scene/Game views, then save manually.
- Compile the new scripts in the installed Unity Editor and test mouse input in Play Mode; test touch input on an Android device.
- Add/configure the EventSystem and raycastable UI before verifying UI blocking visually.
- Review the unresolved existing script GUID on Main Camera separately. The camera setup preserves it.
- The Editor setup command has not been executed by this implementation; the scene remains unchanged.
