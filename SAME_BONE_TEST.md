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

## Very low master sensitivity control (cockpit only)

The experimental `START_MSFS_COCKPIT_TEST.cmd` now starts with **5%** of
the previous cockpit cursor speed, meaning **20x slower on every axis**.
This multiplier applies to head-compensated hand movement in X, Y and
the Z/depth contribution to Y; original 3D hand point and headset
compensation are unchanged.

Use the new **CELKOVA CITLIVOST KOKPITU (vsetky smery)** slider in Fingers:

* Full range: **0.1% to 100%**, logarithmic so extremely low speeds
  are easy to adjust. 0.1% is 1000x slower than the previous version.
* Default: **5% = 20x slower**.
* PageUp/PageDown: approximately **2x** coarse adjustment.
* Buttons **- 2x pomalsie** / **+ 2x rychlejsie**: 2x coarse changes.
* The live label shows the actual percentage and slowing factor.
* Slider value saves to `%APPDATA%\\FingersMSFS\\tuning.txt`
  as `CockpitSensitivitySlider` and reloads next time.
* Only applies to the opt-in cockpit relative mouse mode; the older
  `START_MSFS_SAME_BONE.cmd` keeps its original sensitivity.

In cockpit test mode, source-coordinate changes retain subpixel
precision in an accumulator before mouse input, avoiding the artificial
pixel rounding deadzone at 0.1%-5%. MSFS cockpit itself may still
apply its own mouse sensitivity settings.

## MSFS 2024: menus work but the VR cockpit only responds to a physical mouse

The original `START_MSFS_SAME_BONE.cmd` uses `SetCursorPos` (Windows absolute pointer
position). This works in many menus but the VR cockpit uses a 3D raycast cursor
steered by mouse *movement*, not the same absolute 2D cursor.

Use the **separate experimental** `START_MSFS_COCKPIT_TEST.cmd`:

1. Close all running FingersApp instances before switching.
2. Keep the same Ultraleap/Pimax/OpenXR headset setup. Load an aircraft in VR.
3. Run `START_MSFS_COCKPIT_TEST.cmd`, then return focus to the MSFS cockpit (Alt+Tab
   or click the simulator window with your physical mouse).
4. Confirm **Leap: Connected**, `Leap snimky` rising, and
   **OpenXR: headset data OK**.
5. Move your tracked wrist while pointing at a cockpit switch. Does the
   simulator's native VR cursor/raycast move? Try interacting using the physical
   mouse click as a separate test.
6. If the cockpit pointer does not move, test MSFS 2024 **Settings > Flight
   Interface > Cockpit Interaction System > Legacy** and return to VR via
   **Ctrl+Tab** if input was lost after using the settings menu.
7. If the app works in menus only, do **not** treat this as a proven fix:
   MSFS might use an input path that ignores synthetic mouse movement.
   A virtual HID mouse or true OpenXR controller input would need a separate
   implementation and test.

This mode **still uses the original Ultraleap wrist bone**,
head-compensated OpenXR world position, per-hand persistent world anchor, and
calibration. It produces `mouse_event(MOVE)` relative deltas derived from
the world-based absolute target, instead of calling `SetCursorPos`.
A tracking interruption preserves its last target. The `Ctrl+Shift+F11/F12`
hotkeys explicitly re-anchor. Exact 3D fingertip-over-switch raycast alignment
is not guaranteed; this only tests whether native VR cockpit input starts
responding. The physical mouse can also move the native cursor and introduce
a mismatch.

**Rollback:** Close Fingers, run `START_MSFS_SAME_BONE.cmd` again to use the
untouched Windows-pointer mode.

## Ultraleap camera is not connected

The GitHub Actions runner doesn't contain Ultraleap's native `LeapC.dll`.
The `START_MSFS_SAME_BONE.cmd` launcher now copies it from the local
`%ProgramFiles%\\Ultraleap\\LeapSDK\\lib\\x64\\LeapC.dll` if the package lacks it.

* Unzip the entire build to a writable folder, not directly inside the ZIP.
* Launch **START_MSFS_SAME_BONE.cmd**, not the bare EXE.
* If the launcher says `LeapC.dll was not found`, use the official Ultraleap
  SDK that matches your installed tracking service, or copy the matching
  `LeapC.dll` from your previously working Fingers application into this folder.
* Confirm the camera is visible in the Ultraleap Visualizer and the
  Ultraleap Hand Tracking Service is running before testing Fingers.
* If the app shows `Connecting...`, look at `Leap snimky`: zero means
  no tracking frames arrived. A working camera/SDK is still required.
* This patch fixes native-library discovery, not a disconnected USB camera,
  stopped Ultraleap service, incompatible DLL, or headset OpenXR layer.

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
