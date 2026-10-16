using System;
using System.Numerics;

// Math-only building block for head-independent hand tracking.
// This does NOT obtain an OpenXR headset pose: callers must supply a valid,
// time-correlated head pose from the MSFS OpenXR session.
public struct TrackedHeadPose
{
    public Vector3 PositionMeters;
    public Quaternion Orientation;
    public long TimestampTicks;
    public bool PositionValid;
    public bool OrientationValid;
}

public static class HeadIndependentHand
{
    // Leap hand coordinates are millimeters relative to the headset-mounted sensor.
    // sensorFromHead meters and sensorRotationFromHead are calibration values.
    // Returns coordinates in the OpenXR LOCAL world frame (meters).
    public static bool TryToWorld(
        Vector3 handSensorMillimeters,
        TrackedHeadPose head,
        Vector3 sensorFromHeadMeters,
        Quaternion sensorRotationFromHead,
        out Vector3 handWorldMeters)
    {
        handWorldMeters = Vector3.Zero;
        if (!head.PositionValid || !head.OrientationValid ||
            !IsFinite(handSensorMillimeters) || !IsFinite(head.PositionMeters) ||
            !IsFinite(head.Orientation) || !IsFinite(sensorRotationFromHead) ||
            head.Orientation.LengthSquared() < 0.000001f ||
            sensorRotationFromHead.LengthSquared() < 0.000001f)
            return false;

        var handSensorMeters = handSensorMillimeters * 0.001f;
        var handHead = sensorFromHeadMeters +
                       Vector3.Transform(handSensorMeters, Quaternion.Normalize(sensorRotationFromHead));
        handWorldMeters = head.PositionMeters +
                          Vector3.Transform(handHead, Quaternion.Normalize(head.Orientation));
        return IsFinite(handWorldMeters);
    }

    private static bool IsFinite(Vector3 p) =>
        Valid(p.X) && Valid(p.Y) && Valid(p.Z);
    private static bool IsFinite(Quaternion q) =>
        Valid(q.X) && Valid(q.Y) && Valid(q.Z) && Valid(q.W);
    private static bool Valid(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}
