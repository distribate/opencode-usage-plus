using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using OpenCodeUsagePlus.Core.Models;
using OpenCodeUsagePlus.Core.Storage;
using OpenCodeUsagePlus.Core.Utilities;
using OpenCodeUsagePlus.Services;
using OpenCodeUsagePlus.ViewModels;
using OpenCodeUsagePlus.Views;
using WpfApplication = System.Windows.Application;

namespace OpenCodeUsagePlus.Tray;

/// <summary>
/// Owns the shell objects: the tray icon, its context menu and the flyout. This is the Windows
/// equivalent of <c>MenuBarExtra</c> + <c>LSUIElement</c>.
/// </summary>
internal sealed class TrayHost : IDisposable
{
    private readonly UsageViewModel _viewModel;
    private readonly NotifyIcon _notifyIcon;
    private readonly FlyoutWindow _flyout;
    private readonly ContextMenuStrip _menu;
    private readonly EventWaitHandle _showEvent;
    private readonly Thread _showThread;

    private Icon? _currentIcon;
    private DateTime _lastClickUtc = DateTime.MinValue;
    private int _notifiedThreshold;
    private bool _disposed;

    public TrayHost(EventWaitHandle showEvent)
    {
        _showEvent = showEvent;
        _viewModel = new UsageViewModel();

        _notifyIcon = new NotifyIcon
        {
            Visible = false,
            Text = "OpenCode Usage Plus",
        };
        _notifyIcon.MouseUp += OnIconMouseUp;

        _menu = BuildMenu();
        _notifyIcon.ContextMenuStrip = _menu;

        _flyout = new FlyoutWindow(_viewModel);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.IconStyleChanged += (_, _) => UpdateIcon();

        _showThread = new Thread(ShowEventLoop)
        {
            IsBackground = true,
            Name = "opencode-usage-single-instance",
        };

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public void Start()
    {
        AppLog.Info($"tray: start iconStyle={_viewModel.IconStyle}");
        UpdateIcon();
        _notifyIcon.Visible = true;

        if (_viewModel.TakeFirstRunHint())
        {
            _notifyIcon.ShowBalloonTip(
                timeout: 7000,
                tipTitle: "OpenCode Usage Plus is running",
                tipText: "Windows 11 hides new tray icons by default — drag the icon out of the ^ overflow once.",
                tipIcon: ToolTipIcon.Info);
        }

        _showThread.Start();
        _viewModel.Start();
    }

    public void Stop()
    {
        AppLog.Info("tray: stop");
        _notifyIcon.Visible = false;
        _viewModel.Dispose();
        _flyout.CloseForExit();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _notifyIcon.MouseUp -= OnIconMouseUp;

        _showEvent.Set();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _currentIcon?.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };

        menu.Items.Add(new ToolStripMenuItem("Open usage…", null, (_, _) => ShowFlyout()));
        menu.Items.Add(new ToolStripMenuItem("Refresh now", null, (_, _) => _ = _viewModel.RefreshAsync()));
        menu.Items.Add(new ToolStripSeparator());

        var autoStart = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = _viewModel.LaunchAtLogin,
        };
        autoStart.Click += (_, _) => _viewModel.LaunchAtLogin = autoStart.Checked;
        menu.Items.Add(autoStart);

        menu.Items.Add(new ToolStripMenuItem("Settings…", null, (_, _) => ShowFlyout(openSettings: true)));
        menu.Items.Add(new ToolStripMenuItem("Open data folder", null, (_, _) => OpenDataFolder()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => WpfApplication.Current?.Shutdown()));

        return menu;
    }

    private void OnIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastClickUtc < TimeSpan.FromMilliseconds(250))
        {
            return;
        }

        _lastClickUtc = now;

        if (_flyout.IsVisible)
        {
            _flyout.Hide();
            return;
        }

        ShowFlyout();
    }

    private void ShowFlyout(bool openSettings = false)
    {
        if (!openSettings && DateTime.UtcNow - _flyout.LastHiddenUtc < TimeSpan.FromMilliseconds(250))
        {
            return;
        }

        _flyout.ShowAt(Cursor.Position, openSettings);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(UsageViewModel.Stats):
                UpdateIcon();
                MaybeNotify();
                break;
            case nameof(UsageViewModel.TrayTooltip):
                _notifyIcon.Text = Truncate(_viewModel.TrayTooltip, 63);
                break;
        }
    }

    private void UpdateIcon()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            var icon = TrayIconRenderer.Render(
                _viewModel.Stats,
                _viewModel.IconStyle,
                ThemePalette.TaskbarIsLight,
                size);

            var previous = _currentIcon;
            _currentIcon = icon;
            _notifyIcon.Icon = icon;
            _notifyIcon.Text = Truncate(_viewModel.TrayTooltip, 63);
            previous?.Dispose();
        }
        catch (Exception ex)
        {
            AppLog.Error("tray icon update failed", ex);
        }
    }

    private void MaybeNotify()
    {
        var stats = _viewModel.Stats;
        if (stats is null || !_viewModel.NotificationsEnabled)
        {
            return;
        }

        var percent = (int)Math.Round(stats.MaxPercent);
        if (percent < 80)
        {
            _notifiedThreshold = 0;
            return;
        }

        var threshold = percent >= 95 ? 95 : 80;
        if (threshold <= _notifiedThreshold)
        {
            return;
        }

        _notifiedThreshold = threshold;

        var (label, item) = stats.Rolling.Percent >= stats.Weekly.Percent && stats.Rolling.Percent >= stats.Monthly.Percent
            ? ("Rolling", stats.Rolling)
            : stats.Weekly.Percent >= stats.Monthly.Percent
                ? ("Weekly", stats.Weekly)
                : ("Monthly", stats.Monthly);

        var reset = DurationFormat.FormatUntil(item.ResetsAt, DateTimeOffset.Now);
        _notifyIcon.ShowBalloonTip(
            timeout: 6000,
            tipTitle: $"OpenCode Usage Plus at {percent}%",
            tipText: $"{label} window resets in {reset}.",
            tipIcon: ToolTipIcon.Warning);
    }

    private void OpenDataFolder()
    {
        try
        {
            AppPaths.EnsureExists();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppPaths.AppDataDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"cannot open data folder: {ex.Message}");
        }
    }

    private void ShowEventLoop()
    {
        while (!_disposed)
        {
            _showEvent.WaitOne();
            if (_disposed)
            {
                return;
            }

            Dispatch(() => ShowFlyout());
        }
    }

    private static void Dispatch(Action action)
    {
        var application = WpfApplication.Current;
        if (application is null)
        {
            return;
        }

        if (application.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            application.Dispatcher.BeginInvoke(action);
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        Dispatch(() =>
        {
            UpdateIcon();
            _flyout.RefreshTheme();
        });

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatch(UpdateIcon);

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Dispatch(() => _ = _viewModel.RefreshAsync());
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            Dispatch(() => _ = _viewModel.RefreshAsync());
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            Dispatch(() => _ = _viewModel.RefreshAsync());
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
