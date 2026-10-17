using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.UI.Xaml.Controls.Primitives;

namespace FingersApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Fingers fingers;
        private HwndSource hotkeySource;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private void InstallHandHotkeys(object sender, EventArgs e)
        {
            hotkeySource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            if (hotkeySource == null) return;
            hotkeySource.AddHook(HandHotkeyHook);
            for (int id = 1; id <= 3; id++)
                RegisterHotKey(hotkeySource.Handle, 5300 + id, 0x4003, (uint)(0x30 + id));
        }
        private IntPtr HandHotkeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // wParam is pointer-sized; converting arbitrary window messages to
            // Int32 can overflow on 64-bit Windows. Inspect only WM_HOTKEY.
            if (msg == 0x0312)
            {
                long id = wParam.ToInt64();
                if (id >= 5301 && id <= 5303)
                {
                    MsfsHandModeCombo.SelectedIndex = (int)(id - 5301);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }
        private void RemoveHandHotkeys(object sender, EventArgs e)
        {
            if (hotkeySource == null) return;
            for (int id = 1; id <= 3; id++)
                UnregisterHotKey(hotkeySource.Handle, 5300 + id);
            hotkeySource.RemoveHook(HandHotkeyHook);
        }

        private bool closing = false;
        private DispatcherTimer poseStatusTimer;
        //Thread FingersThread;

        [DllImport("user32.dll")]
        internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        public MainWindow()
        {
            CheckMultipleInstanceofApp();
            InitializeComponent();
            SourceInitialized += InstallHandHotkeys;
            Closed += RemoveHandHotkeys;
            MsfsTuningPanel.Visibility = Visibility.Visible; // Show tuning controls even when launched directly; MSFS mode itself still requires START_MSFS.cmd.
            MsfsHorizontalSlider.Value = ReadTuning("Horizontal", 2);
            MsfsVerticalSlider.Value = ReadTuning("Vertical", 4);
            MsfsCompensationSlider.Value = ReadTuning("Compensation", 0);
            MsfsAccelerationSlider.Value = ReadTuning("Acceleration", 0.75);
            MsfsVerticalHeadTrimSlider.Value = ReadTuning("VerticalHeadTrim", 0);
            Msfs3DCheckBox.IsChecked = ReadTuning("3DMode", 1) != 0;
            MsfsSensorPitchSlider.Value = ReadTuning("SensorPitch", 0);
            MsfsSensorYSlider.Value = ReadTuning("SensorY", 0);
            MsfsHandModeCombo.SelectedIndex = (int)Math.Max(0, Math.Min(2, ReadTuning("HandMode", 0)));
            poseStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            poseStatusTimer.Tick += (s, e) =>
            {
                MsfsPoseStatus.Text = fingers != null && fingers.HasFreshHeadPose ? "OpenXR: headset data OK" : "OpenXR: bez dat headsetu";
                if (fingers != null)
                    MsfsInputStatus.Text = "MSFS mode: " + (fingers.IsMsfsInputMode ? "ANO" : "NIE")
                        + " | Leap snimky: " + fingers.LeapFramesSeen
                        + " | L: " + fingers.LeapLeftSeen + " P: " + fingers.LeapRightSeen
                        + " | Mouse udalosti: " + fingers.MouseMovesSent;
            };
            poseStatusTimer.Start();
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (closing)
            {
                return;
            }

            e.Cancel = true;
            if (poseStatusTimer != null) poseStatusTimer.Stop();
            if (fingers != null)
                await fingers.Closing();
            closing = true;
            Close();
        }

        private bool CheckMultipleInstanceofApp()
        {
            Process[] prc = null;
            string ModName, ProcName;
            Process current = Process.GetCurrentProcess();
            ModName = current.MainModule.ModuleName;
            ProcName = System.IO.Path.GetFileNameWithoutExtension(ModName);
            prc = Process.GetProcessesByName(ProcName);
            if (prc.Length <= 1)
                return false;

            for (int i = 0; i < prc.Length; i++)
            {
                if (prc[i].Id == current.Id) continue;
                IntPtr hWnd = prc[i].MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                    SetForegroundWindow(hWnd);
            }
                
            // Another instance is already running. Do not kill this process silently.
            MessageBox.Show("Fingers uz bezi. Zavri povodnu instanciu pred spustenim novej.", "Fingers", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Environment.Exit(0);
            return true;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Task workerTask = Task.Run(() => { fingers = new Fingers(this); Dispatcher.Invoke(() => { fingers.SetMsfsTuning((float)MsfsHorizontalSlider.Value, (float)MsfsVerticalSlider.Value, (float)MsfsCompensationSlider.Value, (float)MsfsAccelerationSlider.Value, (float)MsfsVerticalHeadTrimSlider.Value); fingers.SetMsfsHandMode(MsfsHandModeCombo.SelectedIndex); fingers.SetMsfs3DMode(Msfs3DCheckBox.IsChecked == true, (float)MsfsSensorPitchSlider.Value, (float)MsfsSensorYSlider.Value); }); });

            /*
            FingersThread = new Thread(() =>
            {
                fingers = new Fingers(this);
            });
            FingersThread.IsBackground = true;
            FingersThread.Start();
            */
        }

        public void SetLeapStatus(String status)
        {
            LeapStatus.Text = status;
        }

        private void SetRingStatus(TextBlock ring, RingStatus status, ulong addr, uint batt)
        {
            if (status == RingStatus.CONNECTED)
            {
                ring.Text = addr.ToString("X").Substring(8);
                float volts = (batt / 100f);
                ring.ToolTip = (batt > 0) ? String.Format("{0:N2}v", volts) : "";
            }
            else
            {
                ring.Text = status == RingStatus.CONNECTING ? "CONNECTING" : "SEARCHING";
                ring.ToolTip = "";
            }
        }
        public void SetLeftRingStatus(RingStatus status, ulong addr, uint batt)
        {
            SetRingStatus(LeftRingStatus, status, addr, batt);
        }
        public void SetRightRingStatus(RingStatus status, ulong addr, uint batt)
        {
            SetRingStatus(RightRingStatus, status, addr, batt);
        }

        public void SetSensitivityDisplay(float sens)
        {
            Debug.WriteLine("Updating slider: {0}", sens);
            SensitivitySlider.Value = (double)sens;
        }

        public void SetButtonStatus(LoopButton b, Boolean pressed, Boolean rightHand)
        {
            ImageSource img = (ImageSource)(pressed ? FindResource("IndicatorOn") : FindResource("IndicatorOff"));

            if (rightHand)
            {
                if (b == LoopButton.CENTER) RightRingBtnCenter.Source = img;
                else if (b == LoopButton.FWD) RightRingBtnFwd.Source = img;
                else if (b == LoopButton.BACK) RightRingBtnBack.Source = img;
                else if (b == LoopButton.UP) RightRingBtnUp.Source = img;
                else if (b == LoopButton.DOWN) RightRingBtnDown.Source = img;
            } else
            {
                if (b == LoopButton.CENTER) LeftRingBtnCenter.Source = img;
                else if (b == LoopButton.FWD) LeftRingBtnFwd.Source = img;
                else if (b == LoopButton.BACK) LeftRingBtnBack.Source = img;
                else if (b == LoopButton.UP) LeftRingBtnUp.Source = img;
                else if (b == LoopButton.DOWN) LeftRingBtnDown.Source = img;
            }
        }

        public void SelectLeapProfile(String name)
        {
            LeapProfileSelector.SelectedIndex =
                LeapProfileSelector.Items.Cast<ComboBoxItem>()
                    .Select(c => (string)c.Content)
                    .ToList()
                    .IndexOf(name);
        }

        private void Swap(object sender, RoutedEventArgs e)
        {
            
            fingers.SwapRings(); // Not threadsafe

            ImageSource img = (ImageSource)FindResource("IndicatorOff");
            LeftRingBtnCenter.Source = img;
            LeftRingBtnFwd.Source = img;
            LeftRingBtnBack.Source = img;
            LeftRingBtnUp.Source = img;
            LeftRingBtnDown.Source = img;
            RightRingBtnCenter.Source = img;
            RightRingBtnFwd.Source = img;
            RightRingBtnBack.Source = img;
            RightRingBtnUp.Source = img;
            RightRingBtnDown.Source = img;
        }

        private void LeapProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;

            String text = (e.AddedItems[0] as ComboBoxItem).Content.ToString();

            // If fingers is null this is getting called from fingers' constructor as it
            // loads the previous data, so we don't need/want to call back to it
            if (text != null && fingers != null)
                fingers.SetLeapProfile(text, false);
        }

        private static string TuningFile
        {
            get
            {
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FingersMSFS");
                return System.IO.Path.Combine(folder, "tuning.txt");
            }
        }

        private static double ReadTuning(string key, double fallback)
        {
            try
            {
                if (!System.IO.File.Exists(TuningFile)) return fallback;
                foreach (string line in System.IO.File.ReadAllLines(TuningFile))
                {
                    string[] pair = line.Split(new[] { '=' }, 2);
                    double value;
                    if (pair.Length == 2 && pair[0] == key &&
                        double.TryParse(pair[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                        return value;
                }
            }
            catch (Exception ex) { Debug.WriteLine("MSFS tuning load: " + ex.Message); }
            return fallback;
        }

        private void SaveTuning()
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(TuningFile));
                System.IO.File.WriteAllLines(TuningFile, new[] {
                    "Horizontal=" + MsfsHorizontalSlider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "Vertical=" + MsfsVerticalSlider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "Compensation=" + MsfsCompensationSlider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "Acceleration=" + MsfsAccelerationSlider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "VerticalHeadTrim=" + MsfsVerticalHeadTrimSlider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "HandMode=" + MsfsHandModeCombo.SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "3DMode=" + (Msfs3DCheckBox.IsChecked == true ? "1" : "0"),
                    "SensorPitch=" + MsfsSensorPitchSlider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "SensorY=" + MsfsSensorYSlider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }
            catch (Exception ex) { Debug.WriteLine("MSFS tuning save: " + ex.Message); }
        }

        private void Msfs3DChanged(object sender, RoutedEventArgs e) { Apply3DSettings(); }
        private void Msfs3DSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { Apply3DSettings(); }
        private void Apply3DSettings()
        {
            if (Msfs3DCheckBox == null || MsfsSensorPitchSlider == null || MsfsSensorYSlider == null) return;
            if (fingers != null) fingers.SetMsfs3DMode(Msfs3DCheckBox.IsChecked == true, (float)MsfsSensorPitchSlider.Value, (float)MsfsSensorYSlider.Value);
            if (MsfsHorizontalSlider != null && MsfsHandModeCombo != null && MsfsVerticalHeadTrimSlider != null) SaveTuning();
        }

        private void MsfsHandModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (MsfsHandModeCombo == null) return;
            if (fingers != null) fingers.SetMsfsHandMode(MsfsHandModeCombo.SelectedIndex);
            if (MsfsHorizontalSlider != null && MsfsVerticalHeadTrimSlider != null) SaveTuning();
        }

        private void MsfsSlidersChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (fingers == null || MsfsHorizontalSlider == null || MsfsVerticalSlider == null || MsfsCompensationSlider == null || MsfsAccelerationSlider == null || MsfsVerticalHeadTrimSlider == null) return;
            fingers.SetMsfsTuning((float)MsfsHorizontalSlider.Value, (float)MsfsVerticalSlider.Value,
                                  (float)MsfsCompensationSlider.Value, (float)MsfsAccelerationSlider.Value,
                                  (float)MsfsVerticalHeadTrimSlider.Value);
            SaveTuning();
        }

        private void SensitivityValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if ((float)e.NewValue == (float)e.OldValue || fingers == null) return;

            fingers.SetSensitivity((float)e.NewValue);
        }
    }
}
