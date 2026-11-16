using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenCodeUsagePlus.Core.Models;
using OpenCodeUsagePlus.Core.Storage;
using OpenCodeUsagePlus.Core.Utilities;
using OpenCodeUsagePlus.Tray;
using OpenCodeUsagePlus.ViewModels;

namespace OpenCodeUsagePlus.Views;

public partial class FlyoutWindow : Window
{
    private readonly UsageViewModel _viewModel;
    private bool _adjustingHeight;
    private bool _syncingSelection;

    public FlyoutWindow(UsageViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        DataContext = viewModel;
        ThemePalette.Refresh();
        ThemePalette.Apply(Resources);

        if (AppEnvironment.PlainWindow)
        {
            // Diagnostic mode: a normal layered-free window without the shadow effect. Used to
            // isolate WPF/GPU rendering crashes on Windows 11.
            AllowsTransparency = false;
            if (Resources["WindowBrush"] is System.Windows.Media.Brush brush)
            {
                Background = brush;
            }

            RootBorder.Effect = null;
            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.BorderThickness = new Thickness(0);
            RootBorder.Margin = new Thickness(0);
        }

        var version = typeof(FlyoutWindow).Assembly.GetName().Version;
        VersionText.Text = version is null ? string.Empty : $"v{version.ToString(3)}";

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) => SyncSelection();
        Closing += OnClosing;

        AppLog.Info($"flyout: constructed plain={AppEnvironment.PlainWindow}");
    }

    private bool AllowClose { get; set; }

    public DateTime LastHiddenUtc { get; private set; } = DateTime.MinValue;

    public void ShowAt(System.Drawing.Point cursorPhysical, bool openSettings = false)
    {
        if (openSettings)
        {
            _viewModel.SettingsOpen = true;
        }

        AdjustHeight();

        var (workArea, cursorX, cursorY) = ScreenInfo.Describe(cursorPhysical);
        var (x, y) = FlyoutPositioner.Place(
            (int)Math.Ceiling(Width),
            (int)Math.Ceiling(Height),
            cursorX,
            cursorY,
            workArea);

        Left = x;
        Top = y;

        AppLog.Info(
            $"flyout: show cursor=({cursorPhysical.X},{cursorPhysical.Y}) " +
            $"size={Width:F0}x{Height:F0} at=({x},{y})");

        Show();
        Activate();

        if (_viewModel.NeedsSetup)
        {
            ApiKeyBox.Focus();
        }
    }

    public void RefreshTheme()
    {
        ThemePalette.Refresh();
        ThemePalette.Apply(Resources);
        InvalidateVisual();
    }

    /// <summary>Only the tray's Quit path may destroy the window; Alt+F4 just hides it.</summary>
    public void CloseForExit()
    {
        AllowClose = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

        e.Cancel = true;
        HideFlyout();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(UsageViewModel.SettingsOpen):
            case nameof(UsageViewModel.ShowSettingsPanel):
            case nameof(UsageViewModel.IsLoading):
            case nameof(UsageViewModel.ShowUsage):
            case nameof(UsageViewModel.ShowError):
            case nameof(UsageViewModel.HasHistory):
                Dispatcher.BeginInvoke(new Action(AdjustHeight));
                break;
        }
    }

    private void AdjustHeight()
    {
        if (_adjustingHeight || Content is not FrameworkElement root)
        {
            return;
        }

        _adjustingHeight = true;
        try
        {
            root.Measure(new Size(Width, double.PositiveInfinity));
            var desired = root.DesiredSize.Height;
            if (desired > 0 && double.IsFinite(desired))
            {
                Height = Math.Clamp(desired, MinHeight, 620);
            }
        }
        catch (Exception exception)
        {
            AppLog.Error("flyout: height measurement failed", exception);
        }
        finally
        {
            _adjustingHeight = false;
        }
    }

    private void SyncSelection()
    {
        _syncingSelection = true;
        try
        {
            IconStyleCombo.SelectedIndex = (int)_viewModel.IconStyle;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideFlyout();
            e.Handled = true;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // ComboBox and other WPF popups are separate windows: ignore their deactivation.
        if (IconStyleCombo.IsDropDownOpen)
        {
            return;
        }

        HideFlyout();
    }

    private void HideFlyout()
    {
        if (!IsVisible)
        {
            return;
        }

        AppLog.Info("flyout: hide");
        LastHiddenUtc = DateTime.UtcNow;
        Hide();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SettingsOpen = !_viewModel.SettingsOpen;
        AdjustHeight();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _ = _viewModel.RefreshAsync();

    private void OnQuitClick(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();

    private void OnSaveApiKeyClick(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        if (key.Length == 0)
        {
            ShowFeedback("Paste an sk-... key first.");
            return;
        }

        if (!_viewModel.SetApiKey(key))
        {
            ShowFeedback("The key could not be saved.");
            return;
        }

        ApiKeyBox.Clear();
        ShowFeedback("Saved. Refreshing…");
        _viewModel.SettingsOpen = false;
    }

    private void OnClearApiKeyClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearApiKey();
        ApiKeyBox.Clear();
        ShowFeedback("Saved key removed.");
    }

    private void OnIconStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || IconStyleCombo.SelectedIndex < 0)
        {
            return;
        }

        _viewModel.IconStyle = (TrayIconStyle)IconStyleCombo.SelectedIndex;
    }

    private void OnOpenDataFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            AppPaths.EnsureExists();
            Process.Start(new ProcessStartInfo
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

    private void ShowFeedback(string message)
    {
        ApiKeyFeedback.Text = message;
        ApiKeyFeedback.Visibility = Visibility.Visible;
    }
}
