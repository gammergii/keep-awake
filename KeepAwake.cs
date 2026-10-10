using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// All execution-state calls stay on the UI thread, including cleanup.
internal sealed class SleepGuard : IDisposable
{
    internal const uint Continuous = 0x80000000;
    internal const uint SystemRequired = 0x00000001;
    internal const uint DisplayRequired = 0x00000002;

    [DllImport("kernel32.dll")]
    internal static extern uint SetThreadExecutionState(uint flags);

    internal void Set(bool active, bool screenOn)
    {
        uint flags = Continuous;
        if (active)
            flags |= SystemRequired | (screenOn ? DisplayRequired : 0);
        if (SetThreadExecutionState(flags) == 0)
            throw new InvalidOperationException("Windows could not update the keep-awake request.");
    }

    public void Dispose()
    {
        SetThreadExecutionState(Continuous);
    }
}

internal sealed class AwakeWindow : Form
{
    private readonly SleepGuard guard = new SleepGuard();
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem toggleMenu;
    private readonly ToolStripMenuItem screenMenu;
    private readonly Label status;
    private readonly Label description;
    private readonly Button toggle;
    private readonly CheckBox screen;
    internal bool Active { get; private set; }

    internal AwakeWindow(bool showTray)
    {
        Text = "Keep Awake";
        Icon = SystemIcons.Information;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(410, 250);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 250, 252);

        status = new Label { Location = new Point(24, 22), Size = new Size(362, 34),
            Font = new Font("Segoe UI", 17F, FontStyle.Bold) };
        description = new Label { Location = new Point(24, 66), Size = new Size(362, 46) };
        screen = new CheckBox { Text = "Also keep the screen on", Location = new Point(24, 122),
            Size = new Size(350, 28), AccessibleName = "Also keep the screen on" };
        toggle = new Button { Location = new Point(24, 169), Size = new Size(174, 36) };
        Button quit = new Button { Text = "Quit", Location = new Point(212, 169), Size = new Size(174, 36) };
        Label hint = new Label { Text = "Minimize to the tray. Closing this window quits.",
            Location = new Point(24, 220), Size = new Size(370, 22),
            ForeColor = Color.FromArgb(85, 95, 110), Font = new Font("Segoe UI", 9F) };
        Controls.AddRange(new Control[] { status, description, screen, toggle, quit, hint });

        menu = new ContextMenuStrip();
        menu.Items.Add("Show window", null, delegate { RestoreWindow(); });
        toggleMenu = new ToolStripMenuItem();
        toggleMenu.Click += delegate { ChangeActive(!Active); };
        menu.Items.Add(toggleMenu);
        screenMenu = new ToolStripMenuItem("Also keep the screen on");
        screenMenu.Click += delegate { screen.Checked = !screen.Checked; };
        menu.Items.Add(screenMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, delegate { Close(); });
        tray = new NotifyIcon { Icon = SystemIcons.Information, ContextMenuStrip = menu, Visible = false };
        tray.DoubleClick += delegate { RestoreWindow(); };
        toggle.Click += delegate { ChangeActive(!Active); };
        quit.Click += delegate { Close(); };
        screen.CheckedChanged += delegate {
            try { guard.Set(Active, screen.Checked); UpdateStatus(); }
            catch (Exception ex) { FailAndQuit(ex); }
        };
        Resize += delegate {
            if (WindowState == FormWindowState.Minimized) Hide();
        };
        try
        {
            guard.Set(true, false);
            Active = true;
            UpdateStatus();
            tray.Visible = showTray;
        }
        catch { Dispose(); throw; }
    }

    internal void ChangeActive(bool active)
    {
        try { guard.Set(active, screen.Checked); Active = active; UpdateStatus(); }
        catch (Exception ex) { FailAndQuit(ex); }
    }

    internal void ChangeScreen(bool keepOn) { screen.Checked = keepOn; }

    private void UpdateStatus()
    {
        status.Text = Active ? "Keeping your PC awake" : "Normal sleep is enabled";
        status.ForeColor = Active ? Color.FromArgb(25, 115, 70) : Color.FromArgb(85, 95, 110);
        description.Text = Active
            ? (screen.Checked ? "Your PC and screen will stay awake until you pause or quit."
                              : "Your PC will stay awake. The screen can still turn off.")
            : "Windows is using your usual sleep settings. Resume whenever you need.";
        toggle.Text = Active ? "Pause" : "Resume";
        toggleMenu.Text = Active ? "Pause (allow sleep)" : "Resume (keep awake)";
        screenMenu.Checked = screen.Checked;
        tray.Text = Active ? "Keep Awake: active" : "Keep Awake: paused";
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void FailAndQuit(Exception ex)
    {
        MessageBox.Show(this, ex.Message, "Keep Awake", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            guard.Dispose();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (menu != null) menu.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 2 && args[0] == "--self-test")
            return SelfTest(args[1]);

        bool created;
        using (Mutex instance = new Mutex(true, @"Local\KeepAwake.Portable.v1", out created))
        {
            if (!created)
            {
                MessageBox.Show("Keep Awake is already running. Look for its icon near the taskbar clock (it may be under the up arrow).",
                    "Keep Awake", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            try
            {
                using (AwakeWindow window = new AwakeWindow(true)) Application.Run(window);
                return 0;
            }
            catch (Exception ex)
            {
                SleepGuard.SetThreadExecutionState(SleepGuard.Continuous);
                MessageBox.Show(ex.Message, "Keep Awake", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            finally { instance.ReleaseMutex(); }
        }
    }

    // Reads Windows' actual previous state, then restores it on this same thread.
    private static void AssertFlags(uint expected, string step)
    {
        uint previous = SleepGuard.SetThreadExecutionState(SleepGuard.Continuous);
        if (previous == 0 || SleepGuard.SetThreadExecutionState(previous) == 0)
            throw new Exception(step + ": API call failed.");
        uint mask = SleepGuard.SystemRequired | SleepGuard.DisplayRequired;
        if ((previous & mask) != expected)
            throw new Exception(step + ": unexpected flags " + previous.ToString("X"));
    }

    private static int SelfTest(string report)
    {
        try
        {
            using (AwakeWindow window = new AwakeWindow(false))
            {
                AssertFlags(SleepGuard.SystemRequired, "Start (screen can sleep)");
                window.ChangeActive(false);
                AssertFlags(0, "Pause");
                window.ChangeActive(true);
                AssertFlags(SleepGuard.SystemRequired, "Resume");
                window.ChangeScreen(true);
                AssertFlags(SleepGuard.SystemRequired | SleepGuard.DisplayRequired, "Screen on");
                window.ChangeActive(false);
                AssertFlags(0, "Pause with screen option");
                window.ChangeActive(true);
                window.ChangeScreen(false);
                AssertFlags(SleepGuard.SystemRequired, "Screen off");
            }
            AssertFlags(0, "Quit/dispose releases request");
            File.WriteAllText(report, "PASS: startup, pause, resume, screen option, and exit cleanup verified against Windows execution-state API.\r\n");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex + "\r\n"); return 1; }
        finally { SleepGuard.SetThreadExecutionState(SleepGuard.Continuous); }
    }
}
