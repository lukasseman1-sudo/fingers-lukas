# Experimental native Pimax OpenXR pose capture

**Not production-ready.** This OpenXR API layer intercepts `xrLocateViews` and
sends view orientation/position over loopback UDP (127.0.0.1:39458) to
the existing `HeadPoseReceiver` in Fingers.

The C++ DLL must be built and validated before enabling it in MSFS. It is NOT
loaded automatically by copying it alongside FingersApp.exe. **Do not place
it inside the MSFS install folder or register it system-wide.**

The `FingersOpenXRLayer.json` is an explicit layer manifest. Windows loader
normally discovers explicit layers through a per-user registry key:
`HKCU\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Explicit`, using its complete
path as a value name and DWORD 0 as the value. Then only processes explicitly
enabling `XR_APILAYER_FINGERS_headpose` should load it. Changing global
OpenXR configuration can affect other VR games. Restore the original settings
after testing; no automatic installer is provided until validation.

**Current limitations**:
- Intercepted pose is relative to the `xrLocateViews` base reference space,
  which may not be stable world space. We must validate reference space type.
- Eye center is estimated as a stereo midpoint; tracker mount offset,
  axes and orientation need calibration on Pimax Crystal Super.
- The input feed lacks a shared source timestamp with Ultraleap hand frames.
- OpenXR layer composition, XR loader negotiation and in-simulator stability
  have not yet been tested.
- The old hand-relative cursor behavior remains the fallback when pose packets
  are missing/stale.

Never claim head compensation is working until a real stationary-world-hand,
moving-head test passes in the MSFS 2024 cockpit.
