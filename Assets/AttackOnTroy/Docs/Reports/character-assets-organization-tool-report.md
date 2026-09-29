# Character Animation Asset Organization Tool

**Implementation status:** Editor tool and report created; menu command was not run. No character assets were moved or modified during implementation.

## Tool and menu

- Tool file: `Assets/AttackOnTroy/Editor/Setup/OrganizeCharacterAnimationAssets.cs`
- Exact menu path: **Attack on Troy > Setup > Organize Character Animation Assets**
- Companion rollback menu: **Attack on Troy > Setup > Undo Last Character Animation Asset Organization**
- Persistent move journal: `Library/AttackOnTroyCharacterAnimationAssetMoves.json`

The companion command is the practical Undo path. Unity's `AssetDatabase.MoveAsset` operations do not join Unity's normal Undo stack. The tool records its complete planned inventory and status before moving, updates the journal after every successful move, and can reverse completed moves through `AssetDatabase.MoveAsset` after checking destination GUIDs and free original paths.

## Complete planned move table

All filenames are preserved. Every `.anim` moves to its existing `Animations` folder. Each current action-specific controller moves to its character's `AnimatorControllers/Legacy` folder. Each move operates on the Unity asset through `AssetDatabase.MoveAsset`; Unity maintains its `.meta` and GUID. PNG sheets remain in place.

| Current path | Proposed path | Asset type |
|---|---|---|
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Death.anim` | `Assets/AttackOnTroy/Art/Characters/Achilles/Animations/ANIM_Achilles_Death.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Hurt.anim` | `Assets/AttackOnTroy/Art/Characters/Achilles/Animations/ANIM_Achilles_Hurt.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Idle.anim` | `Assets/AttackOnTroy/Art/Characters/Achilles/Animations/ANIM_Achilles_Idle.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Run.anim` | `Assets/AttackOnTroy/Art/Characters/Achilles/Animations/ANIM_Achilles_Run.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/ANIM_Achilles_Throw.anim` | `Assets/AttackOnTroy/Art/Characters/Achilles/Animations/ANIM_Achilles_Throw.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_attack_0.controller` | `Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/Legacy/achiles_attack_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_die_0.controller` | `Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/Legacy/achiles_die_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_hur_0.controller` | `Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/Legacy/achiles_hur_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_idle_0.controller` | `Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/Legacy/achiles_idle_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Achilles/Sprites/achiles_run_0.controller` | `Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/Legacy/achiles_run_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_BowAttack.anim` | `Assets/AttackOnTroy/Art/Characters/Odysseus/Animations/ANIM_Odysseus_BowAttack.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_Death.anim` | `Assets/AttackOnTroy/Art/Characters/Odysseus/Animations/ANIM_Odysseus_Death.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_Hurt.anim` | `Assets/AttackOnTroy/Art/Characters/Odysseus/Animations/ANIM_Odysseus_Hurt.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_Idle.anim` | `Assets/AttackOnTroy/Art/Characters/Odysseus/Animations/ANIM_Odysseus_Idle.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/ANIM_Odysseus_Run.anim` | `Assets/AttackOnTroy/Art/Characters/Odysseus/Animations/ANIM_Odysseus_Run.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_attck_4.controller` | `Assets/AttackOnTroy/Art/Characters/Odysseus/AnimatorControllers/Legacy/odyse_attck_4.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_death_0.controller` | `Assets/AttackOnTroy/Art/Characters/Odysseus/AnimatorControllers/Legacy/odyse_death_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_hurt_0.controller` | `Assets/AttackOnTroy/Art/Characters/Odysseus/AnimatorControllers/Legacy/odyse_hurt_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_idle_0.controller` | `Assets/AttackOnTroy/Art/Characters/Odysseus/AnimatorControllers/Legacy/odyse_idle_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Odysseus/Sprites/odyse_run_6.controller` | `Assets/AttackOnTroy/Art/Characters/Odysseus/AnimatorControllers/Legacy/odyse_run_6.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Death.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Animations/ANIM_CyclopsNormal_Death.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Idle.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Animations/ANIM_CyclopsNormal_Idle.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Throw.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Animations/ANIM_CyclopsNormal_Throw.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Walk.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Animations/ANIM_CyclopsNormal_Walk.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/ANIM_CyclopsNormal_Idle_0.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/AnimatorControllers/Legacy/ANIM_CyclopsNormal_Idle_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Death_0.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/AnimatorControllers/Legacy/CyclopsNormal_Death_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Throw_1.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/AnimatorControllers/Legacy/CyclopsNormal_Throw_1.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Walk_3.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/AnimatorControllers/Legacy/CyclopsNormal_Walk_3.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/ANIM_CyclopsHeavy_Death.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Animations/ANIM_CyclopsHeavy_Death.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/ANIM_CyclopsHeavy_Idle.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Animations/ANIM_CyclopsHeavy_Idle.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/ANIM_CyclopsHeavy_Throw.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Animations/ANIM_CyclopsHeavy_Throw.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/ANIM_CyclopsHeavy_Walk.anim` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Animations/ANIM_CyclopsHeavy_Walk.anim` | Animation Clip |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_death_0.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/AnimatorControllers/Legacy/c1_death_0.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_idle_4.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/AnimatorControllers/Legacy/c1_idle_4.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_throw_3.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/AnimatorControllers/Legacy/c1_throw_3.controller` | Animator Controller |
| `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/Sprites/c1_walk_0.controller` | `Assets/AttackOnTroy/Art/Characters/Cyclops/Heavy/AnimatorControllers/Legacy/c1_walk_0.controller` | Animator Controller |

**Not moved:** all 18 PNG action sheets and their sliced sprite sub-assets, all scenes and prefabs, scripts, and all other assets. No integrated Animator Controller is created.

## Safety validation implemented

Before the first asset move, the tool checks the entire explicit move plan:

- Each source exists with the expected Unity type (`AnimationClip` or `AnimatorController`), unless that asset is already at its expected destination.
- A missing source and missing destination aborts the run. An existing source and destination for one plan row is a conflict and aborts the run.
- A destination containing the wrong type or any conflicting asset aborts the run.
- Every expected sprite sheet must load as a texture, contain sprite sub-assets, and have a GUID.
- Duplicate planned destinations are rejected.
- Missing destination folders are made with `AssetDatabase.CreateFolder`; PNG and `.meta` files are not handled manually.

The tool is idempotent: correctly placed assets are logged as skipped. Before moving it writes the plan and GUIDs to the journal; after each move it records progress. On success it calls `AssetDatabase.Refresh` and `AssetDatabase.SaveAssets`, then checks that expected GUIDs remain at the new paths, animation sprite curves resolve to PNG sprite assets, controller state motions resolve, and all expected sprite sheets remain available. If a move or verification fails, completed moves remain in the journal for guarded rollback; the code does not delete or overwrite assets.

The operation cannot be atomic across multiple Unity asset moves. If an unexpected Unity/filesystem failure occurs after some moves, the progress journal allows the rollback command to reverse completed moves after checking that paths and GUIDs still match. Folders created before an unexpected move failure may remain empty; the tool does not delete folders or metadata.

## Unity steps for running

1. Open the project in the recorded Unity version (Unity 6.0.6, `6000.6.0f1`) and wait for script compilation and asset import to finish.
2. Review the planned table above and keep a recoverable project backup/version-control checkpoint.
3. In the Unity menu choose **Attack on Troy > Setup > Organize Character Animation Assets**.
4. Read the completion dialog and Console output. Inspect `Library/AttackOnTroyCharacterAnimationAssetMoves.json` for the complete move/skip status and GUIDs.
5. If a run must be reversed and the tool's guarded preflight passes, choose **Attack on Troy > Setup > Undo Last Character Animation Asset Organization**.

## Post-move verification checklist

- Confirm there are 18 clips in the four `Animations` folders and 18 action-specific controllers under the four `AnimatorControllers/Legacy` folders.
- Confirm each moved asset's GUID in the journal matches `AssetDatabase.AssetPathToGUID` at its destination.
- Open representative clips for each character and action; confirm sprite object-reference curves resolve to the original PNG sheets and expected sliced sprites.
- Open the legacy controllers and confirm their state motions resolve to the intended clips and their default states remain present.
- Confirm all 18 PNGs remain in their original `Sprites` folders and retain their existing Sprite Mode and slicing.
- Review Unity Console for import or reference warnings; inspect any reported issue before proceeding with later controller integration.

## Issues preventing a move

No move was prevented during implementation. The source audit's summary count says 15 controllers, while its enumerated per-character inventory lists 18 (5 Achilles, 5 Odysseus, 4 Cyclops Normal, and 4 Cyclops Heavy). The tool follows the explicit inventory paths and includes all 18 listed controllers. The command was intentionally not run, so runtime preflight, move behavior, rollback, and post-move checks remain for manual verification in Unity. No project assets, scenes, prefabs, import settings, or `.meta` files were changed while creating this tool and report.
