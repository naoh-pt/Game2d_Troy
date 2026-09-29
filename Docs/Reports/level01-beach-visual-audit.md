# Level 1 Beach Visual Audit

## 1. Executive summary

The project root is valid and all six expected beach source PNGs are present with adjacent `.meta` files. The required Sorting Layers exist in the expected order. Prop slicing is present: 13 rectangles have unique names and stable sprite IDs; the remaining ordinal names are left unchanged.

The scene found at `Assets/Scenes/SCN_Level01_Beach.unity` contains only an active Main Camera, Camera component, and AudioListener. It does not contain the requested beach artwork, terrain, props, spawns, UI, or minimap. The scene also is not in the expected `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/` folder and is not listed in Build Settings, which currently points to `Assets/Scenes/SampleScene.unity`.

No Unity files were modified. Unity visual verification is still needed for image alignment and scene appearance.

## 2. Audit scope

Read-only inspection covered the Unity root markers; `Assets/AttackOnTroy/Art/Environment/Beach/`; `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/`; `Assets/AttackOnTroy/World/`; `Assets/AttackOnTroy/Core/`; all project scenes and relevant serialized references; `ProjectSettings/TagManager.asset`; `ProjectSettings/EditorBuildSettings.asset`; importer `.meta` files; and PNG headers/hashes. The expected Level 1 folder currently has no scene file. The actual scene was found by searching `Assets/`.

## 3. Project root validation

**Passed.** `Assets/`, `Packages/`, and `ProjectSettings/` all exist in the current directory.

Git status was unavailable before the audit because this directory does not contain a `.git` repository. Git status was checked again after the audit with the same result; see section 14.

## 4. Asset inventory

The PNG dimensions below were read from their PNG headers. Every image has its corresponding `.meta` file.

| Image | Dimensions | `.meta` | SHA-256 duplicate check |
|---|---:|---|---|
| `Assets/AttackOnTroy/Art/Environment/Beach/Background/BG_Beach_Sky.png` | 2172 × 724 | Present | No identical PNG copy found under `Assets/` |
| `Assets/AttackOnTroy/Art/Environment/Beach/Background/BG_Beach_Far.png` | 2172 × 724 | Present | No identical PNG copy found under `Assets/` |
| `Assets/AttackOnTroy/Art/Environment/Beach/Background/BG_Beach_Mid.png` | 2159 × 728 | Present | No identical PNG copy found under `Assets/` |
| `Assets/AttackOnTroy/Art/Environment/Beach/Terrain/ENV_Beach_Terrain.png` | 2172 × 724 | Present | No identical PNG copy found under `Assets/` |
| `Assets/AttackOnTroy/Art/Environment/Beach/Foreground/FG_Beach_Props.png` | 2172 × 724 | Present | No identical PNG copy found under `Assets/` |
| `Assets/AttackOnTroy/Art/Environment/Beach/Props/PROP_Beach_Set.png` | 1774 × 887 | Present | No identical PNG copy found under `Assets/` |

Missing expected images: **None**. Duplicate copies: **None byte-identical under `Assets/`**. The beach image files are placed in corresponding `Background`, `Terrain`, `Foreground`, and `Props` subfolders under the expected Beach asset root. The actual scene is outside the expected Level 1 scene folder. Other assets outside `Assets/AttackOnTroy` include Unity template/project files under `Assets/Scenes/`, `Assets/Settings/`, `Assets/Sprites/`, and `Assets/Welcome/`; these were not modified.

## 5. Import settings comparison

Serialized enum values were interpreted as Unity importer values: texture type `8` = Sprite (2D and UI), sprite mode `1` = Single, `2` = Multiple, mesh type `0` = Tight, `1` = Full Rect, filter mode `1` = Bilinear, and wrap mode `1` = Clamp. Compression value `1` is the normal compressed setting. Android `overridden: 0` means the platform uses inherited/default texture settings. All six files have mipmaps disabled, max size 2048, and Read/Write disabled.

| Image | Type / Mode | PPU | Mesh | Importer default pivot | Actual primary rect pivot | Alpha transparency | Filter / Wrap | Mips | Max | Compression | Android override | Read/Write |
|---|---|---:|---|---|---|---|---|---|---:|---|---|---|
| Sky | Sprite / Single | 32 | Tight | Center | Bottom-left | Off | Bilinear / Clamp | Off | 2048 | Normal | No | Off |
| Far | Sprite / Single | 32 | Tight | Center | Bottom-left | On | Bilinear / Clamp | Off | 2048 | Normal | No | Off |
| Mid | Sprite / Single | 32 | Tight | Center | Bottom-left | On | Bilinear / Clamp | Off | 2048 | Normal | No | Off |
| Terrain | Sprite / Single | 32 | Tight | Center | Bottom-left | On | Bilinear / Clamp | Off | 2048 | Normal | No | Off |
| Foreground | Sprite / Single | 32 | Tight | Center | Bottom-left | On | Bilinear / Clamp | Off | 2048 | Normal | No | Off |
| `PROP_Beach_Set` | Sprite / Multiple | 32 | Full Rect | Center | Per slice; values vary | On | Bilinear / Clamp | Off | 2048 | Normal | No | Off |

The central importer default pivot is Center for all files, but the primary sprite rects in Sky, Far, Mid, Terrain, and Foreground each override it to `(0, 0)` (bottom-left). This differs from the intended Center pivot; inspect in Unity before changing because a bottom-left anchor may be deliberate for layer alignment. Sky Alpha Is Transparency is off; no alpha-on requirement was specified for Sky. Far, Mid, Terrain, Foreground, and the prop sheet have it on as intended.

The expected sampling, PPU, mip, size, and Android inheritance settings otherwise match. `PROP_Beach_Set` has the intended Multiple mode. In addition, serialized sprite rectangle records exist for Far (2), Mid (2), Terrain (5), and Foreground (8) although their importer mode is Single. Unity’s effective sprite output from these mixed records needs Unity visual verification; do not infer that the small rectangles are visible sprites from YAML alone.

## 6. Prop sprite slicing inventory

`PROP_Beach_Set.png.meta` declares **13 current sprite rectangles**. Each current rect has a nonempty `spriteID` and `internalID`; all 13 names and IDs are unique. The `nameFileIdTable` retains 23 entries, including ten names with no current rectangle: `_2`, `_3`, `_5`, `_6`, `_7`, `_9`, `_10`, `_11`, `_19`, and `_22`. Those are stale-looking serialized mappings and may preserve or confuse old references; static inspection cannot establish whether any scene or prefab still uses those file IDs. Current rects have stable per-sprite IDs, while a rectangle rename can change its name/fileID lookup and break name-based serialized references. Preserve the names until reference impact is checked in Unity.

Rect coordinates use Unity texture coordinates (origin at lower left). Overlaps below are bounding-rectangle overlaps; transparent pixels can make them harmless. No duplicate current rect names were found.

| Current name | Rect `(x, y, width, height)` | Proposed semantic name |
|---|---|---|
| `PROP_Beach_Set_4` | `(1194, 493, 569, 380)` | Needs visual identification for gameplay role (large rock; breakability cannot be inferred visually) |
| `PROP_Beach_Set_8` | `(0, 485, 357, 263)` | `SPR_Beach_Rock01` (left rock cluster) |
| `PROP_Beach_Set_12` | `(26, 287, 269, 218)` | `SPR_Beach_Crate01` (left crate) |
| `PROP_Beach_Set_13` | `(353, 279, 295, 189)` | `SPR_Beach_Crate02` (right crate) |
| `PROP_Beach_Set_14` | `(653, 269, 207, 252)` | `SPR_Beach_OilJar` |
| `PROP_Beach_Set_15` | `(878, 231, 286, 273)` | `SPR_Beach_BrokenShield` |
| `PROP_Beach_Set_16` | `(1191, 268, 571, 230)` | `SPR_Beach_Barricade` |
| `PROP_Beach_Set_17` | `(11, 13, 718, 267)` | `SPR_Beach_ShipFragment` |
| `PROP_Beach_Set_18` | `(1145, 30, 213, 246)` | `SPR_Beach_WoodenPost` |
| `PROP_Beach_Set_20` | `(1433, 17, 320, 259)` | `SPR_Beach_TrojanBanner` |
| `PROP_Beach_Set_21` | `(738, 13, 390, 219)` | `SPR_Beach_Rope` |
| `PROP_Beach_Set_0` | `(372, 528, 412, 270)` | `SPR_Beach_Rock02` (second rock cluster from left) |
| `PROP_Beach_Set_1` | `(797, 523, 376, 205)` | `SPR_Beach_Rock03` (third rock cluster from left) |

The visual identities above were inferred from a read-only view of the source sheet. The large rock’s intended breakability and which rock should be named `SPR_Beach_Rock_Unbreakable` cannot be determined from pixels; identify its gameplay role in Unity/data before assigning that name. Detected rectangle overlaps: `_8` with `_12` by 269 × 20 pixels; `_4` with `_16` by 569 × 5 pixels; `_16` with `_18` by 167 × 8 pixels. Confirm the slice outlines visually; these may overlap only in transparent margins.

## 7. Sorting Layers

`ProjectSettings/TagManager.asset` contains all requested layers in the exact requested relative order, after Unity’s `Default` layer. No similarly named duplicate, spelling, or case variation was found.

| Order index | Sorting Layer | Status |
|---:|---|---|
| 1 | BG_Sky | Present |
| 2 | BG_Far | Present |
| 3 | BG_Mid | Present |
| 4 | World | Present |
| 5 | Character | Present |
| 6 | Projectile | Present |
| 7 | Effect | Present |
| 8 | Foreground | Present |
| 9 | UI | Present |

## 8. Level 1 scene hierarchy findings

The scene is `Assets/Scenes/SCN_Level01_Beach.unity`. Its serialized hierarchy contains one active GameObject, `Main Camera`. It has a Transform, enabled Camera, and enabled AudioListener. It has no MonoBehaviour components and no GUID-backed asset references to beach images.

| Expected object/root | Finding |
|---|---|
| Background root | Absent |
| BG_Beach_Sky | Absent; no sprite reference |
| BG_Beach_Far | Absent; no sprite reference |
| BG_Beach_Mid | Absent; no sprite reference |
| World root | Absent |
| ENV_Beach_Terrain | Absent |
| Foreground root | Absent |
| FG_Beach_Props | Absent |
| MainCamera | Present as `Main Camera`, active; enabled Camera and AudioListener |
| Camera bounds / level bounds | No bounds object, collider, or controller reference in scene |
| Props containers | Absent |
| Player spawn | Absent |
| Cyclops spawn | Absent |
| UI/HUD root | Absent |
| Minimap root | Absent |

Because there are no background or terrain renderers in the scene, scene object positions, scales, sorting layer/order, active states, sprite GUIDs, and PPU compatibility cannot be compared. No visual alignment claim is made. Scene-level static scan found no missing MonoBehaviour script references. Engine built-in references are present in scene settings and are not treated as missing project GUIDs.

## 9. Terrain findings

The terrain PNG and importer metadata exist, but no `ENV_Beach_Terrain` GameObject, prefab reference, or terrain component appears in the Level 1 scene. Therefore no scene instance has a verifiable SpriteRenderer, Collider2D, Rigidbody2D/static configuration, PhysicsMaterial2D, GameObject layer, sorting layer/order, or collider count. Terrain collision setup is **Needs Unity visual verification** after the terrain is instantiated. The texture metadata alone cannot establish collider setup.

## 10. Camera findings

| Property | Static finding |
|---|---|
| Projection | Orthographic, enabled |
| Orthographic Size | 5 (expected starting point approximately 6) |
| Transform position | `(0, 0, -10)` |
| Background color | `(0.19215687, 0.3019608, 0.4745098, 0)` |
| Culling Mask | All layers (`m_Bits: 4294967295`) |
| Camera controller script | None on this scene camera |
| Camera/level bounds component | None found |
| Enabled cameras in Level 1 scene | 1 |
| Enabled AudioListeners in Level 1 scene | 1 |
| Horizontal movement follow | No support evidenced in this scene |
| Vertical projectile follow | No support evidenced in this scene |
| Zoom or drag controls | No support evidenced in this scene |

Orthographic projection and Z = -10 match the expected starting point. Size 5 differs from the approximate target of 6. The scene’s camera data does not demonstrate dynamic movement, projectile following, zoom, or drag support. Other template scenes also contain one camera and one listener each, but they are separate scenes and do not create simultaneous listeners when only one scene is loaded.

## 11. Scene and asset health

- **GUID references:** The Level 1 scene includes Unity built-in references only and no beach sprite references. A broad scan of template URP assets reports package/built-in GUIDs that are not represented by local `.meta` files; these are not classified as broken project references. No local missing GUID was established for the target scene.
- **Missing scripts:** No `m_Script: {fileID: 0}` was found in the target scene. The scene has no MonoBehaviours.
- **Duplicate managers:** None detectable in the target scene; it contains only a camera GameObject.
- **Duplicate enabled cameras/listeners:** None in the target scene (one each). Template scenes are separate.
- **Assets outside `Assets/AttackOnTroy`:** Unity project/template assets exist under `Assets/Scenes`, `Assets/Settings`, `Assets/Sprites`, and `Assets/Welcome`, including `SampleScene`, URP settings/templates, and welcome assets. These appear to be Unity template/project content; whether they should remain is a project-owner decision.
- **Generated folders:** Root `Library/`, `Temp/`, and `Logs/` directories exist. `Obj/` and build directories were not found. Git tracking status cannot be determined because Git metadata is absent.
- **Suspicious names:** No files matching `New Animation`, `New Animator Controller`, `final_final`, `unnamed`, or `new prefab` were found under `Assets/`.
- **Build scene list:** `ProjectSettings/EditorBuildSettings.asset` lists enabled `Assets/Scenes/SampleScene.unity`; it does not list `SCN_Level01_Beach.unity`.

## 12. Findings by severity

### Blocker

- None confirmed by this static audit.

### High

- The discovered Level 1 scene contains only a camera and listener. All requested beach layers, terrain, props, player/Cyclops spawns, HUD, and minimap are absent.
- Build Settings currently enables `SampleScene` only; the Level 1 scene is not included.

### Medium

- Terrain is not instantiated, so collision, physics, layer, and sorting setup cannot be verified and the current scene has no beach ground.
- The Single-mode Far, Mid, Terrain, and Foreground importer metadata each retains additional sprite rectangle records (2, 2, 5, and 8 respectively). Confirm the effective imported sprites in Unity.
- The prop sheet has 13 current rectangles but 23 `nameFileIdTable` entries; ten map names have no current rectangles. Assess serialized references before any rename or cleanup.
- Three pairs of current prop rectangles overlap at their bounding boxes. Inspect slice outlines in Unity to ensure the overlaps do not clip neighboring sprites.

### Low

- Sky, Far, Mid, Terrain, and Foreground primary rectangle pivots are `(0, 0)` although the importer default pivot is Center and the intended configuration specifies Center.
- Orthographic Size is 5 versus the approximate target of 6.
- The Level 1 scene is stored in `Assets/Scenes/` instead of the expected level folder; other Unity template directories/assets remain outside `Assets/AttackOnTroy`.

### Informational

- All six expected beach images and `.meta` files exist; no missing or byte-identical duplicate PNG copies were found under `Assets/`.
- Required Sorting Layers exist in the requested order with no naming variants.
- All 13 current prop rectangles have unique names and stable sprite IDs/internal IDs. Ordinal sprite names remain unchanged.
- The target scene has one enabled camera and one enabled AudioListener; no missing MonoBehaviour reference was detected.
- Root `Library/`, `Temp/`, and `Logs/` directories exist; their Git tracking state is unknown without repository metadata.

## 13. Items requiring visual verification in Unity

- Whether the five scene layers (Sky, Far, Mid, Terrain, Foreground) visually align at the intended framing and camera size.
- Whether the retained extra rectangle records on Single-mode images generate any unwanted sprites.
- The three overlapping prop slice bounds, transparent margins, and exact object identity/semantic mapping.
- Whether the largest rock is intended to be unbreakable; pixel appearance does not establish gameplay behavior.
- Terrain SpriteRenderer, collider shape/count, Rigidbody2D/static configuration, PhysicsMaterial2D, GameObject layer, sorting layer/order, and collision fit after adding it to the scene.
- Final rendered camera framing, background composition, and any movement/zoom behavior.

## 14. Git status before and after

- Before audit: unavailable; the working directory has no `.git` repository.
- After audit: unavailable; the working directory still has no `.git` repository.

## 15. Recommended next steps in priority order

1. Assemble the Level 1 scene with Background, World, and Foreground roots; add the five beach layers, props, player/Cyclops spawn points, and UI/HUD/minimap roots.
2. Configure and visually verify terrain collision and physics components, then verify the terrain against the rendered scene.
3. Add the intended Level 1 scene to Build Settings and confirm the startup flow.
4. In Unity Sprite Editor, review the additional Single-mode rectangle records and the three prop rectangle overlaps; retain all current prop names during this audit.
5. Review the prop sheet’s stale-looking name-to-fileID entries and determine gameplay identity before proposing any rename or cleanup.
6. Confirm the requested pivots and camera size in context; adjust only after checking actual visual alignment.
7. Decide whether Unity template assets and root generated folders belong in the repository, then use repository metadata to check tracking status.

## 16. Files created or modified by this audit

- Created or replaced: `Docs/Reports/level01-beach-visual-audit.md` (this report) only.
- Unity assets, `.meta` files, scenes, prefabs, project settings, packages, and sliced sprite names modified: **None**.
