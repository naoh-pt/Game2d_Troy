# Projectile Folder Structure Setup Report

**Date:** 2026-10-01  
**Scope:** `Assets/AttackOnTroy/` projectile system directory audit and setup  

---

## 1. Relevant Existing Directory Structure Discovered

An audit of `Assets/AttackOnTroy` revealed an established domain-driven architecture:

- **Art (`Assets/AttackOnTroy/Art/`)**: Contains visual art assets. `Art/Projectiles/` already existed (empty with `.meta`), alongside `Art/Characters/` (`Achilles`, `Odysseus`, `Cyclops/Heavy`, `Cyclops/Normal`), `Art/Effects/` (`Destruction`, `Fire`, `Impact`, `Weather`), `Art/Environment/`, `Art/Materials/`, `Art/Shaders/`, and `Art/UI/`.
- **Combat (`Assets/AttackOnTroy/Combat/`)**: Houses all combat gameplay components:
  - `Combat/Prefabs/Projectiles/`: Established container for projectile prefabs.
  - `Combat/Scripts/Projectiles/`: Established container for projectile runtime scripts.
  - `Combat/Data/Projectiles/`: Established container for projectile ScriptableObject data.
  - `Combat/Prefabs/Effects/`: Established container for combat and projectile effect prefabs.
  - `Combat/Prefabs/DamageAreas/`: Established container for area-of-effect damage prefabs.
- **Physics (`Assets/AttackOnTroy/Physics/`)**: Contains `Physics/Materials/` housing `PM_ActorMovement.physicsMaterial2D`, defining the project's physics material convention.
- **Characters & Enemies (`Assets/AttackOnTroy/Characters/`, `Enemies/`)**: Player (`Achilles`, `Odysseus`) and enemy (`CyclopsNormal`, `CyclopsHeavy`) implementation directories containing character-specific prefabs and scripts.
- **Scripts (`Assets/AttackOnTroy/Scripts/`)**: Contains cross-cutting subsystem scripts (`Scripts/Camera/`, `Scripts/Characters/Shared/`, `Scripts/World/Terrain/`).
- **Inspected paths not present at root**: `Assets/AttackOnTroy/Projectiles`, `Assets/AttackOnTroy/Prefabs`, and `Assets/AttackOnTroy/Effects` do not exist at the root level; their assets are canonically organized under `Combat/` and `Art/`.

---

## 2. Directories Created

Only the minimal missing character-specific projectile art folders were created under the existing `Art/Projectiles/` root:

1. `Assets/AttackOnTroy/Art/Projectiles/Achilles`
2. `Assets/AttackOnTroy/Art/Projectiles/Odysseus`
3. `Assets/AttackOnTroy/Art/Projectiles/CyclopsNormal`
4. `Assets/AttackOnTroy/Art/Projectiles/CyclopsHeavy`

*Note: In accordance with project instructions, no `.meta` files were manually created for these new directories; Unity will generate them upon import.*

---

## 3. Existing Directories Selected for Reuse

The following existing directories match the canonical locations for projectile system assets and are selected for reuse:

- **Projectile Sprite Art Root**: `Assets/AttackOnTroy/Art/Projectiles`
- **Projectile Prefabs**: `Assets/AttackOnTroy/Combat/Prefabs/Projectiles`
- **Shared Projectile Runtime Scripts**: `Assets/AttackOnTroy/Combat/Scripts/Projectiles`
- **Projectile Data (ScriptableObjects)**: `Assets/AttackOnTroy/Combat/Data/Projectiles`
- **Projectile Physics Materials**: `Assets/AttackOnTroy/Physics/Materials` (reusing the existing physics material convention; future materials should follow `PM_*` naming, e.g., `PM_Projectile`)
- **Projectile Effect Prefabs**: `Assets/AttackOnTroy/Combat/Prefabs/Effects`
- **Projectile Impact Visual Art**: `Assets/AttackOnTroy/Art/Effects/Impact`

---

## 4. Directories Deliberately Not Created and Rationale

- **`Assets/AttackOnTroy/Projectiles` (top-level)**: Deliberately omitted. The project separates visual assets (`Art/Projectiles/`) from gameplay logic/prefabs (`Combat/`). A top-level directory would create an ambiguous parallel architecture.
- **`Assets/AttackOnTroy/Prefabs` (top-level)**: Deliberately omitted. Prefabs are organized strictly within their functional domains (`Combat/Prefabs/Projectiles`, `Characters/<Name>/Prefabs`, `Enemies/<Name>/Prefabs`, etc.).
- **`Assets/AttackOnTroy/Effects` (top-level)**: Deliberately omitted. Visual effect sprites belong in `Art/Effects/` and effect prefabs belong in `Combat/Prefabs/Effects/`.
- **`Animations/` & `AnimatorControllers/` under projectile art**: Deliberately omitted per instructions. The first projectile prototype rotates in flight through `Rigidbody2D` physics rather than sprite-sheet animation clips.
- **Separate projectile physics materials folder**: Deliberately omitted. The project consolidates all `PhysicsMaterial2D` assets directly in `Assets/AttackOnTroy/Physics/Materials/`.
- **Separate projectile effects folder**: Deliberately omitted. Existing folders `Combat/Prefabs/Effects/` and `Art/Effects/Impact/` already satisfy impact and projectile VFX requirements.
- **Character subfolders under `Combat/Prefabs/Projectiles/`**: Deliberately omitted. Projectile prefabs are self-describing (`PF_Projectile_*`) and belong directly in this category folder without unnecessary nesting.

---

## 5. Exact Destination for Spear Sprite Import

Import `SPR_Projectile_Achilles_Spear.png` to:

```text
Assets/AttackOnTroy/Art/Projectiles/Achilles/SPR_Projectile_Achilles_Spear.png
```

Full filesystem path:
`D:\nam5\semester_1\game_android\game_2d\gameTroyAchilles\Assets\AttackOnTroy\Art\Projectiles\Achilles\SPR_Projectile_Achilles_Spear.png`

---

## 6. Asset Integrity Confirmation

- **No existing file or folder was moved, renamed, or deleted.**
- **No character assets were reorganized.**
- **No existing `.meta` file was modified or rewritten.**
- **No Unity scenes, prefabs, scripts, ProjectSettings, or packages were altered.**
