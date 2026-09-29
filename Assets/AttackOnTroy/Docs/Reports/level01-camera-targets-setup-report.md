# Level 01 Camera Targets Setup Report

## Files created or modified

- Modified `Assets/AttackOnTroy/Scripts/Camera/Level01CameraController.cs`.
- Created `Assets/AttackOnTroy/Editor/Setup/ConfigureLevel01CameraTargets.cs`.
- Created this report at `Assets/AttackOnTroy/Docs/Reports/level01-camera-targets-setup-report.md`.
- `SCN_Level01_Beach.unity` was not modified or saved.

## Architecture

`Level01CameraController` remains the single writer of the Main Camera transform and orthographic size. It now owns a small state machine with `Manual`, `FollowActor`, `FollowProjectile`, and `Transition` modes. No second movement component was introduced.

The existing input and camera code remains in the controller: New Input System mouse/touch input, UI pointer blocking, drag/pinch/wheel handling, smoothing, and `LevelBounds` clamping. Target changes feed desired positions into the same controller. Transitions use an unscaled-time smooth-step blend and replace any transition already in progress. Actor and projectile tracking use separate configurable `SmoothDamp` times. Impact holding is timer state, not a coroutine, so there is no coroutine lifecycle to leak on disable or destroy.

Manual drag during actor follow or an actor transition changes to `Manual`. Panning is ignored while following or transitioning to a projectile. Wheel and pinch zoom remain available in all modes and do not change the requested target. Zoom is preserved when targets change.

## Public API

On `Level01CameraController`:

- `FocusPlayer(bool immediate = false)` — blend to the configured Achilles transform, or snap when immediate.
- `FocusEnemy(bool immediate = false)` — blend to the configured Cyclops transform, or snap when immediate.
- `FocusActor(Transform target, bool immediate = false)` — follow any actor transform with the actor offset/settings.
- `FollowProjectile(Transform projectile)` — blend to and tightly follow any transform; there is no projectile class dependency.
- `NotifyProjectileEnded(Transform fallbackActorOverride = null)` — capture the projectile's last position, hold it for `Impact Hold Duration`, then blend to the override or configured fallback.
- `SetManualMode()` — cancel target/transition/hold state and retain the camera's current position and zoom.
- `SetFallbackActor(Transform actor)` — change the fallback used when a projectile is destroyed or ends.
- `CurrentMode` — read-only `Level01CameraMode` property.
- `CurrentTarget` — read-only target `Transform`; it is null in Manual mode and during the impact hold.

Calling `FollowProjectile(null)` returns toward the fallback. If a followed projectile is destroyed without notification, the controller detects Unity's null transform and safely returns toward the configured fallback without per-frame scene searches.

## Setup command

Menu path: **Attack on Troy > Setup > Configure Level 01 Camera Targets**

The command only operates when the active scene path is exactly:

`Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity`

It preflights unique `Main Camera`, `Player_Achilles`, and `Enemy_CyclopsNormal` objects, rejects duplicate camera controllers, assigns Achilles/player and Cyclops/enemy, and sets Achilles as initial and fallback actor. It reuses the existing controller. If the controller is absent, it only adds one when the camera and exactly one existing `LevelBounds` collider can be resolved safely. It supports Undo, marks the scene dirty, and never saves.

The command does not alter existing camera transform, orthographic size, input tuning, bounds shape/settings, parallax components/factors, actors, or other gameplay objects. Re-running it updates the same component and creates no duplicates.

## Configured defaults

- Initial mode: `FollowActor`
- Initial target: `Player_Achilles`
- Fallback actor: `Player_Achilles`
- Actor follow smooth time: `0.35` seconds
- Projectile follow smooth time: `0.12` seconds
- Target transition duration: `0.5` seconds
- Impact hold duration: `0.6` seconds
- Actor follow offset: `(0, 1, 0)`
- Editor debug hotkeys enabled: `true` (Inspector-configurable and omitted from player code by `UNITY_EDITOR`)

All values are serialized and editable on `Level01CameraController`. Existing manual movement smoothing remains `0.12` seconds unless it was already customized in the scene.

## Exact Unity setup steps

1. Open `Assets/AttackOnTroy/Scenes/Levels/Level01_Beach/SCN_Level01_Beach.unity` and make sure it is the active scene.
2. Wait for Unity script compilation to finish and confirm the Console has no compile errors.
3. Run **Attack on Troy > Setup > Configure Level 01 Camera Targets**.
4. Select `Cameras/Main Camera` and inspect `Level01CameraController`.
5. Confirm Player is `Player_Achilles`, Enemy is `Enemy_CyclopsNormal`, Fallback Actor is `Player_Achilles`, and Initial Mode is `FollowActor`.
6. Review the serialized defaults above and adjust them if desired.
7. Confirm the camera transform, orthographic size, Level Bounds reference, input values, and parallax layer settings are unchanged.
8. Save the scene manually only after review.

## Editor test checklist

- Enter Play Mode: the camera blends to Achilles instead of teleporting and then follows him with the `(0, 1, 0)` offset.
- Press F1: the camera blends to Achilles. Press F2: it blends to Cyclops. Press F3: it returns to Manual.
- Confirm F1/F2/F3 use `Keyboard.current` from the New Input System and are compiled only under `UNITY_EDITOR`, so they are absent from Android builds.
- During actor follow, drag with the mouse and confirm the mode changes to Manual without a jump.
- During actor follow, use the wheel and confirm zoom still works without leaving actor follow.
- In Manual mode, verify mouse drag, wheel zoom, one-finger touch drag, two-finger pinch, smoothing, UI blocking, and LevelBounds clamping.
- Trigger a transition and switch targets again before it finishes; confirm the new transition replaces the old one smoothly.
- After projectile integration, confirm panning is disabled during projectile follow, pinch/wheel zoom still works, the camera remains clamped, and destroy-without-notify returns safely to Achilles.
- Call `NotifyProjectileEnded` at impact; confirm the camera holds the last projectile position for about 0.6 seconds and blends back.
- Disable and re-enable the controller during a transition/hold and confirm there are no exceptions or background behavior.

## Future projectile integration example

```csharp
public sealed class ExampleProjectileCameraBridge : MonoBehaviour
{
    [SerializeField] private Level01CameraController levelCamera;

    public void OnProjectileLaunched(Transform projectile)
    {
        levelCamera.FollowProjectile(projectile);
    }

    public void OnProjectileImpact(Transform actingCharacter)
    {
        levelCamera.NotifyProjectileEnded(actingCharacter);
    }
}
```

The bridge can live in a future projectile/turn system; the camera controller does not require this example class, a particular prefab, a Rigidbody, or a projectile component.

## Existing-controller conflict review

The existing `Level01CameraController` already owned both camera transform and orthographic size in `LateUpdate`. Adding a separate director that also moved the camera would have introduced a writer conflict, so target switching was integrated into that controller. The existing `Configure Level 01 Camera` command remains compatible because `ConfigureReferences` is retained. Its `movementSmoothTime` now specifically controls Manual mode; actor and projectile modes use their dedicated smoothing fields.

No other camera-transform writer was found in the inspected Attack on Troy camera scripts. `Level01ParallaxLayer` remains separate and was not changed.

## Manual verification required

- Run the setup command and save the scene only after inspecting its Undo-able changes; this implementation deliberately did neither.
- Verify Play Mode feel at the actual game view aspect ratio and on an Android touch device.
- Verify F1 is delivered to the Game view on the local Unity/OS/editor shortcut configuration.
- Projectile follow and impact hold require a real future projectile transform for behavioral testing; no fake projectile was created.
