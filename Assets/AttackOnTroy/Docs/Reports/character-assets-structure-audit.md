# Character Assets Structure Audit

**Scope:** `Assets/AttackOnTroy/` character art and related Unity assets  
**Audit date:** 2026-09-28  
**Method:** Read-only inspection of paths, `.meta` importer data, AnimationClip YAML, AnimatorController YAML, and serialized scene/prefab references. No Unity Editor AssetDatabase session was used, so findings describe serialized files on disk.

## Summary

- Found 18 character action PNG sheets (five Achilles, five Odysseus, four Cyclops Normal, four Cyclops Heavy), 18 `.anim` clips, and 15 `.controller` assets.
- All sheets are currently multi-sprite (`Sprite Mode: Multiple`) and have sprite pixel-to-unit scale 100 (100 PPU). Sliced sprite names are stored in `.meta`; individual slices are sub-assets, not separate files.
- All 18 clips report `m_LoopTime: 1`, including death, hurt, throw/attack clips. This is current serialized state, not a recommendation.
- The 15 controllers are action-specific, generally one-state controllers. Their default state uses the matching clip as motion. They are in `Sprites/` beside source PNGs and clips; the `Animations/` folders exist but are empty.
- No character prefabs, materials, Avatar Masks, or Override Controllers were found under `Assets/AttackOnTroy/`. Character runtime `Characters/.../Prefabs`, `Enemies/.../Prefabs`, and character `Data`/`Scripts` paths exist as empty folders (only `.meta` folder records were found).
- The two checked-in Level01 scene files contain no references to these character sheets/clips/controllers. No character-related missing GUID reference was identified in scanned scene/prefab files. This does not establish that no external tooling or unscanned runtime code loads assets by path/name.

## Current folder tree

```text
Assets/AttackOnTroy/
├── Art/Characters/
│   ├── Achilles/
│   │   ├── Animations/        [empty]
│   │   └── Sprites/           [5 PNG sheets, 5 clips, 5 controllers]
│   ├── Odysseus/
│   │   ├── Animations/        [empty]
│   │   └── Sprites/           [5 PNG sheets, 5 clips, 5 controllers]
│   └── Cyclops/
│       ├── Normal/
│       │   ├── Animations/    [empty]
│       │   └── Sprites/       [4 PNG sheets, 4 clips, 4 controllers]
│       └── Heavy/
│           ├── Animations/    [empty]
│           └── Sprites/       [4 PNG sheets, 4 clips, 4 controllers]
├── Characters/
│   ├── Achilles/{Data,Prefabs,Scripts}/ [empty]
│   ├── Odysseus/{Data,Prefabs,Scripts}/ [empty]
│   └── Shared/{Prefabs,Scripts}/        [empty]
├── Enemies/
│   ├── CyclopsNormal/{Data,Prefabs,Scripts}/ [empty]
│   ├── CyclopsHeavy/{Data,Prefabs,Scripts}/  [empty]
│   └── Shared/{Prefabs,Scripts}/             [empty]
└── Docs/Reports/
```

The braces in this tree abbreviate sibling directory names. “Empty” means no payload asset files were found in the directory; Unity `.meta` files for folders exist.

## Actual asset inventory

Paths below are exact project-relative paths. Each PNG has a sibling `.meta` file with stable GUID and slice definitions. The `.anim` and `.controller` records are independent Unity assets, each with its own sibling `.meta`.

### Achilles

| Type | Exact path | Notes |
|---|---|---|
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Death.png` | 3 slices: `achiles_die_0..2` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Hurt.png` | 4 slices: `achiles_hur_0..3` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Idle.png` | 6 slices: `achiles_idle_0..5` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Run.png` | 8 slices: `achiles_run_0,2..8` (index 1 absent) |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/SPR_Achilles_Throw.png` | 6 slices: `achiles_attack_0..5` |
| Clip | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Death.anim` | Uses Death sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Hurt.anim` | Uses Hurt sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Idle.anim` | Uses Idle sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Run.anim` | Uses Run sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Throw.anim` | Uses Throw sheet slices |
| Controller | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_attack_0.controller` | Default state motion: `ANIM_Achilles_Throw` |
| Controller | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_die_0.controller` | Default state motion: `ANIM_Achilles_Death` |
| Controller | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_hur_0.controller` | Default state motion: `ANIM_Achilles_Hurt` |
| Controller | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_idle_0.controller` | Default state motion: `ANIM_Achilles_Idle` |
| Controller | `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_run_0.controller` | Default state motion: `ANIM_Achilles_Run` |

### Odysseus

| Type | Exact path | Notes |
|---|---|---|
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus_BowAttack.png` | 5 slices: `odyse_attck_0..4` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus_Hurt.png` | 4 slices: `odyse_hurt_0..3` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus_Idle.png` | 4 slices: `odyse_idle_0,2,4,5` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus_Run.png` | 6 slices: `odyse_run_0,2,4..7` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/SPR_Odysseus__Death.png` | 4 slices: `odyse_death_0..3`; filename has doubled underscore |
| Clip | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_BowAttack.anim` | Uses BowAttack sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_Death.anim` | Uses Death sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_Hurt.anim` | Uses Hurt sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_Idle.anim` | Uses Idle sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_Run.anim` | Uses Run sheet slices |
| Controller | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_attck_4.controller` | Default state motion: BowAttack |
| Controller | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_death_0.controller` | Default state motion: Death |
| Controller | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_hurt_0.controller` | Default state motion: Hurt |
| Controller | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_idle_0.controller` | Default state motion: Idle |
| Controller | `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_run_6.controller` | Default state motion: Run |

### Cyclops Normal

| Type | Exact path | Notes |
|---|---|---|
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Death.png` | 4 slices: suffixes 0,1,3,4 |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Idle.png` | 5 slices: names use `ANIM_CyclopsNormal_Idle_0,1,2,4,5` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Throw.png` | 5 slices: suffixes 0..4 |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Walk.png` | 4 slices: suffixes 1..4 |
| Clip | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Death.anim` | Uses Death sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Idle.anim` | Uses Idle sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Throw.anim` | Uses Throw sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Walk.anim` | Uses Walk sheet slices |
| Controller | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Idle_0.controller` | Default state motion: Idle; controller filename itself has `ANIM_` prefix |
| Controller | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Death_0.controller` | Default state motion: Death |
| Controller | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Throw_1.controller` | Default state motion: Throw |
| Controller | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Walk_3.controller` | Default state motion: Walk |

### Cyclops Heavy

| Type | Exact path | Notes |
|---|---|---|
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_death.png` | 5 slices `c1_death_0..4` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_idle.png` | 5 slices `c1_idle_0..4` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_throw.png` | 5 slices `c1_throw_0,1,3,4,8` |
| Action sheet | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_walk.png` | 8 slices `c1_walk_0..4,7,10,11` |
| Clip | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/ANIM_CyclopsHeavy_Death.anim` | Uses Death sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/ANIM_CyclopsHeavy_Idle.anim` | Uses Idle sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/ANIM_CyclopsHeavy_Throw.anim` | Uses Throw sheet slices |
| Clip | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/ANIM_CyclopsHeavy_Walk.anim` | Uses Walk sheet slices |
| Controller | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_death_0.controller` | Default state motion: Death |
| Controller | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_idle_4.controller` | Default state motion: Idle |
| Controller | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_throw_3.controller` | Default state motion: Throw |
| Controller | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_walk_0.controller` | Default state motion: Walk |

## Import-setting findings

- All 18 PNG `.meta` files declare `TextureImporter.spriteMode: 2` (Multiple), `TextureImporter.textureType: 8` (Sprite), `spritePixelsToUnits: 100`, and `mipmaps.enableMipMap: 0`.
- All are sliced into explicit rectangles in `spriteSheet.sprites`; each slice has a stable local file ID in `internalIDToNameTable`. The slice names and counts are inventoried above. Numeric gaps in slice suffixes are present; names are not uniform and should not be auto-renumbered.
- `filterMode: 1`, max texture size 2048, compression quality 50, and platform texture compression 1 are recorded. The source files have not been reimported or validated in Unity during this audit.
- No character image is a single-sprite texture. No standalone sliced sprite files exist; clips refer to sprite sub-assets through the PNG GUID and local file IDs.

## Animation and controller findings

- There are 18 clips: 5 Achilles, 5 Odysseus, 4 Cyclops Normal, and 4 Cyclops Heavy. Each clip’s sprite keyframes point to the correct same-character/action sheet GUID and its sliced local IDs.
- Every clip currently has `m_LoopTime: 1` (loop enabled). This includes `Death`, `Hurt`, `Throw`, and `BowAttack`. Review intent later before changing any of these.
- There are 15 action-specific Animator Controllers: five each for the players and four each for Cyclops variants. Each has a single Base Layer state and no observed transitions or parameters; the default state is the corresponding action. No single integrated locomotion/combat controller was found.
- Controller motions reference the expected `.anim` GUIDs. Controller states use the action-name state label, while controller file names often retain slice-derived labels/numbers. This creates naming ambiguity but no same-path duplicate filenames.
- The Cyclops Idle clip is referenced by `ANIM_CyclopsNormal_Idle_0.controller`; despite the controller's `ANIM_` prefix, it is a controller, not a clip.

## References, missing references, and ambiguity

- Clip-to-sprite-sheet references resolve by GUID and local sprite file ID. Controller-to-clip references resolve by GUID. Moving the assets through Unity or moving each asset with its `.meta` preserves those GUIDs and these references.
- The checked-in Level01 scene and its pre-hierarchy backup have no references to the character assets. No relevant character references were found in serialized scene/prefab files scanned under `Assets/AttackOnTroy/`.
- No relevant `m_Script: {fileID: 0}` missing-script marker was found in the inspected character-related serialized assets; no character prefabs exist here to inspect. This audit cannot detect components missing from scenes that are not serialized as text or runtime-loaded content outside this project area.
- No unresolved character GUID was observed among the clip/controller internal references. Built-in sentinel GUIDs in Unity scene lighting data are outside this character audit and are not characterized as character asset failures.
- Ambiguous/nonstandard names: source slice labels use misspellings/abbreviations (`achiles`, `odyse`, `attck`); Odysseus Death source has `__Death`; Cyclops Heavy source sheets use opaque `c1_*`; Cyclops Normal Idle sheet slices begin `ANIM_`; per-action controller filenames include slice numbers. These can be documented or renamed in a separately planned cleanup, after checking code/serialized references.
- Duplicate content was not assessed by image hashing. No duplicate paths or same-name sibling files were found in this set. Similar action names across characters are intentional and remain scoped by character directories.

## Comparison with existing organization

The existing project already has a clear split between `Art/Characters` (visual source and animation authoring assets), `Characters` (player implementation), and `Enemies` (enemy implementation). It already models the Cyclops types as `Cyclops/Normal` and `Cyclops/Heavy` under art, and as `Enemies/CyclopsNormal` and `Enemies/CyclopsHeavy` for implementation. Therefore the useful cleanup is to sort the existing assets into the already-created per-character `Animations` folders and add an `AnimatorControllers` folder beside them. Keep the PNG sheets in `Sprites`; do not relocate art into runtime prefab folders. Character prefabs, if added later, belong in the existing runtime `Prefabs` folders.

## Proposed folder tree

```text
Assets/AttackOnTroy/
├── Art/Characters/
│   ├── Achilles/{Sprites,Animations,AnimatorControllers}/
│   ├── Odysseus/{Sprites,Animations,AnimatorControllers}/
│   └── Cyclops/
│       ├── Normal/{Sprites,Animations,AnimatorControllers}/
│       └── Heavy/{Sprites,Animations,AnimatorControllers}/
├── Characters/
│   ├── Achilles/Prefabs/
│   ├── Odysseus/Prefabs/
│   └── Shared/Prefabs/
├── Enemies/
│   ├── CyclopsNormal/Prefabs/
│   ├── CyclopsHeavy/Prefabs/
│   └── Shared/Prefabs/
└── Art/Characters/Shared/ [only if shared character art assets are introduced]
```

The tree reflects the current ownership split. Shared masks/materials should go in an explicit `Art/Characters/Shared` (or existing `Art/Materials` if genuinely general-purpose) only when such shared assets exist.

## Exact safe move plan

Every listed move means move the Unity asset and its sibling `.meta` together, preserving content and GUID. Rename is deliberately `No` in this plan; naming cleanup has higher reference and ambiguity risk and can be assessed separately. Assets not listed here remain at their current path.

| Current path(s) | Proposed path(s) | Move? | Rename? | Known references to preserve | Risk |
|---|---|---:|---:|---|---|
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_{Death,Hurt,Idle,Run,Throw}.anim` | `Assets/AttackOnTroy/Art/Characters/Achilles/Animations/` (same filenames) | Yes | No | Their sprite-sheet GUID/local-slice references; each matching controller’s motion GUID | Medium |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/{achiles_attack_0,achiles_die_0,achiles_hur_0,achiles_idle_0,achiles_run_0}.controller` | `Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/` (same filenames) | Yes | No | Default state/motion points to corresponding Achilles `.anim` GUID | Medium |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_{BowAttack,Death,Hurt,Idle,Run}.anim` | `Assets/AttackOnTroy/Art/Characters/Odysseus/Animations/` (same filenames) | Yes | No | Their sprite-sheet GUID/local-slice references; matching controller motion GUIDs | Medium |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/{odyse_attck_4,odyse_death_0,odyse_hurt_0,odyse_idle_0,odyse_run_6}.controller` | `Assets/AttackOnTroy/Art/Characters/Odysseus/AnimatorControllers/` (same filenames) | Yes | No | Default state/motion points to corresponding Odysseus `.anim` GUID | Medium |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_{Death,Idle,Throw,Walk}.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Animations/` (same filenames) | Yes | No | Their sheet GUID/local-slice references; matching controller motion GUIDs | Medium |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/{ANIM_CyclopsNormal_Idle_0,CyclopsNormal_Death_0,CyclopsNormal_Throw_1,CyclopsNormal_Walk_3}.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/AnimatorControllers/` (same filenames) | Yes | No | Default state/motion points to corresponding Cyclops Normal `.anim` GUID | Medium |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/ANIM_CyclopsHeavy_{Death,Idle,Throw,Walk}.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Animations/` (same filenames) | Yes | No | Their sheet GUID/local-slice references; matching controller motion GUIDs | Medium |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/{c1_death_0,c1_idle_4,c1_throw_3,c1_walk_0}.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/AnimatorControllers/` (same filenames) | Yes | No | Default state/motion points to corresponding Cyclops Heavy `.anim` GUID | Medium |
| All 18 listed action PNGs in each character’s current `Sprites/` folder | Same paths under the same `Sprites/` folders | No | No | Clips address the sheet GUID and slice local IDs; keep both stable | Low |
| All existing folder organization under `Characters/`, `Enemies/`, `Art/Materials/`, and scenes | Same paths | No | No | Runtime/project organization; no character prefab payloads currently present | Low |
| Any future character prefab | Existing runtime destination: Achilles/Odysseus `Characters/<Name>/Prefabs/`; Cyclops `Enemies/CyclopsNormal/Prefabs/` or `Enemies/CyclopsHeavy/Prefabs/` | Not present | N/A | Would reference the chosen controller and character scripts | N/A |

**Risk meaning:** Medium means the serialized references are GUID-based and internally consistent, but scenes, scripts, addressables, animation events, or external tooling may reference paths/names that static text search did not reveal. Low means no relocation is recommended.

## Assets that should remain where they are

- All 18 PNG action sheets should remain in their current per-character `Sprites/` directories. The `.meta` files contain slices and local IDs that the clips depend on.
- Runtime scripts/data/prefab destination folders should remain in the existing `Characters/` and `Enemies/` trees. No actual character prefab or support asset currently needs to be moved there.
- No Avatar Masks, Override Controllers, materials, or shared character assets were found in scope, so none are assigned a speculative destination.
- Existing scenes and scene backups should remain unchanged.

## GUID preservation for a later relocation

Use Unity Editor Project window moves or an Editor utility using `AssetDatabase.MoveAsset`. Unity updates its asset database and preserves GUIDs. If moving outside the Editor, move each asset file and its `.meta` file together, retaining both file contents; never regenerate metadata. For sliced PNGs, moving the PNG and its `.meta` together preserves the parent texture GUID plus sprite local IDs. After a later move, let Unity refresh and verify clip sprite bindings and controller motions in the Inspector. Do not hand-edit Unity YAML to update paths or GUIDs.

## Recommended next step

Review and approve the move table as a future organization change. When relocation is authorized, move the `.anim` and `.controller` assets into the existing `Animations` folders and new per-character `AnimatorControllers` folders using Unity AssetDatabase, then verify representative sprite bindings and default controller motions. Treat filename cleanup and loop-setting changes as separate decisions after intended runtime behavior is confirmed.
