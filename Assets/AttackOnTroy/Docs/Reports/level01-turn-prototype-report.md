# Level 01 Turn Prototype Report

## Files Created / Modified
- Created: `Assets/AttackOnTroy/Combat/Scripts/Turns/Level01TurnManager.cs`
- Created: `Assets/AttackOnTroy/Editor/Setup/BuildLevel01TurnPrototype.cs`
- Modified: `Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs`

## State Sequence
- `PlayerTurn`: Achilles movement (A/D) and aiming/charging (W/S/Space) enabled; displays `PLAYER TURN`.
- `PlayerThrowing`: Space release begins throw animation; turn input locked to prevent a second throw.
- `ProjectileFlight`: Spear spawns at release threshold (0.45); camera follows spear.
- `ResolutionDelay`: Waits 0.75s allowing existing camera impact hold (0.6s) to settle without conflict.
- `CyclopsTurn`: Displays `CYCLOPS TURN`; camera focuses `Enemy_CyclopsNormal` for 1.5s placeholder.
- `TurnTransition`: Camera refocuses on `Player_Achilles` for 0.4s before restarting `PlayerTurn`.

## Launcher Events Added
- `ThrowStarted`: Fires once in `RequestThrow` when throw animation commences.
- `ProjectileLaunched`: Fires once in `SpawnProjectile` upon actual spear instantiation.
- `ProjectileCompleted`: Fires once in `OnProjectileCompleted` (or on early abort failsafe).
- `SetTurnInputEnabled(bool)`: Gates movement motor and aiming/charging input outside `PlayerTurn`.

## Camera Sequencing
- Uses `Level01CameraController.FocusActor()` and `FollowProjectile()`; avoids direct Transform writes.
- Resolution delay guarantees existing impact hold duration completes prior to Cyclops focus.
- Seamless blend between Achilles -> Spear -> Impact -> Cyclops -> Achilles.

## Setup Status
- Idempotent menu item: `Attack on Troy > Setup > Build Level 01 Turn Prototype`.
- Creates/updates `TurnManager` under `Systems` and `TurnText` under `HUDCanvas`.
- Copies font asset from `AngleText`, preserving custom typography and ProjectileAimHUD layout.
- Marks scene dirty without saving.

## Short Test Steps
1. Open `SCN_Level01_Beach.unity`.
2. Run `Attack on Troy > Setup > Build Level 01 Turn Prototype` in Unity Editor.
3. Enter Play Mode: Verify `PLAYER TURN` displays and movement/aiming work.
4. Hold and release Space: Verify input locks, throw animates, spear launches, and camera tracks.
5. On impact: Verify 0.75s hold, camera pans to Cyclops with `CYCLOPS TURN` for 1.5s.
6. Verify camera returns to Achilles (0.4s) and `PLAYER TURN` restores controls for the next shot.
