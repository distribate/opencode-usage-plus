using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using OpenCodeUsagePlus.Core.Models;
using OpenCodeUsagePlus.Core.Services;
using OpenCodeUsagePlus.Core.Storage;
using OpenCodeUsagePlus.Core.Utilities;
using OpenCodeUsagePlus.Services;

namespace OpenCodeUsagePlus.ViewModels;

/// <summary>
/// All state for the tray icon and the flyout. Lives on the WPF dispatcher: the timers are
/// <see cref="DispatcherTimer"/>s, so property changes always reach bindings on the right thread.
/// </summary>
public sealed class UsageViewModel : INotifyPropertyChanged, IDisposable
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly AppSettings _settings;
    private readonly UsageApiClient _api;
    private readonly OpenCodeCredentialStore _openCodeStore;
    private readonly SecretStore _secrets;
    private readonly UsageHistoryStore _historyStore;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _tickTimer;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private UsageStats? _stats;
    private IReadOnlyList<UsageSample> _history = Array.Empty<UsageSample>();
    private AuthCredential? _credential;
    private bool _isLoading;
    private bool _needsSetup;
    private bool _hasApiKey;
    private bool _settingsOpen;
    private string? _errorMessage;
    private DateTimeOffset? _lastUpdated;
    private bool _disposed;

    public UsageViewModel(
        AppSettings? settings = null,
        UsageApiClient? api = null,
        OpenCodeCredentialStore? openCodeStore = null,
        SecretStore? secrets = null,
        UsageHistoryStore? historyStore = null)
    {
        _settings = settings ?? AppSettings.Load();
        _api = api ?? new UsageApiClient();
        _openCodeStore = openCodeStore ?? new OpenCodeCredentialStore();
        _secrets = secrets ?? new SecretStore();
        _historyStore = historyStore ?? new UsageHistoryStore();

        _hasApiKey = _secrets.Read() is not null;
        _history = _historyStore.Load(DateTimeOffset.Now);

        _refreshTimer = new DispatcherTimer { Interval = RefreshInterval };
        _refreshTimer.Tick += (_, _) => _ = RefreshAsync();

        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += (_, _) => RaiseCountdowns();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The icon renderer must redraw when this fires.</summary>
    public event EventHandler? IconStyleChanged;

    public UsageStats? Stats
    {
        get => _stats;
        private set
        {
            if (Equals(_stats, value))
            {
                return;
            }

            _stats = value;
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public IReadOnlyList<UsageSample> History
    {
        get => _history;
        private set
        {
            _history = value;
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public bool NeedsSetup
    {
        get => _needsSetup;
        private set
        {
            if (_needsSetup == value)
            {
                return;
            }

            _needsSetup = value;
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public bool SettingsOpen
    {
        get => _settingsOpen;
        set
        {
            if (_settingsOpen == value)
            {
                return;
            }

            _settingsOpen = value;
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public bool HasApiKey => _hasApiKey;

    public bool IsUsingOpenCodeToken => !_hasApiKey && _credential?.Kind == CredentialKind.OAuth;

    public bool ShowSettingsPanel => _settingsOpen || _needsSetup;

    public bool ShowUsage => !ShowSettingsPanel && _stats is not null;

    public bool ShowError => !ShowSettingsPanel && _stats is null && _errorMessage is not null;

    public bool ShowLoading => !ShowSettingsPanel && _stats is null && _isLoading && _errorMessage is null;

    public bool ShowWarning => !ShowSettingsPanel && _stats is not null && _errorMessage is not null;

    public string WarningText => _errorMessage ?? string.Empty;

    public bool HasHistory => _history.Count >= 2;

    public bool ShowCollecting => !HasHistory && _stats is not null;

    public double RollingPercent => _stats?.Rolling.Percent ?? 0;

    public double WeeklyPercent => _stats?.Weekly.Percent ?? 0;

    public double MonthlyPercent => _stats?.Monthly.Percent ?? 0;

    public string RollingPercentText => FormatPercent(_stats?.Rolling);

    public string WeeklyPercentText => FormatPercent(_stats?.Weekly);

    public string MonthlyPercentText => FormatPercent(_stats?.Monthly);

    public string RollingResetText => FormatReset(_stats?.Rolling);

    public string WeeklyResetText => FormatReset(_stats?.Weekly);

    public string MonthlyResetText => FormatReset(_stats?.Monthly);

    public string LastUpdatedText
    {
        get
        {
            if (_lastUpdated is null)
            {
                return string.Empty;
            }

            var age = DateTimeOffset.Now - _lastUpdated.Value;
            if (age < TimeSpan.Zero)
            {
                age = TimeSpan.Zero;
            }

            return age < TimeSpan.FromMinutes(1)
                ? "Updated just now"
                : $"Updated {(int)age.TotalMinutes} min ago";
        }
    }

    public string TrayTooltip
    {
        get
        {
            if (_stats is null)
            {
                return _needsSetup
                    ? "OpenCode Usage Plus — not signed in"
                    : "OpenCode Usage Plus — loading…";
            }

            return $"Rolling {_stats.Rolling.DisplayPercent}% · Weekly {_stats.Weekly.DisplayPercent}% · Monthly {_stats.Monthly.DisplayPercent}%";
        }
    }

    public string CredentialStatusText
    {
        get
        {
            if (_hasApiKey)
            {
                return "Using the saved API key.";
            }

            return _credential?.Kind switch
            {
                CredentialKind.OAuth => "Using the OpenCode sign-in token from opencode.db.",
                CredentialKind.ApiKey => "Using an API key stored by OpenCode.",
                _ => "No credentials found. Sign in with OpenCode, or paste an API key.",
            };
        }
    }

    public TrayIconStyle IconStyle
    {
        get => _settings.IconStyle;
        set
        {
            if (_settings.IconStyle == value)
            {
                return;
            }

            _settings.IconStyle = value;
            _settings.Save();
            OnPropertyChanged();
            IconStyleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool LaunchAtLogin
    {
        get => AutoStart.IsSupported ? AutoStart.IsEnabled() : _settings.LaunchAtLogin;
        set
        {
            if (AutoStart.IsSupported)
            {
                AutoStart.SetEnabled(value);
            }

            _settings.LaunchAtLogin = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool NotificationsEnabled
    {
        get => _settings.NotificationsEnabled;
        set
        {
            if (_settings.NotificationsEnabled == value)
            {
                return;
            }

            _settings.NotificationsEnabled = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public void Start()
    {
        _refreshTimer.Start();
        _tickTimer.Start();
        _ = RefreshAsync();
    }

    /// <summary>True exactly once per installation: used for the "look in the tray" hint.</summary>
    public bool TakeFirstRunHint()
    {
        if (_settings.FirstRunHintShown)
        {
            return false;
        }

        _settings.FirstRunHintShown = true;
        _settings.Save();
        return true;
    }

    public async Task RefreshAsync()
    {
        if (_disposed || !await _refreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var credential = await ResolveCredentialAsync().ConfigureAwait(true);
            if (credential is null)
            {
                _credential = null;
                NeedsSetup = true;
                return;
            }

            _credential = credential;
            NeedsSetup = false;

            var stats = await _api.FetchAsync(credential).ConfigureAwait(true);
            Stats = stats;
            _lastUpdated = DateTimeOffset.Now;
            ErrorMessage = null;

            var now = DateTimeOffset.Now;
            var samples = _historyStore.Append(_history, UsageHistoryStore.SampleOf(stats, now), now);
            History = samples;
            _historyStore.Save(samples);

            AppLog.Info(
                $"usage updated: rolling={stats.Rolling.DisplayPercent}% weekly={stats.Weekly.DisplayPercent}% monthly={stats.Monthly.DisplayPercent}%");
        }
        catch (UsageApiException ex)
        {
            if (_disposed)
            {
                return;
            }

            AppLog.Warn($"refresh failed: {ex.Kind} {ex.Message}");
            ErrorMessage = ex.UserMessage;
            if (ex.Kind is UsageErrorKind.AuthExpired or UsageErrorKind.NotConfigured)
            {
                _credential = null;
            }
        }
        catch (Exception ex)
        {
            if (_disposed)
            {
                return;
            }

            AppLog.Error("unexpected refresh failure", ex);
            ErrorMessage = "Unexpected error. See the log for details.";
        }
        finally
        {
            IsLoading = false;
            _refreshGate.Release();
            RaiseDerived();
        }
    }

    public bool SetApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !_secrets.TrySave(apiKey))
        {
            return false;
        }

        _hasApiKey = true;
        _credential = null;
        NeedsSetup = false;
        ErrorMessage = null;
        RaiseDerived();
        _ = RefreshAsync();
        return true;
    }

    public void ClearApiKey()
    {
        _secrets.Clear();
        _hasApiKey = false;
        _credential = null;
        RaiseDerived();
        _ = RefreshAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshTimer.Stop();
        _tickTimer.Stop();
        _api.Dispose();
        // The semaphore is intentionally not disposed: an in-flight refresh releases it in its
        // finally block, and the process is exiting anyway.
    }

    private async Task<AuthCredential?> ResolveCredentialAsync()
    {
        var saved = _secrets.Read();
        if (!string.IsNullOrWhiteSpace(saved))
        {
            _hasApiKey = true;
            return new AuthCredential(saved.Trim(), CredentialKind.ApiKey, SourcePath: "saved key");
        }

        _hasApiKey = false;
        return await Task.Run(_openCodeStore.TryRead).ConfigureAwait(true);
    }

    private void RaiseCountdowns()
    {
        OnPropertyChanged(nameof(RollingResetText));
        OnPropertyChanged(nameof(WeeklyResetText));
        OnPropertyChanged(nameof(MonthlyResetText));
        OnPropertyChanged(nameof(LastUpdatedText));
    }

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(ShowSettingsPanel));
        OnPropertyChanged(nameof(ShowUsage));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowWarning));
        OnPropertyChanged(nameof(WarningText));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(ShowCollecting));
        OnPropertyChanged(nameof(RollingPercent));
        OnPropertyChanged(nameof(WeeklyPercent));
        OnPropertyChanged(nameof(MonthlyPercent));
        OnPropertyChanged(nameof(RollingPercentText));
        OnPropertyChanged(nameof(WeeklyPercentText));
        OnPropertyChanged(nameof(MonthlyPercentText));
        OnPropertyChanged(nameof(RollingResetText));
        OnPropertyChanged(nameof(WeeklyResetText));
        OnPropertyChanged(nameof(MonthlyResetText));
        OnPropertyChanged(nameof(LastUpdatedText));
        OnPropertyChanged(nameof(TrayTooltip));
        OnPropertyChanged(nameof(CredentialStatusText));
        OnPropertyChanged(nameof(HasApiKey));
        OnPropertyChanged(nameof(IsUsingOpenCodeToken));
    }

    private static string FormatPercent(UsageItem? item) => item is null ? "—" : $"{item.DisplayPercent}%";

    private static string FormatReset(UsageItem? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        var seconds = DurationFormat.SecondsUntil(item.ResetsAt, DateTimeOffset.Now);
        return $"Resets in {DurationFormat.Format(seconds)}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
