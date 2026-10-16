# HandMouse Virtual HID driver (experimental source)

This folder contains **UNVERIFIED SOURCE CODE** for a Windows KMDF Virtual
HID Framework (VHF) mouse driver. It is **not a prebuilt, signed, installable
driver**. Build with matching WDK/Visual Studio, validate against Microsoft
VHF driver documentation, and sign for the target Windows system before use.
Do not turn off driver signature enforcement for this experiment.

HandMouse.exe includes an optional *Virtuálna HID myš* checkbox. This requires
the kernel driver to be running and exposing the \\.\HandMouseHid interface.
Until then, leave it unchecked. Normal software mouse mode will still work.

The driver accepts an IOCTL carrying 3 bytes: signed X delta, signed Y delta,
and three button bits. The driver transforms this into the VHF mouse input
report. Device identifiers, security descriptor, device installation INF,
distribution signing, and per-PC installation need to be completed and
validated; no installer is included.

**Important limitations:** VHF exposes a HID device to Windows, but it
does not guarantee that every protected/anti-cheat game accepts it; software
versus hardware input is not a universal distinction. VHF also cannot
distinguish headset motion from hand motion. True head stabilization still
requires synchronized headset pose.

To compile using the WDK, create a KMDF driver project, add HandMouseHid.c,
link vhfkm.lib and the KMDF libraries, and install via an approved signed INF
once created. This repository does NOT automate driver installation.
