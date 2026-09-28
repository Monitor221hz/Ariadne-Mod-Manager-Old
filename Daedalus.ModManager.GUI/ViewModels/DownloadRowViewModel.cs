using System.Globalization;
using System.Reactive;
using ByteSizeLib;
using Daedalus.Contracts.ModManager;
using Daedalus.Downloads;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class DownloadRowViewModel : ViewModelBase
{
    private DownloadJob _job;
    private double _percentage;
    private long _receivedBytes;
    private string _receivedText = "";
    private string _speedText = "";
    private string _etaText = "--";
    private string _detailText = "";
    private bool _isInstalling;
    private bool _didInstall;
    private bool _installFailed;
    private double _installPercentage;

    public DownloadRowViewModel(DownloadJob job, Action<Guid> cancelHandler)
    {
        _job = job;
        Apply(job);
        CancelCommand = ReactiveCommand.Create(() =>
        {
            cancelHandler(job.Id);
        });
        InstallCommand = ReactiveCommand.Create(() => { });
    }

    public DownloadJob Job => _job;
    public string DisplayName => _job.DisplayName;

    public double Percentage
    {
        get => _percentage;
        private set => this.RaiseAndSetIfChanged(ref _percentage, value);
    }

    public long ReceivedBytes
    {
        get => _receivedBytes;
        private set => this.RaiseAndSetIfChanged(ref _receivedBytes, value);
    }

    public string ReceivedText
    {
        get => _receivedText;
        private set => this.RaiseAndSetIfChanged(ref _receivedText, value);
    }

    public string SpeedText
    {
        get => _speedText;
        private set => this.RaiseAndSetIfChanged(ref _speedText, value);
    }

    public string EtaText
    {
        get => _etaText;
        private set => this.RaiseAndSetIfChanged(ref _etaText, value);
    }

    public string DetailText
    {
        get => _detailText;
        private set => this.RaiseAndSetIfChanged(ref _detailText, value);
    }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> InstallCommand { get; set; }

    public bool IsInstalling
    {
        get => _isInstalling;
        set
        {
            this.RaiseAndSetIfChanged(ref _isInstalling, value);
            this.RaisePropertyChanged(nameof(InstallVisible));
            this.RaisePropertyChanged(nameof(ProgressVisible));
            this.RaisePropertyChanged(nameof(ProgressValue));
        }
    }

    public double InstallPercentage
    {
        get => _installPercentage;
        private set => this.RaiseAndSetIfChanged(ref _installPercentage, value);
    }

    public void SetInstallProgress(InstallProgress progress)
    {
        DetailText = progress.EntryPath.Length > 0
            ? $"Installing {progress.EntryPath}"
            : "Installing";
        if (progress.ProgressPercentage is { } percentage)
        {
            InstallPercentage = percentage;
            this.RaisePropertyChanged(nameof(ProgressValue));
        }
    }

    public double ProgressValue => IsInstalling ? InstallPercentage : Percentage;

    public bool ProgressVisible => IsRunning || IsInstalling;

    public bool DidInstall
    {
        get => _didInstall;
        set
        {
            this.RaiseAndSetIfChanged(ref _didInstall, value);
            this.RaisePropertyChanged(nameof(InstallVisible));
            this.RaisePropertyChanged(nameof(StatusText));
        }
    }

    public bool InstallFailed
    {
        get => _installFailed;
        set
        {
            this.RaiseAndSetIfChanged(ref _installFailed, value);
            this.RaisePropertyChanged(nameof(StatusText));
        }
    }

    public bool IsCompleted => _job.Status is DownloadJobStatus.Completed;

    public bool InstallVisible => IsCompleted && !IsInstalling && !DidInstall;

    public bool IsRunning => _job.Status is DownloadJobStatus.Running;
    public bool IsQueued => _job.Status is DownloadJobStatus.Queued;
    public bool IsFinished =>
        _job.Status
            is DownloadJobStatus.Completed
                or DownloadJobStatus.Failed
                or DownloadJobStatus.Cancelled
                or DownloadJobStatus.IntegrityMismatch;

    public string StatusText =>
        DidInstall
            ? "Installed"
            : InstallFailed
                ? "Install failed"
                : _job.Status switch
                {
                DownloadJobStatus.Queued => "Queued",
                DownloadJobStatus.Running => "Downloading",
                DownloadJobStatus.Completed => "Completed",
                DownloadJobStatus.Cancelled => "Cancelled",
                DownloadJobStatus.IntegrityMismatch => "Checksum mismatch",
                _ => $"Failed: {_job.Error}",
            };

    public void Apply(DownloadJob job)
    {
        _job = job;
        if (job.Status is DownloadJobStatus.Completed && job.TotalBytes > 0)
        {
            Percentage = 100;
            ReceivedBytes = job.TotalBytes;
            SpeedText = "";
            EtaText = "--";
        }
        ReceivedText =
            job.TotalBytes > 0
                ? $"{FormatBytes(ReceivedBytes)} of {FormatBytes(job.TotalBytes)}"
                : FormatBytes(ReceivedBytes);
        DetailText = ReceivedText;
        this.RaisePropertyChanged(nameof(Job));
        this.RaisePropertyChanged(nameof(DisplayName));
        this.RaisePropertyChanged(nameof(IsRunning));
        this.RaisePropertyChanged(nameof(IsQueued));
        this.RaisePropertyChanged(nameof(IsFinished));
        this.RaisePropertyChanged(nameof(IsCompleted));
        this.RaisePropertyChanged(nameof(InstallVisible));
        this.RaisePropertyChanged(nameof(StatusText));
    }

    public void ApplyProgress(DownloadProgress progress)
    {
        if (!IsRunning && !IsQueued)
        {
            return;
        }
        Percentage = progress.Percentage;
        ReceivedBytes = progress.ReceivedBytes;
        ReceivedText =
            progress.TotalBytes > 0
                ? $"{FormatBytes(progress.ReceivedBytes)} of {FormatBytes(progress.TotalBytes)}"
                : FormatBytes(progress.ReceivedBytes);
        SpeedText = progress.BytesPerSecond > 0 ? $"{FormatBytes(progress.BytesPerSecond)}/s" : "";
        EtaText = ComputeEtaText(progress);
        DetailText = ReceivedText + SpeedSuffix() + EtaSuffix();
    }

    private string SpeedSuffix()
    {
        return SpeedText.Length > 0 ? $" · {SpeedText}" : "";
    }

    private string EtaSuffix()
    {
        return EtaText is "--" ? "" : $" · ~{EtaText} left";
    }

    private static string ComputeEtaText(DownloadProgress progress)
    {
        if (progress.TotalBytes <= 0 || progress.BytesPerSecond <= 0)
        {
            return "--";
        }
        var remainingSeconds =
            (progress.TotalBytes - progress.ReceivedBytes) / progress.BytesPerSecond;
        if (!double.IsFinite(remainingSeconds) || remainingSeconds < 0)
        {
            return "--";
        }
        var remaining = TimeSpan.FromSeconds(Math.Ceiling(remainingSeconds));
        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        }
        if (remaining.TotalMinutes >= 1)
        {
            return $"{remaining.Minutes}m {remaining.Seconds}s";
        }
        return remaining.TotalSeconds < 1 ? "<1s" : $"{remaining.Seconds}s";
    }

    private static string FormatBytes(double bytes)
    {
        return bytes < 1024
            ? ByteSize.FromBytes(bytes).ToString("0", CultureInfo.InvariantCulture)
            : ByteSize.FromBytes(bytes).ToString("0.0", CultureInfo.InvariantCulture);
    }
}
