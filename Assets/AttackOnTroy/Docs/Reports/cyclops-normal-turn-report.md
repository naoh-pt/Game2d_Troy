# Level 01 Cyclops Normal Turn Report

## Summary of Fixes: Presentation & Movement
Addressing observed Play Mode issues:
1. **Walk Animation Speed**: Slowed only the Cyclops Normal `Walk` state by adding parameter `WalkSpeed` (initial value `0.7f`) driving state speed via `m_SpeedParameterActive` in `AC_CyclopsNormal.controller`. Configurable at runtime via `walkAnimationSpeedMultiplier = 0.7f` on `CyclopsNormalTurnController`. Global `Animator.speed`, `Throw` animation timing, other actors, and source sprite sheets were left untouched.
2. **Facing Fix**: Inspected source sprite artwork (`CyclopsNormal_Idle.png`, `CyclopsNormal_Walk.png`, `CyclopsNormal_Throw.png`) and confirmed the character natively faces **LEFT** when `SpriteRenderer.flipX == false`. Updated `GroundedActorMotor2D` with `spriteFacesLeftByDefault` support (`flipX = spriteFacesLeftByDefault ? (facingDirection > 0) : (facingDirection < 0)`). Cyclops now faces Achilles to the LEFT without negative root scale or reversing the physical projectile launch trajectory.
3. **Turn Sequence & Repositioning**:
   - **Turn Start**: Cyclops stands in place (`motor.SetMoveInput(0f)`), faces Achilles to the left (`motor.SetFacing(-1)`), plays `Throw`, and launches exactly one stone axe toward Achilles. No movement occurs before throwing.
   - **Post-Axe Repositioning**: When axe flight completes (actor impact, terrain hit, out-of-bounds, or timeout), the camera refocuses on Cyclops (`cameraController.FocusActor(transform)`). Cyclops checks reachable ground and walks a short configurable distance (`repositionDistance = 1.0f`) to another safe ground position without teleporting. If no safe destination exists, repositioning is skipped.
   - **Turn End**: After repositioning (or skip), Cyclops stops, re-faces Achilles to the left, and ends the turn. The camera refocuses Achilles (`cameraController.FocusActor(targetTransform)`), new wind is generated, and `Level01TurnManager` transitions reliably to `PlayerTurn`.
4. **Reliable Completion**: Turn completion fires **exactly once** through state machine locking (`turnCompletedFired` guards, fail-safe timeouts on both throw and repositioning phases), preventing deadlock under all completion or failure conditions.

---

## Files Created / Modified
- **Modified**: [`Assets/AttackOnTroy/Scripts/Characters/Shared/GroundedActorMotor2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Scripts/Characters/Shared/GroundedActorMotor2D.cs)
  - Added `spriteFacesLeftByDefault` serialized field and public property.
  - Updated `Initialize()` and `SetFacing()` to respect sprite orientation convention.
- **Modified**: [`Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/AnimatorControllers/AC_CyclopsNormal.controller`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/AnimatorControllers/AC_CyclopsNormal.controller)
  - Added Float parameter `WalkSpeed` (default `0.7f`).
  - Configured `Walk` state speed to `0.7` driven by `WalkSpeed` parameter.
- **Modified**: [`Assets/AttackOnTroy/Enemies/CyclopsNormal/Prefabs/PF_CyclopsNormal.prefab`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Enemies/CyclopsNormal/Prefabs/PF_CyclopsNormal.prefab)
  - Configured `spriteFacesLeftByDefault: 1` on `GroundedActorMotor2D`.
- **Modified**: [`Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs)
  - Restructured turn phase flow: `Idle` -> `Throwing` (in place) -> `AxeInFlight` -> `Repositioning` -> `Completed`.
  - Added `walkAnimationSpeedMultiplier` (`0.7f`) and `repositionDistance` (`1.0f` with `[FormerlySerializedAs("moveDistance")]`).
  - Implemented post-flight camera refocus on Cyclops and safe destination selection.
- **Modified**: [`Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01CyclopsTurn.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01CyclopsTurn.cs)
  - Updated idempotent setup tool to configure motor `SpriteFacesLeftByDefault = true`, ensure `Visual` `flipX = false`, and set initial repositioning parameters without overwriting scene instance overrides.
- **Preserved Unchanged**:
  - `SCN_Level01_Beach.unity` (scene instance overrides, positions, transforms intact; not auto-saved).
  - Achilles spear, club bomb, weapon selector, custom Greek TMP font asset, and terrain colliders.

---

## Idempotent Editor Setup Command
- **Menu Path**: `Attack on Troy > Setup > Configure Level 01 Cyclops Turn`
- **Behavior**:
  - Confirms active scene is `SCN_Level01_Beach` in Edit Mode.
  - Ensures `PF_Projectile_CyclopsNormal_StoneAxe.prefab` is configured.
  - Ensures `CyclopsNormalTurnController` is attached and wired.
  - Ensures `spriteFacesLeftByDefault` is true and `Visual.flipX` is reset to false.
  - Connects `cyclopsController` to `Level01TurnManager`.
  - Uses Unity Undo and marks scene dirty without auto-saving or entering Play Mode.

---

## Validation Performed
- **Roslyn Assembly Compilation**:
  - Compiled runtime scripts with `csc.dll` against Unity 6 engine assemblies (`UnityEngine.CoreModule`, `UnityEngine.Physics2DModule`, `UnityEngine.AnimationModule`, `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`). Result: **0 compilation errors**.
  - Compiled Editor script `ConfigureLevel01CyclopsTurn.cs` against `UnityEditor.dll` and runtime assemblies. Result: **0 compilation errors**.
- **Turn Transition & Event Integrity**:
  - Verified `turnCompletedFired` idempotency guard prevents double completion calls.
  - Verified all transitions (`BeginTurn` -> `Throwing` -> `AxeInFlight` -> `OnAxeCompleted` -> `Repositioning` -> `CompleteTurn`) have fail-safe timeouts.
  - Verified `HasGroundAhead` and `IsGroundReachable` prevent walking into voids or blocked cliffs.
  - Verified `FacingDirection = -1` computes the exact intended leftward trajectory towards Achilles (`launchDirection = (-cos(rad), sin(rad))`).

---

## What Still Needs Brief Manual Play Mode Check
1. Open Unity Editor with `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity`.
2. Enter Play Mode (or optionally run `Attack on Troy > Setup > Configure Level 01 Cyclops Turn` first if not already run).
3. Throw Achilles' spear/bomb with Space bar.
4. When Achilles' projectile finishes:
   - Observe camera focus transition to `Enemy_CyclopsNormal`.
   - Verify Cyclops stands in place, faces **LEFT** (toward Achilles), plays `Throw`, and launches the axe towards Achilles.
5. While axe flies:
   - Verify camera follows axe in flight.
   - Verify axe hits Achilles or terrain without destroying ground.
6. When axe impact occurs:
   - Verify camera refocuses on Cyclops.
   - Verify Cyclops walks a short distance at the reduced walk speed (`~0.7` animation pace).
   - Verify Cyclops halts, turns to face Achilles, and finishes turn.
7. Verify camera returns to Achilles and `PLAYER TURN` resumes.

---

## Known Limitations
- **Ballistics**: The stone axe continues using configurable fixed elevation and launch speed (45° angle, 19f speed); it does not compute dynamic trajectory solutions for varying actor positions.
- **Obstacle Height**: If extreme terrain elevations occur directly between Cyclops and Achilles, high-angle throws are needed to clear the ridge.
