# MSFS 2024 — experimental DCS-style hand point aiming

This experiment is a **separate**, opt-in launcher.
It does NOT change the working `START_MSFS_COCKPIT_TEST.cmd`,
`START_MSFS_SAME_BONE.cmd`, or the original DCS program.

## Why the old MSFS pointer was shifted

The previous MSFS mouse adapter projected the hand's **WORLD X/Y position**
onto an arbitrary Windows screen-origin. It was not the geometric angle
from the headset eye to the actual hand bone.

Original glenmurphy/Fingers **DCS** uses:
- the `TYPE_INDEX` finger's `TYPE_METACARPAL` `NextJoint`;
- a sensor-to-eye Pimax mount position/rotation;
- `atan2(hand.X,hand.Z)` and `-atan2(hand.Y,hand.Z)` to derive
  the direction of the tracked point relative to the eye;
- absolute mouse positioning in the DCS mirror/window.

MSFS 2024 does **not** map its VR cockpit ray identically to an
absolute Windows 2D cursor. The MSFS VR cursor is a ray direction
updated using mouse deltas and cockpit intersection tests, as
documented in the Microsoft Flight Simulator 2024 SDK VR Helpers:
https://docs.flightsimulator.com/msfs2024/flighting/models-and-textures/modeling/aircraft/cockpit/vr-helpers/

## What's implemented in this experimental version

- Tracks both the original DCS index metacarpal joint and the earlier
  MSFS wrist **without changing or overwriting either**.
- Defaults to the **original Fingers DCS index joint**, with a checkbox
  to select wrist instead.
- Applies the existing Pimax sensor mount profile and OpenXR headset
  pose to obtain the bone's real world-space position, then transforms
  it back into the live *head-relative 3D view direction*.
- Uses the exact DCS-style `atan2` angular projection, not a flat
  X/Y world-space projection.
- Sends angle-difference mouse motion through the previously working
  MSFS cockpit-relative input path, preserving very low sensitivity
  and subpixel accumulation.
- Retains the chosen anchor across hand-tracking interruptions.
- Uses `Ctrl+Shift+F12` or the alignment button to capture a new
  pointing reference WITHOUT repositioning the Windows mouse or
  disrupting the actual native VR ray.

## Steps to test

1. Keep your previous working build as a backup. Close all Fingers apps.
2. Download the new DCS Aim package, unzip all files into a NEW folder.
3. Make sure your original `FingersOpenXRLayer.dll` registration works
   in the Pimax native OpenXR runtime. Start MSFS 2024 in VR.
4. Run **`START_MSFS_DCS_AIM_TEST.cmd`**. Verify:
   - Ultraleap shows `Connected`, `Leap snimky` increases.
   - `OpenXR: headset data OK` (live head pose needed).
   - `DCS angle: kotva nastavena` when your hand is tracked.
5. Use your physical mouse to place the *actual MSFS VR cockpit cursor*
   visually over the tracked joint (default **base of the index finger**).
   Keep the hand still.
6. Press **Ctrl+Shift+F12** while Fingers has registered the hotkey.
   You can also click **ZAROVNAT S VR KURZOROM** but that may move the
   simulator's cursor while focusing the app, so use the hotkey in VR.
   This records the angle as the reference without moving the VR ray.
7. Move your tracked hand toward another instrument without moving
   the physical mouse. Try moving your head with the hand fixed in
   the real world and observe the point. Adjust the main master
   sensitivity (0.1%-100%) and horizontal/vertical gains as required.
8. If the cursor or hand drifts apart after you touch the physical
   mouse or switch VR views, re-align by placing the native ray over
   the bone and pressing Ctrl+Shift+F12 again.

## Important limitations (no claim of pixel-perfect guarantee)

This mode still outputs **synthetic Windows relative mouse deltas**.
It does not create an OpenXR motion controller, does not query the true
MSFS VR ray state, and cannot force the 3D cockpit ray to have a chosen
absolute pose. A user's physical mouse or MSFS head-dependent cursor
behaviour may displace the ray and break alignment. Because MSFS does
not expose the ray to this app, the user MUST initially align the ray.
Scale may vary by simulator settings, cockpit view or aircraft.

This version is a geometry-based test, NOT a confirmed fix on actual
Pimax/Ultraleap hardware. For true physical hand-to-cockpit 3D
interaction without manual pointer calibration, a separate OpenXR
virtual motion-controller pose/action emulation implementation would
be required (and tested for MSFS 2024 compatibility).

## Rollback

Close Fingers and launch your previous `START_MSFS_COCKPIT_TEST.cmd`
to use the known-working relative input and its existing cursor mapping.

