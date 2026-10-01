# Achilles Power Bar Fix Report

- CurrentCharge01 updates continuously from 0 to 1 while Space is held and clamps cleanly at 1.
- Launch speed calculation directly uses CurrentCharge01, ensuring visual and physics parity.
- ProjectileAimHUD updates powerBarFill.fillAmount to launcher.CurrentCharge01 every visible frame.
- Initialized powerBarFill with a fallback white sprite to guarantee UGUI filled clipping works.
- Configured Fill Image with Image.Type.Filled, Horizontal method, Origin Left, and Clockwise.
- Ensured Fill reference connects directly to ProjectileAimHUD/PowerBar/Fill in hierarchy and setup.
- Bar resets cleanly to 0 on key release and cancel events, maintaining 0 when idle or throwing.
- Preserved Throw animation synchronization, prepared wind lifecycle, camera, and physics.
- Updated idempotent Build Achilles Projectile Prototype setup command with UI sprite and fill properties.
