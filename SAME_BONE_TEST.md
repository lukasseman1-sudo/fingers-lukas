# MSFS — absolute cursor position on the SAME tracked bone

This experimental, opt-in mode is based on the functioning `msfs-direct-input` branch.

## What is preserved
- `LeapHandler.cs` is **not changed**. It uses the exact same Ultraleap tracked point (currently `hand.WristPosition`), mounting profile, left/right selection, and tracking events.
- The regular `START_MSFS.cmd` and its relative cursor mode remain untouched.
- The existing Pimax OpenXR head-pose receiver and calibration for sensor pitch, Y offset and 2D/3D mode remain in use.

## What changes
- Run `START_MSFS_SAME_BONE.cmd` to enable `FINGERS_MSFS_SAME_BONE_ABSOLUTE=1`.
- Mouse position is now calculated from the **current head-compensated world position of the original tracked point**, relative to a persistent initial world-to-screen anchor.
- When hand tracking is lost, the pointer holds; after reacquisition, it maps the CURRENT tracked position back onto screen instead of resuming accumulated mouse deltas.
- The anchor is NOT reset by tracking loss. Each hand has its own retained anchor.
- The existing `Ctrl+Shift+F12` or `Ctrl+Shift+F11` cursor-recenter hotkey also resets the absolute anchor, allowing explicit realignment.
- Original speed-based mouse acceleration and deadzone are bypassed while this mode is active; sensitivity X/Y is retained.

## Mandatory conditions and limitations
- The project’s existing `FingersOpenXRLayer.dll` must already be registered and publishing **fresh** Pimax OpenXR head poses to UDP loopback port 39458. Check that the app displays `OpenXR: headset data OK`. Without live head poses this mode freezes rather than inventing coordinates.
- Position is mapped onto the Windows mouse cursor. It does NOT inject a true OpenXR controller `aim pose` into MSFS or raycast onto the 3D cockpit. Physically perfect overlap of a bone and the native 3D VR interaction cursor cannot be guaranteed by this method.
- The physical-to-screen scale reuses the original MSFS sensitivity parameters and needs manual calibration.
- The application and OpenXR DLL are untested here with the actual headset, so this is a source-level experimental change, not a VR-validated release.

## Test
1. Back up the working package and its settings, and keep the same OpenXR runtime.
2. Build/download the branch build package. Do NOT replace the existing working install first.
3. Start MSFS in VR normally, with the original successful head-pose layer configuration.
4. Run `START_MSFS_SAME_BONE.cmd`; verify `OpenXR: headset data OK`.
5. Bring your hand into view. Move it, then hide it from the Ultraleap and reacquire it in a new position: cursor should move to the mapped position immediately.
6. Use `Ctrl+Shift+F12` to recenter and re-anchor if the initial alignment is offset. Close the new build and resume the old `START_MSFS.cmd` to roll back.
