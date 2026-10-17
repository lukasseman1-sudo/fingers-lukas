# Head-independent hand tracking - implementation status

The existing relative mouse mode follows hand *angles in the headset frame*.
That means turning the head causes a cursor delta even when a hand is world-stationary.

This branch now contains `HeadIndependentHand.cs`: the world-space
transformation necessary to remove that rotation, **provided that we obtain
head position/orientation at the matching measurement time**.

OpenXR API layers can intercept `xrLocateViews` or `xrLocateSpace` from
MSFS's existing Pimax OpenXR session and publish time-stamped tracking data.
A separate OpenXR session is not guaranteed to give access to the game's pose.
The API layer, IPC communication, time synchronization and headset/sensor
calibration are **not implemented yet**.

Until a verified pose stream exists, the live Fingers cursor code deliberately
continues to use its existing relative-hand mode. We should never fabricate
head pose from cursor movement: that reproduces the old feedback-loop problem.

Coordinate conventions must be calibrated before activation: Leap sensor
coordinates are not automatically the same as OpenXR LOCAL axes.

Validation should cover: (1) stationary hand, rotated head (world result
remains constant), (2) stationary head, moving hand (world result changes),
(3) tracking loss / stale pose (reject), (4) click and hand reacquisition.

Do not install an untested implicit OpenXR API layer globally. Prefer an
application-scoped deployment with an easy disable/rollback path.
