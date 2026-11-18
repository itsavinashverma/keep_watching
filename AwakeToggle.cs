using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    private const string MutexName = @"Local\AwakeToggle_Clean_v3_Mutex";
    private const string EventName = @"Local\AwakeToggle_Clean_v3_Toggle";
    private const string VideoGroup = "7516b95f-f776-4464-8c53-06167f40cc99";
    private const string DisplayTimeout = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e";
    private const string SleepGroup = "238c9fa8-0aad-41ed-83f4-97be242c8f20";
    private const string SleepTimeout = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da";
    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;
    private static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AwakeToggleClean");
    private static readonly string BackupPath = Path.Combine(DataDirectory, "original-timeouts.txt");

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    [STAThread]
    private static void Main(string[] args)
    {
        bool createdNew;
        using (Mutex singleInstance = new Mutex(true, MutexName, out createdNew))
        {
            if (!createdNew)
            {
                try
                {
                    using (EventWaitHandle existing = EventWaitHandle.OpenExisting(EventName)) existing.Set();
                }
                catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("Awake Toggle is starting. Please click again.", "Awake Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                catch (UnauthorizedAccessException) { MessageBox.Show("Unable to signal the running Awake Toggle.", "Awake Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (EventWaitHandle toggleSignal = new EventWaitHandle(false, EventResetMode.AutoReset, EventName))
            {
                try
                {
                    using (AwakeContext context = new AwakeContext(toggleSignal)) Application.Run(context);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Awake Toggle Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private sealed class AwakeContext : ApplicationContext
    {
        private NotifyIcon tray;
        private Icon icon;
        private Thread watcher;
        private EventWaitHandle toggleSignal;
        private volatile bool stopping;
        private bool active;

        internal AwakeContext(EventWaitHandle signal)
        {
            toggleSignal = signal;
            // ApplicationContext itself has no WinForms synchronization context.
            // Create an invisible control so signals can be marshalled to the UI thread.
            Control dispatcher = new Control();
            IntPtr dispatcherHandle = dispatcher.Handle;
            Dispatcher = dispatcher;
            try
            {
                StartAwake();
                icon = CreateIcon();
                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.Add("Awake Mode: ON").Enabled = false;
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Turn OFF and exit", null, (sender, e) => StopAwake());
                tray = new NotifyIcon { Icon = icon, Text = "Awake Mode ON - Screen and sleep: Never", ContextMenuStrip = menu, Visible = true };
                tray.DoubleClick += (sender, e) => StopAwake();
                watcher = new Thread(() =>
                {
                    while (!stopping)
                    {
                        if (!toggleSignal.WaitOne(200)) continue;
                        if (stopping) break;
                        try { Dispatcher.BeginInvoke((MethodInvoker)(() => StopAwake())); }
                        catch (InvalidOperationException) { break; }
                        break;
                    }
                });
                watcher.IsBackground = true;
                watcher.Start();
                Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
            }
            catch
            {
                if (active) { try { RestoreBackup(); } catch { } }
                SetThreadExecutionState(ES_CONTINUOUS);
                dispatcher.Dispose();
                throw;
            }
        }

        private Control Dispatcher { get; set; }

        private void OnSessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
        {
            if (!active) return;
            try { RestoreBackup(); active = false; } catch { }
        }

        private void StartAwake()
        {
            Directory.CreateDirectory(DataDirectory);
            // Recover from an abnormal exit before taking a fresh snapshot.
            if (File.Exists(BackupPath)) RestoreBackup();

            string scheme = GetScheme();
            int[] screen = ReadTimeouts(scheme, VideoGroup, DisplayTimeout);
            int[] sleep = ReadTimeouts(scheme, SleepGroup, SleepTimeout);
            string[] backup = { scheme, screen[0].ToString(CultureInfo.InvariantCulture), screen[1].ToString(CultureInfo.InvariantCulture), sleep[0].ToString(CultureInfo.InvariantCulture), sleep[1].ToString(CultureInfo.InvariantCulture) };
            File.WriteAllLines(BackupPath + ".tmp", backup);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            File.Move(BackupPath + ".tmp", BackupPath);
            active = true;
            try
            {
                WriteTimeout(scheme, "ac", VideoGroup, DisplayTimeout, 0);
                WriteTimeout(scheme, "dc", VideoGroup, DisplayTimeout, 0);
                WriteTimeout(scheme, "ac", SleepGroup, SleepTimeout, 0);
                WriteTimeout(scheme, "dc", SleepGroup, SleepTimeout, 0);
                RunPowerCfg("/setactive " + scheme);
                if (SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED) == 0)
                    throw new InvalidOperationException("Windows did not accept the keep-awake request.");
            }
            catch
            {
                try { RestoreBackup(); active = false; } catch { }
                throw;
            }
        }

        private void StopAwake()
        {
            if (stopping) return;
            try
            {
                RestoreBackup();
                active = false;
                SetThreadExecutionState(ES_CONTINUOUS);
                stopping = true;
                Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                if (icon != null) icon.Dispose();
                Dispatcher.Dispose();
                ExitThread();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not restore your original power settings. Awake Toggle is still running.\n\n" + ex.Message,
                    "Awake Toggle", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void RestoreBackup()
        {
            if (!File.Exists(BackupPath)) return;
            string[] original = File.ReadAllLines(BackupPath);
            if (original.Length != 5 || !Regex.IsMatch(original[0], @"^[0-9a-fA-F-]{36}$"))
                throw new InvalidDataException("Saved power settings are invalid. File: " + BackupPath);
            string scheme = original[0];
            WriteTimeout(scheme, "ac", VideoGroup, DisplayTimeout, uint.Parse(original[1], CultureInfo.InvariantCulture));
            WriteTimeout(scheme, "dc", VideoGroup, DisplayTimeout, uint.Parse(original[2], CultureInfo.InvariantCulture));
            WriteTimeout(scheme, "ac", SleepGroup, SleepTimeout, uint.Parse(original[3], CultureInfo.InvariantCulture));
            WriteTimeout(scheme, "dc", SleepGroup, SleepTimeout, uint.Parse(original[4], CultureInfo.InvariantCulture));
            // Only re-activate the snapshot's scheme if it is already the active scheme;
            // don't undo a user's intentional plan switch while this app was running.
            if (string.Equals(GetScheme(), scheme, StringComparison.OrdinalIgnoreCase)) RunPowerCfg("/setactive " + scheme);
            File.Delete(BackupPath);
        }

        private static string GetScheme()
        {
            string output = RunPowerCfg("/getactivescheme");
            Match match = Regex.Match(output, @"\b[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b");
            if (!match.Success) throw new InvalidOperationException("Cannot identify the active Windows power plan.");
            return match.Value;
        }

        private static int[] ReadTimeouts(string scheme, string subgroup, string setting)
        {
            string output = RunPowerCfg("/query " + scheme + " " + subgroup + " " + setting);
            MatchCollection matches = Regex.Matches(output, @"0x[0-9a-fA-F]{8}\b");
            if (matches.Count < 2) throw new InvalidOperationException("Cannot read the original screen/sleep timeouts.\n" + output);
            // powercfg /query emits AC and DC current values, in that order.
            return new[] { checked((int)Convert.ToUInt32(matches[matches.Count - 2].Value.Substring(2), 16)), checked((int)Convert.ToUInt32(matches[matches.Count - 1].Value.Substring(2), 16)) };
        }

        private static void WriteTimeout(string scheme, string mode, string subgroup, string setting, uint seconds)
        {
            RunPowerCfg("/set" + mode + "valueindex " + scheme + " " + subgroup + " " + setting + " " + seconds.ToString(CultureInfo.InvariantCulture));
        }

        private static string RunPowerCfg(string arguments)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("powercfg.exe", arguments) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("powercfg " + arguments + " failed: " + error + " " + output);
                return output;
            }
        }

        private static Icon CreateIcon()
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (Brush fill = new SolidBrush(Color.FromArgb(35, 173, 87))) graphics.FillEllipse(fill, 1, 1, 30, 30);
                using (Pen rim = new Pen(Color.FromArgb(20, 105, 49), 1.7f)) graphics.DrawEllipse(rim, 2, 2, 28, 28);
                using (Font font = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    graphics.DrawString("A", font, Brushes.White, new RectangleF(0, -1, 32, 32), sf);
                IntPtr hIcon = bitmap.GetHicon();
                try { using (Icon temporary = Icon.FromHandle(hIcon)) return (Icon)temporary.Clone(); }
                finally { DestroyIcon(hIcon); }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
