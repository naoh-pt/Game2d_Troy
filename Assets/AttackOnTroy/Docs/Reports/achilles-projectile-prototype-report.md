# Achilles Projectile Prototype Report

## 1. Prototype Overview
Implemented the first ballistic projectile prototype for Achilles in `SCN_Level01_Beach.unity`. The prototype integrates 2D rigid-body physics, wind acceleration, velocity-aligned flight rotation, circular destructible terrain impact, facing-aware spawn offsets, and seamless camera tracking.

## 2. Implemented Components & Canonical Locations
- **`Projectile2D.cs`**: `Assets/AttackOnTroy/Combat/Scripts/Projectiles/Projectile2D.cs`
  - Manages flight physics via `Rigidbody2D` and `CapsuleCollider2D`.
  - Ignores collisions with the firing actor.
  - Applies horizontal wind acceleration in `FixedUpdate` (`body.linearVelocityX`).
  - Aligns root and `Visual` orientation to current velocity vector.
  - Limits lifetime (~10s) and bounds within `LevelBounds`.
  - On collision with `DestructibleTerrain2D` or `RuntimeTerrainColliders`, calls `DestroyCircle` (~1.0 unit radius) and destroys itself.
- **`AchillesProjectileDebugLauncher.cs`**: `Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs`
  - New Input System controls: `W`/`S` adjust elevation angle (10°–80°, default 45°); holding `Space` charges launch speed (12–24 units/s over 1.5s); releasing fires.
  - Restricts firing to one active projectile at a time.
  - Mirrors local spawn offset horizontally using `GroundedActorMotor2D.FacingDirection` to counter `SpriteRenderer.flipX` orientation.
  - Randomizes shot wind acceleration (-2 to +2 units/s²).
  - Logs a single concise line per launch: angle, speed, and wind.
- **`BuildAchillesProjectilePrototype.cs`**: `Assets/AttackOnTroy/Editor/Setup/BuildAchillesProjectilePrototype.cs`
  - Menu command: `Attack on Troy > Setup > Build Achilles Projectile Prototype`.
  - Preflights `SCN_Level01_Beach`, spear sprite, terrain, player, and camera.
  - Builds/updates `PF_Projectile_Achilles_Spear.prefab`.
  - Configures `ProjectileSpawnPoint` and `AchillesProjectileDebugLauncher` on `Player_Achilles` with zero duplicates. Marks scene dirty.

## 3. Projectile Prefab Specification
- **Path**: `Assets/AttackOnTroy/Combat/Prefabs/Projectiles/PF_Projectile_Achilles_Spear.prefab`
- **Root**: `Rigidbody2D` (Dynamic, gravityScale 1, Interpolate, Continuous, free rotation), horizontal `CapsuleCollider2D` (2.0 x 0.25), `Projectile2D`.
- **Child `Visual`**: `SpriteRenderer` (`SPR_Projectile_Achilles_Spear.png`, Sorting Layer: `Projectile`, Order: 0, scale: 0.12).

## 4. Camera Integration
- On launch: switches `Level01CameraController` to follow projectile (`FollowProjectile`).
- On impact: triggers `NotifyProjectileEnded`, holding crater position for 0.6s before smoothly returning to `Player_Achilles`.
- On timeout/out-of-bounds: returns follow focus immediately to `Player_Achilles`.

## 5. Verification & Constraints
- **Setup command executed:** No (ready for user execution in Unity Editor).
- **Scene saved:** No (`SCN_Level01_Beach.unity` was not modified or saved).
- **Asset integrity:** No character assets, terrain colliders, camera logic, or `.meta` files altered.
