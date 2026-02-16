using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using MacMode.Core.Engine;
using MacMode.Core.Hook;
using MacMode.Core.Logging;
using MacMode.Core.Settings;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace MacMode.App;

/// <summary>
/// Manages the system tray icon and context menu. Receives all core
/// dependencies via constructor injection from <see cref="AppBootstrapper"/>.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private static readonly TimeSpan SuspendDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan HookHealthInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan HookSilenceThreshold = TimeSpan.FromMinutes(5);
    private static readonly bool IsRunningAsAdmin = IsElevated();

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _suspendItem;
    private readonly ToolStripMenuItem _startOnLoginItem;

    private readonly AppBootstrapper _app;
    private readonly MappingEngine _engine;
    private readonly SettingsManager _settings;
    private readonly KeyboardHook _hook;

    private DispatcherTimer? _suspendTimer;
    private readonly DispatcherTimer _hookHealthTimer;
    private ProfileEditorWindow? _editorWindow;
    private bool _disposed;

    public TrayIcon(AppBootstrapper app)
    {
        _app = app;
        _engine = app.Engine;
        _settings = app.Settings;
        _hook = app.Hook;

        _engine.PanicKeyPressed += OnPanicKey;
        _hook.KeyEvent += OnKeyEvent;

        // Build tray menu
        _contextMenu = new ContextMenuStrip();

        _toggleItem = new ToolStripMenuItem(_engine.Enabled ? "Mac Mode: ON" : "Mac Mode: OFF");
        _toggleItem.Click += OnToggleMacMode;

        _suspendItem = new ToolStripMenuItem("Suspend (10 min)");
        _suspendItem.Click += OnSuspend;

        _startOnLoginItem = new ToolStripMenuItem("Start on Login")
        {
            Checked = StartupManager.IsStartOnLoginEnabled()
        };
        _startOnLoginItem.Click += OnStartOnLogin;

        var editProfilesItem = new ToolStripMenuItem("Edit Profiles...");
        editProfilesItem.Click += OnEditProfiles;

        var restartAsAdminItem = new ToolStripMenuItem("Restart as Admin");
        restartAsAdminItem.Click += OnRestartAsAdmin;

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += OnExit;

        _contextMenu.Items.Add(_toggleItem);
        _contextMenu.Items.Add(_suspendItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_startOnLoginItem);
        _contextMenu.Items.Add(editProfilesItem);
        _contextMenu.Items.Add(restartAsAdminItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(_engine.Enabled),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        UpdateTrayState();

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                OnToggleMacMode(this, EventArgs.Empty);
        };

        // Surface profile parse errors as tray notifications
        _app.Profiles.ProfileError += OnProfileError;

        // Install hooks
        _hook.Install();
        _app.MouseHook.Install();

        // Hook health monitor: periodically checks if the hook is still receiving events.
        // Windows can silently remove hooks under heavy system load.
        _hookHealthTimer = new DispatcherTimer { Interval = HookHealthInterval };
        _hookHealthTimer.Tick += OnHookHealthCheck;
        _hookHealthTimer.Start();
    }

    private void OnKeyEvent(object? sender, KeyboardHookEventArgs e)
    {
        bool suppress = _engine.ProcessKeyEvent(e);
        if (suppress)
            e.Handled = true;
    }

    private void OnProfileError(string message)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _notifyIcon.ShowBalloonTip(5000, "MacMode", message, System.Windows.Forms.ToolTipIcon.Error);
        });
    }

    private void OnHookHealthCheck(object? sender, EventArgs e)
    {
        var silence = DateTime.UtcNow - _hook.LastEventTime;
        if (silence > HookSilenceThreshold)
        {
            Logger.Info($"Hook health: no events for {silence.TotalSeconds:F0}s, reinstalling hook.");
            try
            {
                _hook.Reinstall();
            }
            catch (Exception ex)
            {
                Logger.Error($"Hook reinstall failed: {ex.Message}");
            }
        }
    }

    private void OnPanicKey()
    {
        _engine.Enabled = false;
        _settings.Current.MacModeEnabled = false;
        _settings.Save();
        UpdateTrayState();

        _notifyIcon.ShowBalloonTip(3000, "MacMode",
            "Mac Mode disabled (panic key).", System.Windows.Forms.ToolTipIcon.Warning);
    }

    private void OnToggleMacMode(object? sender, EventArgs e)
    {
        _engine.Enabled = !_engine.Enabled;
        _settings.Current.MacModeEnabled = _engine.Enabled;
        _settings.Save();
        UpdateTrayState();

        // Cancel any active suspend
        _suspendTimer?.Stop();
        _suspendTimer = null;
        _suspendItem.Text = "Suspend (10 min)";
    }

    private void OnSuspend(object? sender, EventArgs e)
    {
        if (_suspendTimer != null)
        {
            // Already suspended, cancel it
            _suspendTimer.Stop();
            _suspendTimer = null;
            _engine.Enabled = true;
            _settings.Current.MacModeEnabled = true;
            _settings.Save();
            _suspendItem.Text = "Suspend (10 min)";
            UpdateTrayState();
            Logger.Info("Suspend cancelled.");
            return;
        }

        _engine.Enabled = false;
        _suspendItem.Text = "Resume (suspended)";
        UpdateTrayState();
        Logger.Info("Mac Mode suspended for 10 minutes.");

        _suspendTimer = new DispatcherTimer
        {
            Interval = SuspendDuration
        };
        _suspendTimer.Tick += (_, _) =>
        {
            _suspendTimer.Stop();
            _suspendTimer = null;
            _engine.Enabled = true;
            _settings.Current.MacModeEnabled = true;
            _settings.Save();
            _suspendItem.Text = "Suspend (10 min)";
            UpdateTrayState();
            Logger.Info("Suspend expired, Mac Mode re-enabled.");

            _notifyIcon.ShowBalloonTip(2000, "MacMode",
                "Mac Mode re-enabled after suspend.", System.Windows.Forms.ToolTipIcon.Info);
        };
        _suspendTimer.Start();
    }

    private void OnStartOnLogin(object? sender, EventArgs e)
    {
        bool newValue = !_startOnLoginItem.Checked;
        StartupManager.SetStartOnLogin(newValue);
        _startOnLoginItem.Checked = newValue;
        _settings.Current.StartOnLogin = newValue;
        _settings.Save();
    }

    private void OnEditProfiles(object? sender, EventArgs e)
    {
        if (_editorWindow != null && _editorWindow.IsLoaded)
        {
            _editorWindow.Activate();
            return;
        }

        _editorWindow = new ProfileEditorWindow(
            _app.Profiles.ProfilesDirectory,
            _engine,
            _hook);
        _editorWindow.Closed += (_, _) => _editorWindow = null;
        _editorWindow.Show();
    }

    private void OnRestartAsAdmin(object? sender, EventArgs e)
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            });

            Dispose();
            System.Windows.Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User clicked "No" on the UAC prompt
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to restart as admin: {ex.Message}");
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void UpdateTrayState()
    {
        _toggleItem.Text = _engine.Enabled ? "Mac Mode: ON" : "Mac Mode: OFF";
        _notifyIcon.Icon = CreateTrayIcon(_engine.Enabled);
        string suffix = IsRunningAsAdmin ? " [Admin]" : "";
        _notifyIcon.Text = _engine.Enabled ? $"MacMode (ON){suffix}" : $"MacMode (OFF){suffix}";
    }

    /// <summary>
    /// Creates a Finder-inspired tray icon: a two-toned face with dot eyes and a smile,
    /// reminiscent of the classic macOS Finder icon. Rendered at 32x32 for high-DPI.
    /// </summary>
    private static Icon CreateTrayIcon(bool enabled)
    {
        const int size = 32;
        const int pad = 1;
        const int radius = 7;
        var bmp = new Bitmap(size, size);

        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var faceRect = new Rectangle(pad, pad, size - pad * 2, size - pad * 2);
            using var facePath = RoundedRect(faceRect, radius);

            // Two-tone face: navy/sky blue normally, gold/amber when running as admin
            Color leftColor, rightColor;
            if (!enabled)
            {
                leftColor = Color.FromArgb(120, 120, 130);
                rightColor = Color.FromArgb(170, 170, 180);
            }
            else if (IsRunningAsAdmin)
            {
                leftColor = Color.FromArgb(230, 160, 0);   // deep gold
                rightColor = Color.FromArgb(255, 210, 60);  // bright amber
            }
            else
            {
                leftColor = Color.FromArgb(13, 71, 161);    // deep navy
                rightColor = Color.FromArgb(66, 165, 245);  // sky blue
            }

            // Clip to rounded rect, then fill each half
            g.SetClip(facePath);
            using (var leftBrush = new SolidBrush(leftColor))
                g.FillRectangle(leftBrush, pad, pad, (size - pad * 2) / 2, size - pad * 2);
            using (var rightBrush = new SolidBrush(rightColor))
                g.FillRectangle(rightBrush, pad + (size - pad * 2) / 2, pad, (size - pad * 2) / 2, size - pad * 2);
            g.ResetClip();

            // Eyes: two white ovals
            var featureColor = Color.White;
            using var featureBrush = new SolidBrush(featureColor);
            g.FillEllipse(featureBrush, 8, 10, 4, 5);   // left eye
            g.FillEllipse(featureBrush, 20, 10, 4, 5);   // right eye

            // Smile: a white arc
            using var smilePen = new Pen(featureColor, 2.0f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawArc(smilePen, 9, 14, 14, 10, 15, 150);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>
    /// Creates a GraphicsPath for a rounded rectangle.
    /// </summary>
    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var gp = new GraphicsPath();
        gp.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        gp.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        gp.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        gp.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        gp.CloseFigure();
        return gp;
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _hookHealthTimer.Stop();
        _suspendTimer?.Stop();
        _hook.KeyEvent -= OnKeyEvent;
        _app.Profiles.ProfileError -= OnProfileError;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _app.Dispose();
    }
}
