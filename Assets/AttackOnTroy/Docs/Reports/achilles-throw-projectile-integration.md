# Achilles Throw Animation & Projectile Integration Report

## 1. Overview
Integrated the Achilles Throw animation with the projectile prototype. Releasing Space requests a throw, locks character movement and facing, triggers the Animator `Attack` trigger, and spawns the spear at the visual release point.

## 2. Integration Details
- **Actual Throw State Name**: `Attack` (Animator state in `AC_Achilles.controller`, playing `ANIM_Achilles_Throw.anim`).
- **Trigger Name**: `Attack` (hash-evaluated `attackTriggerHash`).
- **Release Normalized Time**: `0.45` (~0.225s into the 0.5s clip, matching the forward spear thrust release pose).
- **Movement & Facing Lock**: Calling `GroundedActorMotor2D.SetMovementEnabled(false)` suppresses move input, keeping Rigidbody2D physics/gravity active while locking facing to `storedFacing`.

## 3. Fail-Safe Behavior
- **Missing/Disabled Animator**: Logs a descriptive error and restores movement immediately.
- **Fail-Safe Timeout**: A 1.5s countdown automatically aborts stuck throw sequences and restores movement via `SetMovementEnabled(true)`.
- **Interruption/Disabling**: If `AchillesProjectileDebugLauncher` is disabled or destroyed mid-throw, `OnDisable`/`OnDestroy` restores character movement.

## 4. Files Changed
- `Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs`
- `Assets/AttackOnTroy/Editor/Setup/BuildAchillesProjectilePrototype.cs`

## 5. Unity Test Steps
1. Open and activate `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity`.
2. Run `Attack on Troy > Setup > Build Achilles Projectile Prototype` to link Animator and Throw release settings.
3. Enter Play Mode.
4. Press `W`/`S` to adjust elevation angle (10°–80°).
5. Hold `Space` to charge speed (12–24 m/s), then release `Space`.
6. Observe Achilles enter the Throw (`Attack`) animation with movement/facing locked.
7. Verify spear spawns at normalized time ~0.45, camera follow begins at release, and movement re-enables when the animation returns to Idle.
8. Face left (`A`) and repeat: verify spear spawns forward-left and mirrors spawn offset.
