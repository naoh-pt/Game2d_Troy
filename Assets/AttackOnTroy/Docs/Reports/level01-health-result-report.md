# Level 01 Health, Battle Result, and Play Mode Fixes Report

## Overview
This report documents the resolution of three Play Mode issues observed in Level 01 (`SCN_Level01_Beach.unity`):
1. **Restart Button Clickability**: Victory/Defeat result panel appeared, but the Restart button could not receive clicks.
2. **Cyclops Throw Animation Size**: Cyclops visibly shrunk by ~33% during its axe Throw animation compared to Idle.
3. **Cyclops Movement & Position Retention**: Cyclops stepped forward on turn 1, but backtracked to its starting position on turn 2, and showed no movement on subsequent turns.

---

## Root Causes & Implemented Fixes

### 1. Issue 1: Restart Button Clickability
- **Root Causes**:
  - `SCN_Level01_Beach.unity` had no `EventSystem` GameObject. Under Unity's new Input System, Unity UI (`GraphicRaycaster`) cannot dispatch pointer clicks to `Button` components without an active `EventSystem` and `InputSystemUIInputModule`.
  - In `ConfigureLevel01HealthAndBattleResult.cs`, `RestartButton` had no explicit `targetGraphic` assignment, and the child `ButtonText` (`TextMeshProUGUI`) defaulted to `raycastTarget = true`, potentially obstructing click events.
- **Fixes Applied**:
  - **Runtime Fallback**: Added `EnsureEventSystemExists()` in [`Level01BattleUI.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs) `Awake()`. If no `EventSystem` is found in the scene, it creates one with `EventSystem` and `InputSystemUIInputModule`. No duplicates are ever created if one already exists.
  - **Setup Tool**: Updated [`ConfigureLevel01HealthAndBattleResult.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01HealthAndBattleResult.cs) to ensure an `EventSystem` GameObject with `InputSystemUIInputModule` is added via Undo in the scene.
  - **Raycast Configuration**: Explicitly assigned `restartBtn.targetGraphic = btnImg`, set `btnImg.raycastTarget = true`, and set `buttonText.raycastTarget = false`.
  - **TimeScale Reset**: Ensured `Time.timeScale = 1f;` in `Level01BattleUI.RestartBattle()` before reloading `SCN_Level01_Beach`.

### 2. Issue 2: Cyclops Normal Throw Animation Size & Ground Contact
- **Root Cause**:
  - `CyclopsNormal_Idle.png` was authored at higher resolution (Idle frame 0 character content height: 649 px, world height at 100 PPU = 6.49 units).
  - `CyclopsNormal_Throw.png` was authored at smaller resolution (Throw frame 0 character content height: 437 px, world height at 100 PPU = 4.37 units, ~67.3% of Idle).
  - At identical 100 PPU, Cyclops visibly shrunk by ~33% during Throw. Furthermore, the authored pivots placed feet at varying depths below the ground contact line (`-0.575` to `-0.723` units vs. Idle's `-0.3287` units).
- **Fix Applied**:
  - Updated [`CyclopsNormal_Throw.png.meta`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Throw.png.meta):
    - Changed `spritePixelsToUnits` from `100` to `67.334` (`437 px / 6.49 units`), exactly scaling Throw character geometry to 6.49 world units without touching root scale, Rigidbody2D, or colliders.
    - Adjusted `pivot.y` for all 5 throw frames so feet consistently align with the ground line (`local y = -0.3287 units`):
      - `CyclopsNormal_Throw_0`: `pivot.y = 0.05698` (was 0.09457)
      - `CyclopsNormal_Throw_1`: `pivot.y = 0.04562` (was 0.09796)
      - `CyclopsNormal_Throw_2`: `pivot.y = 0.04260` (was 0.08942)
      - `CyclopsNormal_Throw_3`: `pivot.y = 0.05099` (was 0.10959)
      - `CyclopsNormal_Throw_4`: `pivot.y = 0.05221` (was 0.09452)

### 3. Issue 3: Cyclops Movement & Position Retention
- **Root Causes**:
  - In `BeginRepositioning()`, when preferred direction (toward Achilles, -1) was deemed unreachable or finished, the controller fell back to `-preferredDir` (+1), making Cyclops walk backwards to its old starting position on turn 2.
  - `IsGroundReachable` used a short 3m raycast from `y + 1.0f` and checked only the full `repositionDistance` (1.0m); natural slope variations on the beach caused false negatives, rejecting forward moves.
  - `HasGroundAhead` used a 1.5m raycast from `y + 0.5f` (reaching only down to `y - 1.0f`). On the downhill beach slope, the ground surface was slightly lower, causing `edgeDetected = true` on frame 1, halting movement immediately.
- **Fixes Applied**:
  - In [`CyclopsNormalTurnController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs):
    - **No Backtracking**: If preferred forward movement toward Achilles is blocked, Cyclops checks a shorter half-step (`0.5m`). If still blocked, **it stays where it is** and completes the turn without reversing. It never backtracks to the initial spawn position.
    - **Robust Ground Checks**: Deepened `IsGroundReachable` probe to cast from `y + 1.5f` down `4.5f`, verifying walkable slope angle (`normal.y >= 0.5f`) and rejecting steep cliffs.
    - **Slope-Tolerant Edge Probe**: Deepened `HasGroundAhead` probe from `y + 1.5f` down `3.5f` so natural downhill beach contours do not false-trigger edge aborts.
    - **Position Retention & Fall Recovery Sync**: Upon completing movement in `CompleteTurn()`, the motor halts, facing is restored toward Achilles, and `ActorFallRecovery2D.RecordSpawnPose()` is updated to the newly reached position to prevent any recovery reset.
    - **Axe Launch Direction**: Axe launch uses `transform.TransformPoint` from the new reached position, aiming accurately toward Achilles each turn.

---

## Changed Files
1. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01BattleUI.cs)
   - Added automatic `EnsureEventSystemExists()` runtime fallback.
   - Configured `restartButton.targetGraphic = btnImg`, `btnImg.raycastTarget = true`, `restartButtonText.raycastTarget = false`.
   - Set `Time.timeScale = 1f;` on `RestartBattle()`.
2. [`Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01HealthAndBattleResult.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01HealthAndBattleResult.cs)
   - Idempotently creates `EventSystem` with `InputSystemUIInputModule` in `SCN_Level01_Beach.unity` via Undo.
   - Wires `targetGraphic` and clears `raycastTarget` on button text.
3. [`Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Throw.png.meta`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Throw.png.meta)
   - Configured `spritePixelsToUnits: 67.334` to match Idle world height (6.49 units).
   - Aligned pivots for frames 0-4 to maintain consistent ground contact line (`local y = -0.3287`).
4. [`Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Enemies/CyclopsNormal/Scripts/CyclopsNormalTurnController.cs)
   - Removed backtracking behavior: stays at current position if path ahead is blocked.
   - Added half-step fallback before skipping movement.
   - Enhanced ground detection and edge probes for terrain slope tolerance.
   - Calls `ActorFallRecovery2D.RecordSpawnPose()` on move completion.

---

## Hit & Death Animation Feedback Milestone

### Overview
This section documents the integration of hit (hurt) and death animation feedback in Level 01 (`SCN_Level01_Beach.unity`) using the existing animation clips and Animator Controllers for both Achilles and Cyclops Normal.

### Implemented Behaviors & Technical Details

1. **Achilles Non-Lethal Hurt Feedback (`ANIM_Achilles_Hurt`)**
   - **Damage Receiver Trigger**: In [`ActorDamageReceiver2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Damage/ActorDamageReceiver2D.cs), when non-lethal damage is received (`CurrentHealth > 0`), the `Hurt` trigger is activated on the attached/child `Animator`.
   - **Single-Trigger Latching**: Triggers once per damage event (direct axe hit, direct spear hit, or bomb explosion via `HashSet<IDamageable2D>`). `AnyState -> Hurt` has `CanTransitionToSelf = 0`, preventing repeated hurt restarts from the same hit.
   - **Clean Return to Idle/Run**: In [`AC_Achilles.controller`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/AC_Achilles.controller), added transitions exiting `Hurt` with `HasExitTime = 1` (`ExitTime = 1`):
     - Transition to `Run` when `Speed > 0.05`
     - Transition to `Idle` when `Speed <= 0.05`
   - **Non-looping & Visual Stability**: `ANIM_Achilles_Hurt.anim` is non-looping (`m_LoopTime: 0`) and maintains identical local position/scale curves (`{x: -0.1, y: 0.7, z: 0}`) matching Idle/Run.

2. **Cyclops Normal Death Feedback (`ANIM_CyclopsNormal_Death`)**
   - **Death Trigger**: When Cyclops HP reaches 0, `ActorDamageReceiver2D` fires the `Die` trigger on Cyclops Normal's Animator.
   - **Permanent Death Pose**: `AC_CyclopsNormal.controller` enters `Death` state with no exit transitions. The clip `ANIM_CyclopsNormal_Death.anim` is non-looping (`m_LoopTime: 0`), holding the final fallen pose (frame 4) indefinitely.
   - **Action & Collision Disabling**: Upon reaching 0 HP, [`Level01TurnManager.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs) disables `cyclopsController.enabled = false` (locks throwing), halts horizontal motor (`cyclopsMotor.SetMovementEnabled(false)`), and disables `cyclopsDamageReceiver.enabled = false` (prevents further collision damage).
   - **Root & Collider Preservation**: Cyclops root position, `Rigidbody2D`, and `CapsuleCollider2D` remain active so the actor remains grounded without clipping or disappearing.
   - **Visual Size & Ground Contact Consistency**:
     - `CyclopsNormal_Death.png` was drawn at ~58.55% resolution compared to Idle.
     - In [`CyclopsNormal_Death.png.meta`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Death.png.meta), updated `spritePixelsToUnits: 58.552` (matching Idle's 6.49 world units) and adjusted sprite pivots (`py = 0.05038` to `0.09388`) so Cyclops does not shrink upon death and rests accurately on the ground contact line (`local y = -0.3287`).

3. **Lethal Input Lock & Result Panel Delay**
   - **Immediate Input & Turn Lock**: On lethal hit, `Level01TurnManager.EndBattle(isWin)` immediately sets `isBattleOver = true` and `currentState = BattleOver`, locking player input, charging, weapon switching, and all turn pacing transitions.
   - **Camera Focus**: Automatically commands [`Level01CameraController.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Camera/Level01CameraController.cs) to focus the defeated actor so the death animation is front and center.
   - **Configurable Result Delay**: Added `battleResultDelay = 1.0f` (`[Range(0.5f, 3.0f)]`). The result panel (`VICTORY` / `DEFEAT`) with the clickable `Restart` button is shown after this 1-second delay via coroutine, ensuring the complete death animation plays out.

4. **Achilles Death Feedback (`ANIM_Achilles_Death`)**
   - When Achilles HP reaches 0, `ActorDamageReceiver2D` resets any pending `Hurt` trigger and fires `Die`.
   - `AC_Achilles.controller` transitions to `Death` state. `ANIM_Achilles_Death.anim` plays once (`m_LoopTime: 0`) and holds the fallen pose.
   - Turn HUD displays `DEFEAT`, camera focuses Achilles, and `battleUI.ShowResult(false)` is presented after the 1-second delay.

---

## Changed Files
1. [`Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Death.png.meta`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/Characters/Cyclops/Normal/Sprites/CyclopsNormal_Death.png.meta)
   - Configured `spritePixelsToUnits: 58.552` to match Idle world height (6.49 units).
   - Aligned pivots for frames 0, 1, 3, and 4 to maintain consistent ground contact line (`local y = -0.3287`).
2. [`Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/AC_Achilles.controller`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Art/Characters/Achilles/AnimatorControllers/AC_Achilles.controller)
   - Added `Hurt -> Run` transition when `Speed > 0.05` on `ExitTime = 1`.
   - Configured `Hurt -> Idle` transition when `Speed <= 0.05` on `ExitTime = 1`.
3. [`Assets/AttackOnTroy/Combat/Scripts/Damage/ActorDamageReceiver2D.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Damage/ActorDamageReceiver2D.cs)
   - Added automatic `Animator` detection and trigger evaluation (`Hurt` for non-lethal, `Die` for lethal).
   - Added `TriggerHurtAnimation()` and `TriggerDeathAnimation()` public methods.
4. [`Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs)
   - Added configurable `battleResultDelay = 1.0f` (`[Range(0.5f, 3.0f)]`).
   - Immediately locks battle inputs, disables receiver on defeated actor, focuses camera on defeated actor, and delays result UI display via coroutine.
5. [`Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01HealthAndBattleResult.cs`](file:///d:/nam5/semester_1/game_android/game_2d/gameTroyAchilles/Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01HealthAndBattleResult.cs)
   - Wires `battleResultDelay = 1.0f` and ensures `ActorDamageReceiver2D` components are linked to their respective character animators.

---

## Editor Setup Command
- **Menu Path**: `Attack on Troy > Setup > Configure Level 01 Health and Battle Result`
- **When to Run**: Optional if opening scene in Edit Mode to ensure `battleResultDelay` and damage receiver references are serialized in the scene asset. (Runtime scripts also contain automatic self-healing fallbacks via `GetComponentInChildren<Animator>()` if entering Play Mode directly).
- **Idempotence**: Safe to run repeatedly; does not create duplicate components, panels, or event subscriptions. Does not auto-save or enter Play Mode.

---

## Verification Performed
- **Roslyn Compiler (`csc.dll`)**: Compiled both runtime and editor assemblies against Unity 6 engine DLLs (`UnityEngine.CoreModule`, `UnityEngine.UIModule`, `UnityEngine.Physics2DModule`, `UnityEngine.AnimationModule`, `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEditor.CoreModule`, `netstandard.dll`). Result: **0 compilation errors**.
- **PPU & Pivot Mathematics**: Verified via PIL pixel inspection that Cyclops Death frame 0 (`380 px / 58.552 = 6.4900 units`) matches Idle frame 0 height (`649 px / 100 = 6.4900 units`), and all death frames maintain ground contact at `-0.3287 units`.

---

## Four Manual Play Mode Checks
1. **Check 1: Achilles Non-Lethal Hurt Animation Feedback**
   - Enter Play Mode in `SCN_Level01_Beach.unity`.
   - Allow Cyclops to throw its stone axe at Achilles, or throw Achilles' club bomb at the ground near Achilles to inflict self-damage.
   - Verify Achilles plays `ANIM_Achilles_Hurt` once upon taking damage.
   - If stationary, Achilles returns cleanly to `Idle`. If moving during the damage resolution, Achilles transitions to `Run`.
   - Verify `Hurt` does not repeatedly loop or re-trigger from a single hit.
2. **Check 2: Cyclops Normal Lethal Death Animation & Result Panel Delay**
   - Land lethal attacks on Cyclops Normal (e.g. 4 direct hits with spears / bombs to deplete 100 HP).
   - On the lethal hit, verify battle input and turn progression lock immediately.
   - Verify the camera focuses Cyclops Normal while it plays `ANIM_CyclopsNormal_Death` once and rests in its fallen pose without shrinking.
   - Verify Cyclops does not move, throw axes, or deal/take further damage.
   - Verify the `VICTORY` result panel appears ~1.0 second after the lethal hit, allowing the death animation to be fully seen.
3. **Check 3: Achilles Lethal Death Animation Feedback**
   - Deplete Achilles' HP to 0 using Cyclops stone axes or repeated self-bomb blasts.
   - On reaching 0 HP, verify Achilles plays `ANIM_Achilles_Death` once and remains in the fallen pose.
   - Verify the camera focuses Achilles and the `DEFEAT` result panel appears after the 1-second delay.
4. **Check 4: Clean Battle Restart After Death**
   - Click the **RESTART** button on either the `VICTORY` or `DEFEAT` panel.
   - Verify that `SCN_Level01_Beach` cleanly reloads with both actors alive, HP restored to 100/100, full mobility restored, and Player Turn active.

