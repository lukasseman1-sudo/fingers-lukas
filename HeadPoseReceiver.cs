using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;

// Pose intake for a future OpenXR API layer. Only accepts loopback packets.
// Protocol: POSE1|x|y|z|qx|qy|qz|qw (meters, world/OpenXR LOCAL coordinates).
// No headset coordinates are invented when the feed is absent or stale.
public sealed class HeadPoseReceiver : IDisposable
{
    private readonly UdpClient client;
    private readonly Thread thread;
    private readonly object gate = new object();
    private TrackedHeadPose latest;
    private long receivedTicks;
    private bool running = true;

    public HeadPoseReceiver(int port = 39458)
    {
        client = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        client.Client.ReceiveTimeout = 300;
        thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "Fingers OpenXR pose receiver" };
        thread.Start();
    }

    public bool TryGetPose(out TrackedHeadPose pose)
    {
        lock (gate)
        {
            pose = latest;
            return pose.PositionValid && pose.OrientationValid &&
                   (DateTime.UtcNow.Ticks - receivedTicks) < TimeSpan.FromMilliseconds(100).Ticks;
        }
    }

    private void ReceiveLoop()
    {
        var sender = new IPEndPoint(IPAddress.Loopback, 0);
        while (running)
        {
            try
            {
                var bytes = client.Receive(ref sender);
                if (!IPAddress.IsLoopback(sender.Address)) continue;
                string[] values = Encoding.ASCII.GetString(bytes).Split('|');
                if (values.Length != 8 || values[0] != "POSE1") continue;
                float[] v = new float[7];
                bool valid = true;
                for (int i = 0; i < v.Length; i++)
                {
                    valid &= float.TryParse(values[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])
                        && !float.IsInfinity(v[i]) && !float.IsNaN(v[i]);
                }
                if (!valid) continue;
                Quaternion q = new Quaternion(v[3], v[4], v[5], v[6]);
                if (q.LengthSquared() < 0.5f || q.LengthSquared() > 1.5f) continue;
                var pose = new TrackedHeadPose {
                    PositionMeters = new Vector3(v[0], v[1], v[2]),
                    Orientation = Quaternion.Normalize(q),
                    PositionValid = true,
                    OrientationValid = true,
                    TimestampTicks = DateTime.UtcNow.Ticks
                };
                lock (gate) { latest = pose; receivedTicks = pose.TimestampTicks; }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }
        }
    }

    public void Dispose() { running = false; client.Close(); thread.Join(1000); }
}
