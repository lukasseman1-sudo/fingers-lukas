# Absolute bone tracking — integration notes

This branch introduces `AbsoluteBoneCursorMapper`, a standalone C# mapping component.

## Intended behavior
- Keep the **same bone** already selected by the original Fingers input pipeline. Do not substitute the fingertip.
- Provide its position in a **stable head-relative coordinate system** (transform world-space bone coordinates with the current headset pose if necessary).
- When tracking is invalid, do not issue cursor movement commands; leave the OS cursor at its last position.
- On the first valid tracking sample after a loss, set the cursor **directly** to the new absolute position. Do not accumulate deltas, recenter, or interpolate from the last cursor position.
- Persist calibration (`ReferenceX/Y`, `ScaleX/Y`, `OffsetX/Y`, inversion) across tracking interruptions.

## Integration example

```csharp
var mapped = mapper.Update(
    boneIsTracked,
    new AbsoluteBoneCursorMapper.Point3(boneX, boneY, boneZ),
    screenWidth, screenHeight);
if (mapped.HasValue)
{
    // Use the application's existing cursor-output method:
    // SetCursorPosition(mapped.Value.X, mapped.Value.Y);
}
```

**Important:** The mapper is not yet connected to the existing Leap Motion/OpenXR data pipeline or Windows cursor output. The real-world 1:1 relationship requires correct frame transformation and calibration. This commit does not contain a compiled Windows executable or verified VR integration.
