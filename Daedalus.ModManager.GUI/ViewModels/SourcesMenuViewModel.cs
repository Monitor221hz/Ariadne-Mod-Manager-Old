using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using Daedalus.Security;
using Daedalus.WebProtocol.Modl;
using Daedalus.WebProtocol.Nexus;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class SourcesMenuViewModel : ViewModelBase
{
    public const string DevKeyEnvironmentVariable = "DAEDALUS_NEXUS_API_KEY";
    private const string ApiKeySecretName = "nexus-api-key";

    private readonly INxmProtocolRegistration? _nxmRegistration;
    private readonly IModlProtocolRegistration? _modlRegistration;
    private readonly INexusSsoSessionFactory? _ssoSessions;
    private readonly INexusAccountClient? _accountClient;
    private readonly INexusAccountCache? _accountCache;
    private readonly ISecretStore? _secrets;
    private readonly IUrlLauncher? _urlLauncher;

    private CancellationTokenSource? _signInCancellation;
    private NxmAssociationState? _nxmState;
    private ModlAssociationState? _modlState;
    private string? _nxmStatusText;
    private string? _modlStatusText;
    private string? _statusText;
    private string _accountStatusText = "Not signed in";
    private string _signInText = "Sign in to Nexus Mods…";
    private bool _nxmChecked;
    private bool _nxmToggleAllowed;
    private bool _modlChecked;
    private bool _modlToggleAllowed;
    private bool _settingsHintVisible;
    private bool _signedIn;
    private bool _signInBusy;
    private bool _usingEnvironmentKey;

    public SourcesMenuViewModel()
    {
        RefreshCommand = ReactiveCommand.Create(() => { });
        ToggleNxmCommand = ReactiveCommand.Create(() => { });
        ToggleModlCommand = ReactiveCommand.Create(() => { });
        SignInCommand = ReactiveCommand.Create(() => { });
        SignOutCommand = ReactiveCommand.Create(() => { });
        OpenDefaultAppsCommand = ReactiveCommand.Create(() => { });
        ShowNxmDiagnosticsCommand = ReactiveCommand.Create(() => { });
        ShowModlDiagnosticsCommand = ReactiveCommand.Create(() => { });

        AssociationSupported = OperatingSystem.IsWindows();
    }

    public SourcesMenuViewModel(
        INxmProtocolRegistration nxmRegistration,
        IModlProtocolRegistration modlRegistration,
        INexusSsoSessionFactory ssoSessions,
        INexusAccountClient accountClient,
        ISecretStore secrets,
        IUrlLauncher urlLauncher,
        INexusAccountCache accountCache
    )
    {
        _nxmRegistration = nxmRegistration;
        _modlRegistration = modlRegistration;
        _ssoSessions = ssoSessions;
        _accountClient = accountClient;
        _secrets = secrets;
        _urlLauncher = urlLauncher;
        _accountCache = accountCache;

        AssociationSupported = OperatingSystem.IsWindows();

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        ToggleNxmCommand = ReactiveCommand.CreateFromTask(
            ToggleNxmAsync,
            this.WhenAnyValue(x => x.NxmToggleAllowed)
        );
        ToggleModlCommand = ReactiveCommand.CreateFromTask(
            ToggleModlAsync,
            this.WhenAnyValue(x => x.ModlToggleAllowed)
        );
        SignInCommand = ReactiveCommand.CreateFromTask(SignInAsync);
        SignOutCommand = ReactiveCommand.CreateFromTask(SignOutAsync);
        OpenDefaultAppsCommand = ReactiveCommand.Create(OpenDefaultAppsSettings);
        ShowNxmDiagnosticsCommand = ReactiveCommand.CreateFromTask(ShowNxmDiagnosticsAsync);
        ShowModlDiagnosticsCommand = ReactiveCommand.CreateFromTask(ShowModlDiagnosticsAsync);

        MonitorThrownExceptions(RefreshCommand);
        MonitorThrownExceptions(ToggleNxmCommand);
        MonitorThrownExceptions(ToggleModlCommand);
        MonitorThrownExceptions(SignInCommand);
        MonitorThrownExceptions(SignOutCommand);
        MonitorThrownExceptions(ShowNxmDiagnosticsCommand);
        MonitorThrownExceptions(ShowModlDiagnosticsCommand);
    }

    public bool AssociationSupported { get; }

    public bool NxmChecked
    {
        get => _nxmChecked;
        private set => this.RaiseAndSetIfChanged(ref _nxmChecked, value);
    }

    public bool NxmToggleAllowed
    {
        get => _nxmToggleAllowed;
        private set => this.RaiseAndSetIfChanged(ref _nxmToggleAllowed, value);
    }

    public string? NxmStatusText
    {
        get => _nxmStatusText;
        private set => this.RaiseAndSetIfChanged(ref _nxmStatusText, value);
    }

    public bool ModlChecked
    {
        get => _modlChecked;
        private set => this.RaiseAndSetIfChanged(ref _modlChecked, value);
    }

    public bool ModlToggleAllowed
    {
        get => _modlToggleAllowed;
        private set => this.RaiseAndSetIfChanged(ref _modlToggleAllowed, value);
    }

    public string? ModlStatusText
    {
        get => _modlStatusText;
        private set => this.RaiseAndSetIfChanged(ref _modlStatusText, value);
    }

    public bool SettingsHintVisible
    {
        get => _settingsHintVisible;
        private set => this.RaiseAndSetIfChanged(ref _settingsHintVisible, value);
    }

    public bool SignedIn
    {
        get => _signedIn;
        private set => this.RaiseAndSetIfChanged(ref _signedIn, value);
    }

    public bool UsingEnvironmentKey
    {
        get => _usingEnvironmentKey;
        private set => this.RaiseAndSetIfChanged(ref _usingEnvironmentKey, value);
    }

    public string AccountStatusText
    {
        get => _accountStatusText;
        private set => this.RaiseAndSetIfChanged(ref _accountStatusText, value);
    }

    public string SignInText
    {
        get => _signInText;
        private set => this.RaiseAndSetIfChanged(ref _signInText, value);
    }

    public bool SignInBusy
    {
        get => _signInBusy;
        private set => this.RaiseAndSetIfChanged(ref _signInBusy, value);
    }

    public string? StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleNxmCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleModlCommand { get; }
    public ReactiveCommand<Unit, Unit> SignInCommand { get; }
    public ReactiveCommand<Unit, Unit> SignOutCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDefaultAppsCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowNxmDiagnosticsCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowModlDiagnosticsCommand { get; }

    public Interaction<(string Title, string Text), Unit> ShowInfo { get; } = new();

    private static void MonitorThrownExceptions(ReactiveCommand<Unit, Unit> command)
    {
        command.ThrownExceptions.Subscribe(exception => Debug.WriteLine(exception));
    }

    private async Task RefreshAsync()
    {
        await Task.Run(RefreshProtocolStates);
        await RefreshAccountAsync(CancellationToken.None);
    }

    private void RefreshProtocolStates()
    {
        if (_nxmRegistration is not null)
        {
            ApplyNxmState(_nxmRegistration.GetState());
        }
        if (_modlRegistration is not null)
        {
            ApplyModlState(_modlRegistration.GetState());
        }
    }

    private void ApplyNxmState(NxmAssociationState state)
    {
        _nxmState = state;
        NxmChecked = state.IsRegistered;

        var overridden = state.Status is NxmAssociationStatus.Overridden;
        NxmToggleAllowed = !overridden;
        NxmStatusText = state.Status switch
        {
            NxmAssociationStatus.Overridden =>
                $"Handled by {state.HandlerName} - change the default in Windows Settings",
            NxmAssociationStatus.Incomplete => "Registration incomplete - click to repair",
            _ => null,
        };
        UpdateSettingsHint();
    }

    private void ApplyModlState(ModlAssociationState state)
    {
        _modlState = state;
        ModlChecked = state.IsRegistered;

        var overridden = state.Status is ModlAssociationStatus.Overridden;
        ModlToggleAllowed = !overridden;
        ModlStatusText = state.Status switch
        {
            ModlAssociationStatus.Overridden =>
                $"Handled by {state.HandlerName} - change the default in Windows Settings",
            ModlAssociationStatus.Incomplete => "Registration incomplete - click to repair",
            _ => null,
        };
        UpdateSettingsHint();
    }

    private void UpdateSettingsHint()
    {
        SettingsHintVisible =
            _nxmState?.Status is NxmAssociationStatus.Overridden
            || _modlState?.Status is ModlAssociationStatus.Overridden;
    }

    private async Task ToggleNxmAsync()
    {
        if (_nxmRegistration is null || _nxmState is null || !NxmToggleAllowed)
        {
            return;
        }

        StatusText = null;
        if (_nxmState.IsRegistered)
        {
            var removal = await Task.Run(_nxmRegistration.Unregister);
            if (!removal.Succeeded)
            {
                StatusText = "Failed to remove the nxm:// association.";
            }
            else if (removal.RetainedUserChoice)
            {
                StatusText =
                    "Windows kept your previous default choice for nxm links. Change it in Windows Settings → Default apps.";
            }
        }
        else
        {
            var registered = await Task.Run(_nxmRegistration.Register);
            if (!registered)
            {
                StatusText = "Failed to register the nxm:// association.";
            }
        }

        if (_nxmRegistration is not null)
        {
            await Task.Run(() => ApplyNxmState(_nxmRegistration.GetState()));
        }
    }

    private async Task ToggleModlAsync()
    {
        if (_modlRegistration is null || _modlState is null || !ModlToggleAllowed)
        {
            return;
        }

        StatusText = null;
        if (_modlState.IsRegistered)
        {
            var removal = await Task.Run(_modlRegistration.Unregister);
            if (!removal.Succeeded)
            {
                StatusText = "Failed to remove the modl:// association.";
            }
            else if (removal.RetainedUserChoice)
            {
                StatusText =
                    "Windows kept your previous default choice for modl links. Change it in Windows Settings → Default apps.";
            }
        }
        else
        {
            var registered = await Task.Run(_modlRegistration.Register);
            if (!registered)
            {
                StatusText = "Failed to register the modl:// association.";
            }
        }

        if (_modlRegistration is not null)
        {
            await Task.Run(() => ApplyModlState(_modlRegistration.GetState()));
        }
    }

    private async Task SignInAsync()
    {
        if (_ssoSessions is null || _secrets is null || _urlLauncher is null)
        {
            return;
        }

        if (UsingEnvironmentKey)
        {
            StatusText =
                $"A development API key is active via the {DevKeyEnvironmentVariable} environment variable; unset it to sign in.";
            return;
        }

        if (_signInCancellation is not null)
        {
            _signInCancellation.Cancel();
            return;
        }

        StatusText = null;
        SignInBusy = true;
        SignInText = "Signing in - click to cancel";
        using var cancellation = new CancellationTokenSource();
        _signInCancellation = cancellation;
        try
        {
            var session = _ssoSessions.Create();
            _urlLauncher.Open(session.AuthorizationUri.ToString());

            var result = await session.ConnectAsync(cancellation.Token);
            if (!result.Succeeded)
            {
                StatusText = result.Error ?? "Nexus Mods sign-in failed.";
                return;
            }

            await _secrets.SetAsync(ApiKeySecretName, result.ApiKey!, cancellation.Token);
            await RefreshAccountAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Sign-in cancelled.";
        }
        finally
        {
            _signInCancellation = null;
            SignInBusy = false;
            SignInText = "Sign in to Nexus Mods…";
        }
    }

    private async Task SignOutAsync()
    {
        if (_secrets is null)
        {
            return;
        }

        await _secrets.DeleteAsync(ApiKeySecretName);
        _accountCache?.Clear();
        if (UsingEnvironmentKey)
        {
            StatusText =
                $"A development API key is active via the {DevKeyEnvironmentVariable} environment variable; it remains in effect until the variable is unset.";
            return;
        }
        SignedIn = false;
        AccountStatusText = "Not signed in";
    }

    private async Task RefreshAccountAsync(CancellationToken cancellationToken)
    {
        if (_secrets is null || _accountClient is null || _accountCache is null)
        {
            return;
        }

        var apiKey = GetEnvironmentApiKey();
        UsingEnvironmentKey = apiKey is not null;
        apiKey ??= await _secrets.GetAsync(ApiKeySecretName, cancellationToken);
        if (apiKey is null)
        {
            _accountCache.Clear();
            SignedIn = false;
            AccountStatusText = "Not signed in";
            return;
        }

        var account = _accountCache.Read(apiKey);
        if (account is null)
        {
            account = await _accountClient.ValidateAsync(apiKey, cancellationToken);
            if (account is null)
            {
                if (!UsingEnvironmentKey)
                {
                    await _secrets.DeleteAsync(ApiKeySecretName, cancellationToken);
                    StatusText = "Stored Nexus Mods API key was rejected; please sign in again.";
                }
                else
                {
                    StatusText =
                        $"The development API key in {DevKeyEnvironmentVariable} was rejected by Nexus Mods.";
                }
                _accountCache.Clear();
                SignedIn = false;
                AccountStatusText = "Not signed in";
                return;
            }
            _accountCache.Write(apiKey, account);
        }

        SignedIn = true;
        AccountStatusText = account.IsPremium
            ? $"Signed in as {account.Name} (Premium)"
            : $"Signed in as {account.Name}";
        if (UsingEnvironmentKey)
        {
            AccountStatusText += " - dev key";
        }
    }

    private static string? GetEnvironmentApiKey()
    {
#if DEBUG
        var key = Environment.GetEnvironmentVariable(DevKeyEnvironmentVariable);
        return string.IsNullOrWhiteSpace(key) ? null : key;
#else
        return null;
#endif
    }

    private void OpenDefaultAppsSettings()
    {
        _urlLauncher?.Open("ms-settings:defaultapps");
    }

    private async Task ShowNxmDiagnosticsAsync()
    {
        if (_nxmRegistration is null)
        {
            return;
        }

        var report = await Task.Run(_nxmRegistration.GetDiagnosticReport);
        await ShowInfo.Handle(("nxm:// association report", report));
    }

    private async Task ShowModlDiagnosticsAsync()
    {
        if (_modlRegistration is null)
        {
            return;
        }

        var report = await Task.Run(_modlRegistration.GetDiagnosticReport);
        await ShowInfo.Handle(("modl:// association report", report));
    }
}
