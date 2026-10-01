# Character Idle Anti-Slide and Small-Step Fix

## Summary of Changes
- **Idle Anti-Slide (`GroundedActorMotor2D`)**:
  - When grounded with zero movement input, tangential velocity along ground is removed.
  - Applied counter-force canceling tangential gravity (`-gravityTangent * body.mass`), stabilizing actors and Cyclops on walkable slopes without freezing Rigidbody2D constraints or moving transforms.
  - If terrain disappears, `IsGrounded` clears and free-fall resumes immediately.
- **Locomotion Animation**:
  - Movement input is passed to `UpdateLocomotionAnimation`; zero input promptly clears the Run latch and forces Idle.
  - Entering Run requires movement input plus actual displacement; obstacle hysteresis is preserved.
  - Facing direction updates on any movement input, even when forward blocked.
- **Small-Step Traversal**:
  - Increased `maximumStepHeight` from 0.20 to 0.35 world units.
  - Step traversal verifies raised capsule clearance and samples a valid supporting surface on top or immediately ahead with normal within walkable slope (<= 45°).
  - True walls, ceilings, and over-limit slopes remain blocking; vertical wall climbing is prevented.
- **Setup Menu (`FixCharacterMovementCollision`)**:
  - Rerunning the menu applies the 0.35 step allowance and anti-slide to the four character prefabs and Level 01 actors.
