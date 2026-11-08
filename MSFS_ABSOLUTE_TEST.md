# MSFS 2024 — absolute bone cursor experimental test

This branch builds on **msfs-direct-input**, retaining the existing LeapHandler tracked bone, left/right selection, 2D/3D controls and tuning.

## Test
1. Build the existing FingersApp Windows .NET Framework 4.8 project using its required Ultraleap SDK.
2. Start MSFS 2024, then launch `START_MSFS_ABSOLUTE.cmd` from the executable folder (or set `FINGERS_MSFS=1` and `FINGERS_MSFS_ABSOLUTE=1` before launching).
3. Calibrate the cursor at your initial bone position. Move the tracked hand, hide it from the sensor, and bring it back at a different location. The cursor should jump directly to the absolute mapped location rather than resuming accumulated relative movement.
4. If OpenXR headset pose data is absent or stale, the cursor freezes: absolute mode NEVER falls back to head-relative coordinates, since that would violate world-locking.
5. OpenXR pose is received via HeadPoseReceiver UDP loopback port 39458. A compatible pose publisher running in the actual MSFS OpenXR session is REQUIRED; this repository's receiver alone does not provide headset tracking.

## Caveats
- This is an **experimental source update**, not a compiled or VR-validated release.
- Absolute here means mapping against a retained calibration reference, **not** projecting onto a true 3D cockpit surface. Mapping depends on calibration, OS cursor mapping, and how MSFS handles cursor input.
- The absolute mode intentionally bypasses the original relative acceleration/dead-zone algorithm; sensitivity sliders remain relevant.
- No ring/button bindings or LeapHandler bone selection are changed.
- Existing `centerOnTrackingStart` is disabled in absolute mode to avoid forced recentering.
