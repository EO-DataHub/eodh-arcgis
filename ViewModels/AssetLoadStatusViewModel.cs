using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using eodh.Services;

namespace eodh.ViewModels;

/// <summary>
/// Dockpane-owned status for the asset currently being loaded. All UI updates
/// are marshalled to the dispatcher captured when the dockpane is created.
/// </summary>
internal sealed class AssetLoadStatusViewModel : INotifyPropertyChanged, IAssetLoadProgressReporter
{
    private readonly object _gate = new();
    private readonly Dispatcher? _dispatcher = Application.Current?.Dispatcher;
    private Guid? _currentOperationId;
    private long _lastDownloadUpdate;
    private long? _expectedBytes;
    private bool _isActive;
    private bool _isBusy;
    private bool _isIndeterminate;
    private double _progressValue;
    private string _statusText = "Ready";
    private string _detailText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
                NotifyPropertyChanged(nameof(ProgressText));
        }
    }

    /// <summary>
    /// True only while ArcGIS or the downloader is actively working. This is
    /// deliberately separate from IsActive, which remains true briefly to show
    /// the completed status after an operation finishes.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        private set
        {
            if (SetProperty(ref _isIndeterminate, value))
                NotifyPropertyChanged(nameof(ProgressText));
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set
        {
            if (SetProperty(ref _progressValue, value))
                NotifyPropertyChanged(nameof(ProgressText));
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value))
                NotifyPropertyChanged(nameof(HasNonReadyStatus));
        }
    }

    public string DetailText
    {
        get => _detailText;
        private set => SetProperty(ref _detailText, value);
    }

    public string ProgressText => IsActive && !IsIndeterminate
        ? $"{ProgressValue:F0}%"
        : string.Empty;

    /// <summary>
    /// Drives the footer marquee directly from the user-visible status. Keeping
    /// this separate from IsBusy ensures every non-Ready state has a visible
    /// activity strip, including the short completion message.
    /// </summary>
    public bool HasNonReadyStatus =>
        !string.Equals(StatusText, "Ready", StringComparison.Ordinal);

    public Guid Begin(
        string itemId,
        string assetKey,
        string fileType,
        long? expectedBytes,
        int assetIndex,
        int assetCount)
    {
        var operationId = Guid.NewGuid();
        lock (_gate)
        {
            _currentOperationId = operationId;
            _lastDownloadUpdate = 0;
            _expectedBytes = expectedBytes is > 0 ? expectedBytes : null;
        }

        var position = assetCount > 1 ? $"Asset {assetIndex} of {assetCount}" : "Asset";
        Update(operationId, () =>
        {
            IsActive = true;
            IsBusy = true;
            IsIndeterminate = true;
            ProgressValue = 0;
            StatusText = $"{position}: {assetKey} ({fileType})";
            DetailText = WithExpectedSize($"Preparing {Shorten(itemId, 100)}...");
        });
        return operationId;
    }

    public void ReportStage(Guid operationId, string detail) =>
        Update(operationId, () =>
        {
            IsActive = true;
            IsBusy = true;
            IsIndeterminate = true;
            ProgressValue = 0;
            DetailText = WithExpectedSize(detail);
        });

    public void ReportDownload(Guid operationId, long bytesReceived, long? totalBytes)
    {
        var now = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (_currentOperationId != operationId)
                return;

            var isFinal = totalBytes is > 0 && bytesReceived >= totalBytes.Value;
            if (!isFinal && _lastDownloadUpdate != 0 &&
                Stopwatch.GetElapsedTime(_lastDownloadUpdate, now) < TimeSpan.FromMilliseconds(150))
                return;
            _lastDownloadUpdate = now;
        }

        Update(operationId, () =>
        {
            IsActive = true;
            IsBusy = true;
            if (totalBytes is > 0)
            {
                var percent = Math.Clamp(bytesReceived * 100d / totalBytes.Value, 0, 100);
                IsIndeterminate = false;
                ProgressValue = percent;
                DetailText = $"Downloading {FormatBytes(bytesReceived)} of " +
                             $"{FormatBytes(totalBytes.Value)} — {percent:F0}%";
            }
            else
            {
                IsIndeterminate = true;
                ProgressValue = 0;
                DetailText = $"Downloading {FormatBytes(bytesReceived)}...";
            }
        });
    }

    public void Complete(Guid operationId, string assetKey)
    {
        Update(operationId, () =>
        {
            IsActive = true;
            IsBusy = false;
            IsIndeterminate = false;
            ProgressValue = 100;
            StatusText = "Asset loaded";
            DetailText = $"Added {assetKey} to the map.";
        });
        _ = ResetAfterDelayAsync(operationId);
    }

    public void Fail(Guid operationId, string detail) =>
        Update(operationId, () =>
        {
            IsActive = false;
            IsBusy = false;
            IsIndeterminate = false;
            ProgressValue = 0;
            StatusText = "Asset load failed";
            DetailText = detail;
        });

    public void Reset()
    {
        lock (_gate)
        {
            _currentOperationId = null;
            _expectedBytes = null;
        }
        RunOnUi(() =>
        {
            IsActive = false;
            IsBusy = false;
            IsIndeterminate = false;
            ProgressValue = 0;
            StatusText = "Ready";
            DetailText = string.Empty;
        });
    }

    private async Task ResetAfterDelayAsync(Guid operationId)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        Update(operationId, () =>
        {
            IsActive = false;
            ProgressValue = 0;
            StatusText = "Ready";
            DetailText = string.Empty;
        });
    }

    private void Update(Guid operationId, Action action) => RunOnUi(() =>
    {
        lock (_gate)
        {
            if (_currentOperationId != operationId)
                return;
        }
        action();
    });

    private void RunOnUi(Action action)
    {
        if (_dispatcher != null && !_dispatcher.CheckAccess())
            _dispatcher.BeginInvoke(action);
        else
            action();
    }

    private bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void NotifyPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private string WithExpectedSize(string detail) => _expectedBytes is > 0
        ? $"{detail}  •  {FormatBytes(_expectedBytes.Value)} expected"
        : detail;

    private static string Shorten(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..(maximumLength - 1)] + "…";

    internal static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:F0} {units[unit]}" : $"{value:F1} {units[unit]}";
    }
}
