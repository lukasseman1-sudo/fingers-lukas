using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Leap;

namespace HandMouse
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
        const uint MOUSEEVENTF_MOVE = 0x0001;
        readonly Controller leap = new Controller();
        readonly object gate = new object();
        readonly Timer refresh = new Timer();
        readonly Label status = new Label();
        readonly CheckBox enabled = new CheckBox();
        readonly CheckBox useHid = new CheckBox();
        readonly HidMouseOutput hidOutput = new HidMouseOutput();
        readonly TrackBar horizontal = new TrackBar();
        readonly TrackBar vertical = new TrackBar();
        readonly TrackBar minGain = new TrackBar();
        readonly TrackBar maxGain = new TrackBar();
        readonly TrackBar accelThreshold = new TrackBar();
        readonly TrackBar deadzone = new TrackBar();
        readonly TrackBar smoothing = new TrackBar();
        Vector2 previous;
        Vector2 filtered;
        Vector2 residue;
        bool previousValid;
        bool filterValid;
        long previousTick;
        DateTime lastTracking = DateTime.MinValue;
        string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HandMouse", "settings.txt");

        public MainForm()
        {
            Text = "HandMouse - Ultraleap";
            Width = 500;
            Height = 660;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(25, 28, 32);
            ForeColor = Color.White;

            enabled.Text = "Ovládať kurzor rukou (F8 zapne/vypne)";
            enabled.Checked = false;
            enabled.SetBounds(20, 16, 430, 32);
            enabled.CheckedChanged += (s, e) => ResetMotion();
            Controls.Add(enabled);

            useHid.Text = "Virtuálna HID myš (vyžaduje ovládač)";
            useHid.SetBounds(20, 50, 445, 28);
            useHid.CheckedChanged += (s, e) => {
                if (useHid.Checked && !hidOutput.Connect()) {
                    useHid.Checked = false;
                    MessageBox.Show("Virtuálny HID ovládač nie je dostupný. Použije sa softvérová myš.", "HandMouse");
                }
                ResetMotion();
            };
            Controls.Add(useHid);
            status.SetBounds(20, 79, 445, 24);
            status.Text = "Ultraleap: čakám na pripojenie";
            Controls.Add(status);

            AddSlider("Horizontálna citlivosť", horizontal, 100, 1, 200, 40);
            AddSlider("Vertikálna citlivosť", vertical, 166, 1, 200, 60);
            AddSlider("Jemný pohyb – minimálna citlivosť", minGain, 232, 1, 100, 20);
            AddSlider("Rýchly pohyb – maximálna citlivosť", maxGain, 298, 10, 400, 180);
            AddSlider("Začiatok zrýchlenia (nižšie = skôr)", accelThreshold, 364, 1, 200, 30);
            AddSlider("Filter mikropohybov", deadzone, 430, 0, 100, 2);
            AddSlider("Vyhladzovanie (vyššie = pokojnejší kurzor)", smoothing, 496, 0, 95, 50);

            var note = new Label { Text = "Sledovaný bod: základňa ukazováka (metakarpál).\nFingers ani Steam nastavenia nie sú potrebné.", AutoSize = false };
            note.SetBounds(20, 566, 460, 40);
            Controls.Add(note);

            LoadSettings();
            foreach (var t in new[] { horizontal, vertical, minGain, maxGain, accelThreshold, deadzone, smoothing })
                t.ValueChanged += (s, e) => SaveSettings();

            leap.FrameReady += OnFrame;
            leap.Device += (s, e) => { try { leap.SetPolicy(Controller.PolicyFlag.POLICY_OPTIMIZE_HMD); leap.SetPolicy(Controller.PolicyFlag.POLICY_BACKGROUND_FRAMES); } catch {} };
            refresh.Interval = 250;
            refresh.Tick += (s, e) =>
            {
                bool tracking;
                lock (gate) tracking = (DateTime.UtcNow - lastTracking).TotalSeconds < 1;
                status.Text = tracking ? "Ultraleap: ruka sledovaná" : "Ultraleap: bez sledovanej ruky";
            };
            refresh.Start();
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F8) { enabled.Checked = !enabled.Checked; e.Handled = true; } };
            FormClosed += (s, e) => { refresh.Stop(); leap.Dispose(); hidOutput.Dispose(); };
        }

        void AddSlider(string caption, TrackBar slider, int y, int lo, int hi, int value)
        {
            Controls.Add(new Label { Text = caption, Left = 20, Top = y, Width = 450, Height = 20 });
            slider.SetBounds(20, y + 20, 440, 45);
            slider.Minimum = lo; slider.Maximum = hi; slider.Value = value;
            slider.TickStyle = TickStyle.None;
            Controls.Add(slider);
        }

        void ResetMotion()
        {
            lock (gate) { previousValid = false; filterValid = false; residue = Vector2.Zero; previousTick = 0; }
        }

        void OnFrame(object sender, FrameEventArgs args)
        {
            try
            {
                Frame frame = args.frame;
                if (frame.Hands.Count == 0) { ResetMotion(); return; }
                // Index metacarpal end near the palm, same tracking point as original Fingers.
                var hand = frame.Hands[0];
                var finger = hand.Fingers.Find(f => f.Type == Finger.FingerType.TYPE_INDEX);
                if (finger == null) { ResetMotion(); return; }
                var joint = finger.Bone(Bone.BoneType.TYPE_METACARPAL).NextJoint;
                // Match Fingers' headset-relative coordinate orientation.
                Vector3 point = new Vector3(-joint[0], -joint[2], joint[1]);
                if (point.Z < 30) { ResetMotion(); return; }
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(() => ProcessPoint(point)));
            }
            catch { ResetMotion(); }
        }

        void ProcessPoint(Vector3 point)
        {
            try
            {
                lock (gate)
                {
                    lastTracking = DateTime.UtcNow;
                    if (!enabled.Checked) { previousValid = false; return; }
                    Vector2 angle = new Vector2(
                        (float)(Math.Atan2(point.X, point.Z) * 180.0 / Math.PI),
                        (float)(-Math.Atan2(point.Y, point.Z) * 180.0 / Math.PI));
                    if (!previousValid) { previous = angle; previousValid = true; previousTick = System.Diagnostics.Stopwatch.GetTimestamp(); return; }
                    Vector2 delta = angle - previous;
                    previous = angle;
                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    float dt = (float)(now - previousTick) / System.Diagnostics.Stopwatch.Frequency;
                    previousTick = now;
                    if (dt <= 0 || dt > .15f || Math.Abs(delta.X) > 10 || Math.Abs(delta.Y) > 10) return;
                    // Smooth velocity before acceleration to avoid amplified tracker noise.
                    float alpha = 1f - smoothing.Value / 110f;
                    filtered = filterValid ? filtered + (delta - filtered) * alpha : delta;
                    filterValid = true;
                    float dz = deadzone.Value / 200f;
                    float x = Math.Abs(filtered.X) > dz ? filtered.X - Math.Sign(filtered.X) * dz : 0;
                    float y = Math.Abs(filtered.Y) > dz ? filtered.Y - Math.Sign(filtered.Y) * dz : 0;
                    float speed = (float)Math.Sqrt(x*x + y*y) / Math.Max(.008f, dt);
                    float start = accelThreshold.Value / 3f;
                    float t = Math.Max(0, Math.Min(1, (speed - start) / 50f));
                    t = t * t * (3 - 2*t);
                    float gain = (minGain.Value / 100f) + ((maxGain.Value / 100f) - (minGain.Value / 100f)) * t;
                    Vector2 movement = new Vector2(x * horizontal.Value / 40f, y * vertical.Value / 40f) * (28f * gain) + residue;
                    int dx = (int)Math.Truncate(movement.X), dy = (int)Math.Truncate(movement.Y);
                    residue = movement - new Vector2(dx, dy);
                    if (dx != 0 || dy != 0) {
                        if (useHid.Checked) {
                            if (!hidOutput.Move(dx, dy)) {
                                enabled.Checked = false;
                                useHid.Checked = false;
                                MessageBox.Show("Spojenie s HID ovládačom zlyhalo. Ovládanie sa vyplo.", "HandMouse");
                            }
                        } else mouse_event(MOUSEEVENTF_MOVE, unchecked((uint)dx), unchecked((uint)dy), 0, UIntPtr.Zero);
                    }
                }
            }
            catch { ResetMotion(); }
        }

        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllLines(settingsPath, new[] {
                    horizontal.Value.ToString(), vertical.Value.ToString(), minGain.Value.ToString(),
                    maxGain.Value.ToString(), accelThreshold.Value.ToString(), deadzone.Value.ToString(),
                    smoothing.Value.ToString() });
            }
            catch {}
        }
        void LoadSettings()
        {
            try {
                if (!File.Exists(settingsPath)) return;
                var rows = File.ReadAllLines(settingsPath);
                var arr = new[] { horizontal, vertical, minGain, maxGain, accelThreshold, deadzone, smoothing };
                for (int i = 0; i < arr.Length && i < rows.Length; i++) {
                    int v;
                    if (int.TryParse(rows[i], out v)) arr[i].Value = Math.Max(arr[i].Minimum, Math.Min(arr[i].Maximum, v));
                }
            } catch {}
        }
    }
}