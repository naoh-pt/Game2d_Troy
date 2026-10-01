# Achilles Aiming HUD & Direction Guide Report

## 1. Overview
Implemented a focused aiming HUD, in-world direction guide, prepared-wind lifecycle, and animation duration-aware timeout for the Achilles projectile prototype in `SCN_Level01_Beach.unity`.

## 2. Files Changed & Created
- `Assets/AttackOnTroy/Combat/Scripts/Projectiles/AchillesProjectileDebugLauncher.cs` (Presentation API, prepared wind lifecycle, duration-aware fail-safe timeout).
- `Assets/AttackOnTroy/Combat/Scripts/Projectiles/AimGuideRenderer.cs` (2–3 unit world-space aiming line with facing and visibility management).
- `Assets/AttackOnTroy/Combat/Scripts/Projectiles/ProjectileAimHUD.cs` (Screen HUD displaying angle, power bar fill, prepared wind with arrow, and inactive dimming).
- `Assets/AttackOnTroy/Editor/Setup/BuildAchillesProjectilePrototype.cs` (Idempotent setup wiring HUD, AimGuide, and launcher references).

## 3. UI Hierarchy Created by Setup
```text
HUDCanvas (Canvas, CanvasScaler [1920x1080], GraphicRaycaster)
└── ProjectileAimHUD (RectTransform, Image [BG], CanvasGroup, ProjectileAimHUD)
    ├── AngleText (TextMeshProUGUI: "ANGLE: 45°")
    ├── PowerBar (Image [BG])
    │   ├── Fill (Image [Type: Filled, Horizontal])
    │   └── Label (TextMeshProUGUI: "POWER")
    └── WindText (TextMeshProUGUI: "WIND: 0.0 •")
```

## 4. Prepared-Wind Lifecycle
1. **Initial Selection**: When the launcher initializes, `preparedWind` is randomized between -2 and +2 world units/s².
2. **Aiming & Charge**: Displayed continuously via `CurrentWind` on `ProjectileAimHUD`. It remains static during aiming, charging, and throwing.
3. **Flight**: The fired projectile launches with this exact wind value.
4. **Renewal**: The next wind value is generated strictly upon projectile completion (`OnProjectileCompleted`), ready for the next shot.

## 5. Animation Timeout Calculation
Replaced fixed fail-safe timeout with a speed-aware calculation:
$$\text{stateDuration} = \frac{\text{clipLength}}{\max(0.01, |\text{animator.speed} \cdot \text{stateInfo.speed}|)}$$
$$\text{timeout} = \max(\text{minimumFallbackTimeout (1.5s)}, \text{stateDuration} \times 2.0 + \text{margin (0.5s)})$$
Guarantees slow-motion animations will never trigger fail-safe aborts before `releaseNormalizedTime` (0.45).

## 6. Short Test Steps
1. Open `SCN_Level01_Beach.unity`.
2. Run menu item: `Attack on Troy > Setup > Build Achilles Projectile Prototype`.
3. Enter Play Mode.
4. Observe the compact HUD (top-left) showing `ANGLE: 45°` and `WIND: <value> [◀/▶]`.
5. Observe the 2.5-unit yellow aiming guide line extending from Achilles's spear spawn point.
6. Press `W`/`S`: angle text and line guide rotate smoothly ($10^\circ$–$80^\circ$).
7. Press `A`/`D`: line guide mirrors correctly with facing direction.
8. Hold `Space`: power bar fills proportionally; guide stays visible.
9. Release `Space`: line guide hides, HUD dims during flight, spear fires with the exact prepared wind displayed.
10. On impact: camera returns, HUD brightens, and a new prepared wind value is generated.
