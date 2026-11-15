using System.Numerics; // Vector

public struct HandData
{
    public bool isLeft;
    public bool isActive;

    // position relative to eye
    // X+ is right
    // Y+ is up
    // Z+ is distance
    public Vector3 pos;

    // Original glenmurphy/Fingers DCS point: index metacarpal NextJoint.
    // The older MSFS wrist point above remains unchanged.
    public Vector3 dcsIndexPoint;

    // Used for dragging; ideally this is the rotation of the hand, but in practice
    // it could just be a position
    public float angle;
}