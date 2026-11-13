using System;

namespace FingersApp.Tracking
{
    /// <summary>
    /// Absolute bone-to-cursor mapping. Call Update with the SAME tracked bone used by
    /// the existing Fingers application, expressed in a stable head-relative coordinate frame.
    /// This class deliberately never integrates deltas.
    /// </summary>
    public sealed class AbsoluteBoneCursorMapper
    {
        public struct Point3
        {
            public double X, Y, Z;
            public Point3(double x, double y, double z) { X = x; Y = y; Z = z; }
        }

        public struct CursorPoint
        {
            public int X, Y;
            public CursorPoint(int x, int y) { X = x; Y = y; }
        }

        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;
        public bool InvertX { get; set; }
        public bool InvertY { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double ReferenceX { get; set; }
        public double ReferenceY { get; set; }

        public bool HasValidTracking { get; private set; }
        public CursorPoint? LastPosition { get; private set; }

        /// <summary>
        /// Returns null while tracking is invalid. On recovery, the FIRST valid
        /// sample immediately determines the absolute position, without interpolation
        /// from the old position or resetting the calibration.
        /// </summary>
        public CursorPoint? Update(bool isTracked, Point3 bone, int width, int height)
        {
            if (!isTracked || !IsFinite(bone.X) || !IsFinite(bone.Y) ||
                !IsFinite(bone.Z) || width <= 0 || height <= 0)
            {
                HasValidTracking = false;
                return null;
            }

            double x = width * 0.5 + (bone.X - ReferenceX) * ScaleX * (InvertX ? -1 : 1) + OffsetX;
            double y = height * 0.5 + (bone.Y - ReferenceY) * ScaleY * (InvertY ? -1 : 1) + OffsetY;
            if (!IsFinite(x) || !IsFinite(y))
            {
                HasValidTracking = false;
                return null;
            }

            var point = new CursorPoint(
                (int)Math.Round(Math.Max(0, Math.Min(width - 1, x))),
                (int)Math.Round(Math.Max(0, Math.Min(height - 1, y))));
            LastPosition = point;
            HasValidTracking = true;
            return point;
        }

        public void TrackingLost() { HasValidTracking = false; }

        private static bool IsFinite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
    }
}
