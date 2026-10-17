using System;
using System.Numerics; // Vector
using System.Windows.Forms; // SystemInformation
using System.Diagnostics; // Debug
using System.Threading.Tasks;

public enum RingStatus
{
    SEARCHING = 1,
    CONNECTING = 2,
    CONNECTED = 4,
}

public class Fingers
{
    // DCS translates mouse movement over its window into a 2D plane in the game world - you can see
    // this window when you bring up the ESC menu where it is fixed in place, while it follows the
    // head position at other times (this is why you get two different mouse behaviors). A mouse
    // cursor in a corner of the DCS window always translates into the same position in-game,
    // regardless of the player's FOV or the size/ratio of the main DCS window, so we need to
    // compensate for the stretching DCS does to map input over its desktop window of varying size and
    // aspect ratio to its inner window of fixed size and aspect ratio.
    //
    // This virtual screen appears to be 100 degrees wide with 16:9 aspect ratio
    private static float inputScreenRatio = 16f / 10f;
    private static float inputScreenWidthDegrees = 135f;
    private static float inputScreenHeightDegrees = inputScreenWidthDegrees / inputScreenRatio;

    // When choosing between two hands, how many degrees we should bias towards sticking with the
    // currently tracked hand.
    private static float overlap = 1;

    // Scroll tracking
    // DCS uses both individual events (for discrete things like channel selectors) as well as amounts
    // for analog things like brightness controls - need to strike a balance between making one too
    // sensitive and the other too insensitive
    private static int ScrollDetentAmount = 750;


    // Internal variables
    // The inputScreenRatio setup is translated using inputAngleScale, which is set in 
    // HandleDCSWindow; here are some reasonable default values
    Vector2 inputAngleScale = new Vector2(16.6f, 16.6f);

    Vector4 DCSWindow = new Vector4(0, 0, 0, 0);

    public volatile int LeapFramesSeen;
    public volatile int LeapLeftSeen;
    public volatile int LeapRightSeen;
    public volatile int MouseMovesSent;
    public bool IsMsfsInputMode { get { return msfsMode; } }
    public void ReportLeapFrame(bool left, bool right)
    {
        System.Threading.Interlocked.Increment(ref LeapFramesSeen);
        LeapLeftSeen = left ? 1 : 0;
        LeapRightSeen = right ? 1 : 0;
    }
    // The last hand we tracked
    HandData currentHand;

    // MSFS mode sends relative movement straight from hand tracking (no cursor feedback loop).
    readonly bool mouseOnlyMode = Environment.GetEnvironmentVariable("FINGERS_MOUSE_ONLY") == "1";
    readonly bool msfsMode = Environment.GetEnvironmentVariable("FINGERS_MSFS") == "1" || Environment.GetEnvironmentVariable("FINGERS_MOUSE_ONLY") == "1";
    Vector2 previousMsfsAngle;
    bool previousMsfsAngleValid = false;
    Vector2 msfsRemainder;
    Vector2 msfsDeadzoneAccumulator;
    const float MsfsPixelsPerDegree = 28f;
    volatile float msfsHorizontal = 2f;
    volatile float msfsVertical = 4f;
    volatile float msfsDeadzone = 0f;
    volatile float msfsAcceleration = 0.75f;
    long msfsLastTick = 0;
    HeadPoseReceiver headPoseReceiver;
    public bool HasFreshHeadPose { get { TrackedHeadPose h; return headPoseReceiver != null && headPoseReceiver.TryGetPose(out h); } }
    Vector3 previousWorldHand;
    bool previousWorldHandValid = false;
    float previousHeadPitchDegrees;
    readonly object verticalLogLock = new object();
    long lastVerticalLogTicks = 0;
    void LogVertical(float pitch, float handY, float worldY, float deltaY, bool poseValid)
    {
        long now = DateTime.UtcNow.Ticks;
        if (now - lastVerticalLogTicks < TimeSpan.FromMilliseconds(100).Ticks) return;
        lastVerticalLogTicks = now;
        try {
            string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FingersMSFS", "vertical_diagnostics.csv");
            lock(verticalLogLock) {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                bool header = !System.IO.File.Exists(path);
                using (var writer = new System.IO.StreamWriter(path, true)) {
                    if (header) writer.WriteLine("utc_ticks,pose_valid,head_pitch_deg,hand_head_y_mm,hand_world_y_m,cursor_delta_y_deg");
                    writer.WriteLine(string.Join(",", now, poseValid ? "1" : "0", pitch.ToString(System.Globalization.CultureInfo.InvariantCulture), handY.ToString(System.Globalization.CultureInfo.InvariantCulture), worldY.ToString(System.Globalization.CultureInfo.InvariantCulture), deltaY.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
        } catch (Exception ex) { Debug.WriteLine("Vertical diagnostics: " + ex.Message); }
    }
    bool previousHeadPitchValid = false;
    volatile float msfsVerticalHeadTrim = 0f;
    public void SetMsfsTuning(float horizontal, float vertical, float compensation, float acceleration, float verticalHeadTrim)
    {
        msfsHorizontal = horizontal;
        msfsVertical = vertical;
        msfsDeadzone = compensation;
        msfsAcceleration = acceleration;
        msfsVerticalHeadTrim = verticalHeadTrim;
    }

    public FingersApp.MainWindow ui;

    Vector2 screenCenter;
    Vector2 resetPoint;

    ulong leftRingAddr = 0;
    ulong rightRingAddr = 0;
    uint leftRingBatt = 0;
    uint rightRingBatt = 0;

    bool leftButtonDown = false;
    bool rightButtonDown = false;
    long lastClicked = 0;
    bool cursorEnabled = true;
    float verticalOffset = 0;

    LoopListener loop;
    LeapHandler leap;

    public Fingers(FingersApp.MainWindow mainWindow)
    {
        ui = mainWindow;

        // Configuration variables
        screenCenter = new Vector2(SystemInformation.PrimaryMonitorSize.Width / 2, SystemInformation.PrimaryMonitorSize.Height / 2);

        resetPoint = new Vector2(screenCenter.X, screenCenter.Y * (float)1.25);

        if (!msfsMode) DCS.Monitor(this);
        else {
            Debug.WriteLine("Fingers MSFS relative input mode active");
            try { if (!mouseOnlyMode) headPoseReceiver = new HeadPoseReceiver(); }
            catch (Exception ex) { Debug.WriteLine("OpenXR pose receiver unavailable: " + ex.Message); }
        }

        loop = new LoopListener(this);
        leap = new LeapHandler(this);
        SetLeapProfile(FingersApp.Properties.Settings.Default.LeapProfile, true);

        SetSensitivity(FingersApp.Properties.Settings.Default.Sensitivity);
        ui.Dispatcher.Invoke(() => { ui.SetSensitivityDisplay(FingersApp.Properties.Settings.Default.Sensitivity); });

        // Keep this process running
        Console.ReadLine();
    }

    public async Task<bool> Closing()
    {
        if (headPoseReceiver != null) headPoseReceiver.Dispose();
        await loop.Closing();
        return true;
    }

    public void HandleDCSWindow(Vector4 dim)
    {
        DCSWindow = dim;
        Debug.WriteLine("DCS Window Size Adjustment: {0}", dim);
        UpdateAngleScale();
    }

    public void UpdateAngleScale()
    {
        inputAngleScale = new Vector2(DCSWindow.W / inputScreenWidthDegrees,
                                      DCSWindow.Z / inputScreenHeightDegrees);
        Debug.WriteLine("New inputAngleScale: {0}", inputAngleScale);
    }

    public void SetSensitivity(float sens)
    {
        Debug.WriteLine("New sens: {0}", sens);
        inputScreenWidthDegrees = sens;
        inputScreenHeightDegrees = inputScreenWidthDegrees / inputScreenRatio;
        UpdateAngleScale();

        if (sens != FingersApp.Properties.Settings.Default.Sensitivity)
        {
            FingersApp.Properties.Settings.Default.Sensitivity = sens;
            FingersApp.Properties.Settings.Default.Save();
        }
    }

    // Get the angle of the position (in HandData position) relative to the eye in degrees
    public Vector2 GetRelativeAngle(ref Vector3 pos)
    {
        Vector2 angle;
        angle.X = (float)Math.Atan2(pos.X, pos.Z) * 180 / (float)Math.PI;
        angle.Y = -(float)Math.Atan2(pos.Y, pos.Z) * 180 / (float)Math.PI;
        return angle;
    }

    public Vector2 GetScreenPosition(Vector3 pos)
    {
        Vector2 angle = GetRelativeAngle(ref pos);

        return new Vector2(
          angle.X * inputAngleScale.X,
          (angle.Y + verticalOffset) * inputAngleScale.Y
        );
    }

    public bool IsDragging()
    {
        return (leftButtonDown || rightButtonDown);
    }

    volatile bool msfs3DMode = true;
    volatile float msfsSensorPitch = 0f;
    volatile float msfsSensorY = 0f;
    public void SetMsfs3DMode(bool enabled, float sensorPitch, float sensorY)
    {
        msfs3DMode = enabled;
        msfsSensorPitch = sensorPitch;
        msfsSensorY = sensorY;
        DisengageHand();
    }
    volatile int msfsHandMode = 0; // 0=left, 1=right, 2=both
    public void SetMsfsHandMode(int mode) { msfsHandMode = Math.Max(0, Math.Min(2, mode)); DisengageHand(); }

    public HandData GetActiveHand(HandData left, HandData right)
    {
        // In MSFS mode only the left hand is allowed to control the cursor.
        // Never fall back to the right hand when left tracking is lost.
        if (msfsMode && msfsHandMode == 0) return left.isActive ? left : new HandData() { isActive = false, isLeft = true };
        if (msfsMode && msfsHandMode == 1) return right.isActive ? right : new HandData() { isActive = false, isLeft = false };
        HandData activeHand = new HandData() { isActive = false };

        // Match to existing hand if we're in some kind of drag event (so we don't snap to another
        // hand partway through).
        if (IsDragging() && currentHand.isActive)
        {
            if (currentHand.isLeft && left.isActive)
                return left;
            else if (!currentHand.isLeft && right.isActive)
                return right;
            else
                return activeHand;
        }

        if (!left.isActive && right.isActive)
            return right;
        else if (left.isActive && !right.isActive)
            return left;
        else if (left.isActive && right.isActive)
        {
            // Figure out which hand to focus on by distance away from the middle
            float l = Math.Abs(GetRelativeAngle(ref left.pos).X);
            float r = Math.Abs(GetRelativeAngle(ref right.pos).X);

            // If we're currently tracking a hand, bias away from the other hand by a certain amount of
            // angle (defined in overlap)
            if (currentHand.isActive && currentHand.isLeft)
                r += overlap;
            else if (currentHand.isActive && !currentHand.isLeft)
                l += overlap;
            return (l < r) ? left : right;
        }

        return activeHand;
    }

    public void HandleLeapConnected()
    {
        ui.Dispatcher.Invoke(() =>
        {
            ui.SetLeapStatus("Connected");
        });
    }
    public void HandleLeapDisconnected()
    {
        ui.Dispatcher.Invoke(() =>
        {
            ui.SetLeapStatus("Connecting...");
        });
    }

    public void HandleHands(HandData left, HandData right)
    {
        HandData activeHand = GetActiveHand(left, right);

        if (!activeHand.isActive && currentHand.isActive)
        {
            DisengageHand();
            return;
        }

        currentHand = activeHand;
        if (!activeHand.isActive) return;

        if (msfsMode)
        {
            // Relative angle is tracked before any Windows cursor movement is generated.
            // Never read back the Windows cursor position, preventing recirculation.
            Vector2 angle = GetRelativeAngle(ref activeHand.pos);
            Vector2 delta = Vector2.Zero;
            bool sampleValid = false;
            TrackedHeadPose head = new TrackedHeadPose();
            bool poseValid = headPoseReceiver != null && headPoseReceiver.TryGetPose(out head);
            if (poseValid && msfs3DMode)
            {
                Vector3 worldHand;
                if (HeadIndependentHand.TryToWorld(new Vector3(activeHand.pos.X, activeHand.pos.Y, -activeHand.pos.Z), head,
                                                   new Vector3(0f, msfsSensorY * 0.001f, 0f),
                                                   Quaternion.CreateFromAxisAngle(Vector3.UnitX, msfsSensorPitch * (float)Math.PI / 180f),
                                                   out worldHand))
                {
                    if (previousWorldHandValid)
                    {
                        Vector3 diff = worldHand - previousWorldHand;
                        delta = new Vector2(diff.X, -diff.Y) * 114.59f;
                        sampleValid = true;
                    }
                    // Trim residual pitch-induced Y motion without changing X.
                    Vector3 forward = Vector3.Transform(new Vector3(0f, 0f, -1f), head.Orientation);
                    float pitch = (float)(Math.Atan2(forward.Y, Math.Sqrt(forward.X * forward.X + forward.Z * forward.Z)) * 180.0 / Math.PI);
                    if (previousHeadPitchValid && sampleValid)
                    {
                        float pitchDelta = pitch - previousHeadPitchDegrees;
                        if (Math.Abs(pitchDelta) < 15f)
                            delta.Y += pitchDelta * msfsVerticalHeadTrim;
                    }
                    previousHeadPitchDegrees = pitch;
                    previousHeadPitchValid = true;
                    LogVertical(pitch, activeHand.pos.Y, worldHand.Y, delta.Y, true);
                    previousWorldHand = worldHand;
                    previousWorldHandValid = true;
                    // Do not mix head-local angle with world-space delta.
                    previousMsfsAngleValid = false;
                }
                else poseValid = false;
            }
            if (!poseValid || !msfs3DMode)
            {
                LogVertical(0f, activeHand.pos.Y, 0f, delta.Y, false);
                previousWorldHandValid = false;
                previousHeadPitchValid = false;
                if (previousMsfsAngleValid) { delta = angle - previousMsfsAngle; sampleValid = true; }
                previousMsfsAngle = angle;
                previousMsfsAngleValid = true;
            }

            if (sampleValid)
            {
                // A tracker reacquisition or hand switch must not cause a huge cursor jump.
                if (Math.Abs(delta.X) < 12f && Math.Abs(delta.Y) < 12f)
                {
                    // A 2D radial dead zone with accumulated movement.
                    // Do not discard each small frame independently: deliberate slow
                    // movements must eventually cross the dead-zone boundary.
                    msfsDeadzoneAccumulator += delta;
                    float deadzone = Math.Max(0f, msfsDeadzone);
                    float traveled = msfsDeadzoneAccumulator.Length();
                    if (traveled <= deadzone)
                    {
                        delta = Vector2.Zero;
                    }
                    else if (deadzone > 0f)
                    {
                        Vector2 direction = msfsDeadzoneAccumulator / traveled;
                        delta = direction * (traveled - deadzone);
                        msfsDeadzoneAccumulator = direction * deadzone;
                    }
                    else
                    {
                        delta = msfsDeadzoneAccumulator;
                        msfsDeadzoneAccumulator = Vector2.Zero;
                    }
                    // Nonlinear acceleration based on angular speed, not distance from center.
                    // Small, slow corrections remain precise; deliberate fast motions travel farther.
                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    float dt = msfsLastTick == 0 ? 0.016f : (float)(now - msfsLastTick) / System.Diagnostics.Stopwatch.Frequency;
                    msfsLastTick = now;
                    dt = Math.Max(0.005f, Math.Min(dt, 0.10f));
                    float angularSpeed = delta.Length() / dt;
                    float t = Math.Max(0f, Math.Min(1f, (angularSpeed - 3f) / 32f));
                    t = t * t * (3f - 2f * t);
                    float blend = msfsAcceleration;
                    float gain = (1f - blend) + blend * (0.20f + 1.80f * t);
                    Vector2 movement = new Vector2(delta.X * msfsHorizontal, delta.Y * msfsVertical) * (MsfsPixelsPerDegree * gain) + msfsRemainder;
                    int dx = (int)Math.Truncate(movement.X);
                    int dy = (int)Math.Truncate(movement.Y);
                    msfsRemainder = movement - new Vector2(dx, dy);
                    if (dx != 0 || dy != 0) { Winput.MoveMouseRelative(dx, dy); System.Threading.Interlocked.Increment(ref MouseMovesSent); }
                }
            }
        }
        else SetCursorPos(GetScreenPosition(activeHand.pos));
    }

    // Called when the current hand stops being active; currently we leave left/right mouse buttons
    // down because a user might have them down for other reasons (e.g. holding temporary switches)
    private void DisengageHand()
    {
        previousMsfsAngleValid = false;
        msfsRemainder = Vector2.Zero;
        msfsDeadzoneAccumulator = Vector2.Zero;
        msfsLastTick = 0;
        previousWorldHandValid = false;
        previousHeadPitchValid = false;
        currentHand.isActive = false;
    }

    private void ToggleVerticalOffset()
    {
        verticalOffset = (verticalOffset == 0) ? 12 : 0;
    }

    private void ToggleCursorEnabled()
    {
        cursorEnabled = !cursorEnabled;
    }

    private long GetTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void HandleLoopConnecting(ulong addr)
    {
        if (leftRingAddr != 0 && rightRingAddr != 0)
        {
            return;
        }
        else if (leftRingAddr != 0 || FingersApp.Properties.Settings.Default.RightRingID == addr)
        {
            if (rightRingAddr != addr) ui.Dispatcher.Invoke(() =>
            {
                ui.SetRightRingStatus(RingStatus.CONNECTING, 0, 0);
            });
        }
        else if (leftRingAddr != addr) 
        {
            ui.Dispatcher.Invoke(() =>
            {
                ui.SetLeftRingStatus(RingStatus.CONNECTING, 0, 0);
            });
        }
    }

    public void HandleLoopConnected(ulong addr)
    {
        String addrString = addr.ToString("X");
        if (leftRingAddr != 0 || addr.Equals(FingersApp.Properties.Settings.Default.RightRingID))
        {
            rightRingAddr = addr;
            FingersApp.Properties.Settings.Default.RightRingID = addr;
            FingersApp.Properties.Settings.Default.Save();
        }
        else
        {
            leftRingAddr = addr;
            FingersApp.Properties.Settings.Default.LeftRingID = addr;
            FingersApp.Properties.Settings.Default.Save();
        }
        UpdateRingStatus();
    }

    public void HandleLoopDisconnected(ulong addr)
    {
        String addrString = addr.ToString("X");

        if (leftRingAddr == addr)
        {
            leftRingAddr = 0;
            leftRingBatt = 0;
        }
        if (rightRingAddr == addr)
        {
            rightRingAddr = 0;
            rightRingBatt = 0;
        }
        UpdateRingStatus();
    }

    public void SetLeapProfile(String name, bool updateUI)
    {
        if (name.Equals("Joculus"))
        {
            leap.SetProfile(5, 0, 0, 0, -18, 100);
        }
        else if (name.Equals("Generic"))
        {
            leap.SetProfile(0, 0, 0, 0, 0, 90);
        }
        else
        {
            name = "Pimax";
            leap.SetProfile(10, 0, 0, 0, -45, 68);
        }

        if (updateUI)
        {
            ui.Dispatcher.Invoke(() =>
            {
                ui.SelectLeapProfile(name);
            });
        }
        FingersApp.Properties.Settings.Default.LeapProfile = name;
        FingersApp.Properties.Settings.Default.Save();
    }

    private void UpdateRingStatus()
    {
        if (leftRingAddr > 0)
            ui.Dispatcher.Invoke(() => { ui.SetLeftRingStatus(RingStatus.CONNECTED, leftRingAddr, leftRingBatt); });
        else
            ui.Dispatcher.Invoke(() => { ui.SetLeftRingStatus(RingStatus.SEARCHING, 0, 0); });

        if (rightRingAddr > 0)
            ui.Dispatcher.Invoke(() => { ui.SetRightRingStatus(RingStatus.CONNECTED, rightRingAddr, rightRingBatt); });
        else
            ui.Dispatcher.Invoke(() => { ui.SetRightRingStatus(RingStatus.SEARCHING, 0, 0); });
    }
    public void SwapRings()
    {
        FingersApp.Properties.Settings.Default.LeftRingID = rightRingAddr;
        FingersApp.Properties.Settings.Default.RightRingID = leftRingAddr;
        leftRingAddr = FingersApp.Properties.Settings.Default.LeftRingID;
        rightRingAddr = FingersApp.Properties.Settings.Default.RightRingID;
        FingersApp.Properties.Settings.Default.Save();

        uint t = leftRingBatt;
        leftRingBatt = rightRingBatt;
        rightRingBatt = t;

        UpdateRingStatus();
    }

    public void HandleLoopBattery(ulong addr, uint batt)
    {
        if (addr == leftRingAddr)
            leftRingBatt = batt;
        else if (addr == rightRingAddr)
            rightRingBatt = batt;

        UpdateRingStatus();
    }

    public void HandleLoopEvent(LoopButton b, Boolean pressed, ulong addr)
    {
        Debug.WriteLine("{0}: {1} {2}", addr.ToString("X"), b, pressed ? "pressed" : "released");
        if (addr == rightRingAddr)
        {
            if (b == LoopButton.FWD) b = LoopButton.BACK;
            else if (b == LoopButton.BACK) b = LoopButton.FWD;
        }

        ui.Dispatcher.Invoke(() =>
        {
            ui.SetButtonStatus(b, pressed, (addr == rightRingAddr));
        });

        /*
        if (pressed && (b == LoopButton.FWD || !cursorEnabled))
        {
            ToggleCursorEnabled();
            return;
        }
        */
        if (b == LoopButton.FWD && pressed)
        {
            ToggleVerticalOffset();
        } 
        else if (b == LoopButton.CENTER && pressed)
        {
            Winput.MouseButton(Winput.MouseEventF.LeftDown);
            leftButtonDown = true;
            lastClicked = GetTime();
        }
        else if (b == LoopButton.CENTER && !pressed)
        {
            Winput.MouseButton(Winput.MouseEventF.LeftUp);
            leftButtonDown = false;
        }
        else if (b == LoopButton.BACK && pressed)
        {
            Winput.MouseButton(Winput.MouseEventF.RightDown);
            rightButtonDown = true;
            lastClicked = GetTime();
        }
        else if (b == LoopButton.BACK && !pressed)
        {
            Winput.MouseButton(Winput.MouseEventF.RightUp);
            rightButtonDown = false;
        }
        else if (b == LoopButton.UP && pressed)
        {
            Scroll(ScrollDetentAmount);
        }
        else if (b == LoopButton.DOWN && pressed)
        {
            Scroll(-ScrollDetentAmount);
        }
    }

    private void Scroll(int amount)
    {
        Winput.ScrollMouse(amount);
    }

    private void SetCursorPos(Vector2 pos)
    {
        if (!cursorEnabled) 
            return;

        // Could use ClipCursor, but direct control is good
        int x = (int)(screenCenter.X + pos.X);
        int y = (int)(screenCenter.Y + pos.Y);

        if (DCSWindow.W == 0)
            { }
        else if (x < DCSWindow.X)
            x = (int)DCSWindow.X;
        else if (x > DCSWindow.X + DCSWindow.W)
            x = (int)(DCSWindow.X + DCSWindow.W);

        if (DCSWindow.Z == 0)
        { }
        else if (y < DCSWindow.Y)
            y = (int)DCSWindow.Y;
        else if (y >= DCSWindow.Y + DCSWindow.Z)
            y = (int)(DCSWindow.Y + DCSWindow.Z - 1);

        Winput.SetCursorPosition(x, y);
    }
}