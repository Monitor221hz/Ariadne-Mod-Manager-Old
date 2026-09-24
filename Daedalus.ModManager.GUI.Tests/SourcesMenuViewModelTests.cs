using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Daedalus.ModManager.GUI;
using Daedalus.ModManager.GUI.ViewModels;
using Daedalus.Security;
using Daedalus.WebProtocol.Modl;
using Daedalus.WebProtocol.Nexus;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

public class SourcesMenuViewModelTests
{
    private sealed class FakeNxmRegistration : INxmProtocolRegistration
    {
        private NxmAssociationState _state = new()
        {
            Status = NxmAssociationStatus.Unregistered,
            IsRegistered = false,
            HasRegistrationEntries = false,
            HandlerName = "Ariadne",
        };

        public int RegisterCalls { get; private set; }
        public int UnregisterCalls { get; private set; }

        public NxmAssociationState GetState()
        {
            return _state;
        }

        public bool Register()
        {
            RegisterCalls++;
            _state = _state with
            {
                Status = NxmAssociationStatus.Registered,
                IsRegistered = true,
                HasRegistrationEntries = true,
            };
            return true;
        }

        public NxmUnregistrationResult Unregister()
        {
            UnregisterCalls++;
            _state = _state with
            {
                Status = NxmAssociationStatus.Unregistered,
                IsRegistered = false,
                HasRegistrationEntries = false,
            };
            return new NxmUnregistrationResult(true, true, false);
        }

        public string GetDiagnosticReport()
        {
            return "nxm report";
        }

        public void OverrideWith(string handlerName)
        {
            _state = _state with
            {
                Status = NxmAssociationStatus.Overridden,
                IsRegistered = true,
                HasRegistrationEntries = true,
                HandlerName = handlerName,
            };
        }

        public void MarkIncomplete()
        {
            _state = _state with { Status = NxmAssociationStatus.Incomplete };
        }
    }

    private sealed class FakeModlRegistration : IModlProtocolRegistration
    {
        private ModlAssociationState _state = new()
        {
            Status = ModlAssociationStatus.Unregistered,
            IsRegistered = false,
            HasRegistrationEntries = false,
            HandlerName = "Ariadne",
        };

        public int RegisterCalls { get; private set; }
        public int UnregisterCalls { get; private set; }

        public ModlAssociationState GetState()
        {
            return _state;
        }

        public bool Register()
        {
            RegisterCalls++;
            _state = _state with
            {
                Status = ModlAssociationStatus.Registered,
                IsRegistered = true,
                HasRegistrationEntries = true,
            };
            return true;
        }

        public ModlUnregistrationResult Unregister()
        {
            UnregisterCalls++;
            _state = _state with
            {
                Status = ModlAssociationStatus.Unregistered,
                IsRegistered = false,
                HasRegistrationEntries = false,
            };
            return new ModlUnregistrationResult(true, true, false);
        }

        public string GetDiagnosticReport()
        {
            return "modl report";
        }
    }

    private sealed class FakeSsoSession : INexusSsoSession
    {
        public NexusSsoResult Result = NexusSsoResult.Authorized("fresh-key");

        public Uri AuthorizationUri { get; } =
            new("https://www.nexusmods.com/sso?id=stub&application=test");

        public Task<NexusSsoResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeSsoFactory : INexusSsoSessionFactory
    {
        public FakeSsoSession Session { get; } = new();
        public int Created { get; private set; }

        public INexusSsoSession Create()
        {
            Created++;
            return Session;
        }
    }

    private sealed class FakeAccountClient : INexusAccountClient
    {
        public NexusAccount? Account = new("Ron Example", true, null);
        public int ValidateCalls { get; private set; }

        public Task<NexusAccount?> ValidateAsync(
            string apiKey,
            CancellationToken cancellationToken = default
        )
        {
            ValidateCalls++;
            return Task.FromResult(Account);
        }
    }

    private sealed class FakeAccountCache : INexusAccountCache
    {
        private NexusAccount? _account;
        private string? _apiKey;
        public int Reads { get; private set; }

        public NexusAccount? Read(string apiKey)
        {
            Reads++;
            return _apiKey == apiKey ? _account : null;
        }

        public void Write(string apiKey, NexusAccount account)
        {
            _apiKey = apiKey;
            _account = account;
        }

        public void Clear()
        {
            _apiKey = null;
            _account = null;
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new();

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
        }

        public Task SetAsync(
            string key,
            string value,
            CancellationToken cancellationToken = default
        )
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUrlLauncher : IUrlLauncher
    {
        public List<string> Opened { get; } = [];

        public void Open(string url)
        {
            Opened.Add(url);
        }
    }

    private sealed class Fixture
    {
        public FakeNxmRegistration Nxm { get; } = new();
        public FakeModlRegistration Modl { get; } = new();
        public FakeSsoFactory Sso { get; } = new();
        public FakeAccountClient Accounts { get; } = new();
        public FakeAccountCache AccountCache { get; } = new();
        public InMemorySecretStore Secrets { get; } = new();
        public FakeUrlLauncher Launcher { get; } = new();
        public SourcesMenuViewModel ViewModel { get; }

        public Fixture()
        {
            ViewModel = new SourcesMenuViewModel(
                Nxm,
                Modl,
                Sso,
                Accounts,
                Secrets,
                Launcher,
                AccountCache
            );
        }
    }

    [Fact]
    public async Task Refresh_AppliesRegisteredStates()
    {
        var fixture = new Fixture();
        fixture.Nxm.Register();

        await fixture.ViewModel.RefreshCommand.Execute();

        Assert.True(fixture.ViewModel.NxmChecked);
        Assert.True(fixture.ViewModel.NxmToggleAllowed);
        Assert.False(fixture.ViewModel.ModlChecked);
        Assert.Null(fixture.ViewModel.NxmStatusText);
    }

    [Fact]
    public async Task Refresh_OverriddenState_DisablesToggleWithExplanation()
    {
        var fixture = new Fixture();
        fixture.Nxm.OverrideWith("Vortex");

        await fixture.ViewModel.RefreshCommand.Execute();

        Assert.True(fixture.ViewModel.NxmChecked);
        Assert.False(fixture.ViewModel.NxmToggleAllowed);
        Assert.Contains("Vortex", fixture.ViewModel.NxmStatusText);
        Assert.True(fixture.ViewModel.SettingsHintVisible);
    }

    [Fact]
    public async Task Refresh_IncompleteState_ShowsRepairHint()
    {
        var fixture = new Fixture();
        fixture.Nxm.MarkIncomplete();

        await fixture.ViewModel.RefreshCommand.Execute();

        Assert.False(fixture.ViewModel.NxmChecked);
        Assert.True(fixture.ViewModel.NxmToggleAllowed);
        Assert.Contains("repair", fixture.ViewModel.NxmStatusText);
        Assert.False(fixture.ViewModel.SettingsHintVisible);
    }

    [Fact]
    public async Task ToggleNxm_WhenUnregistered_RegistersAndChecks()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.RefreshCommand.Execute();

        await fixture.ViewModel.ToggleNxmCommand.Execute();

        Assert.Equal(1, fixture.Nxm.RegisterCalls);
        Assert.True(fixture.ViewModel.NxmChecked);
        Assert.Null(fixture.ViewModel.StatusText);
    }

    [Fact]
    public async Task ToggleNxm_WhenRegistered_UnregistersAndUnchecks()
    {
        var fixture = new Fixture();
        fixture.Nxm.Register();
        await fixture.ViewModel.RefreshCommand.Execute();

        await fixture.ViewModel.ToggleNxmCommand.Execute();

        Assert.Equal(1, fixture.Nxm.UnregisterCalls);
        Assert.False(fixture.ViewModel.NxmChecked);
    }

    [Fact]
    public async Task ToggleNxm_WhenOverridden_DoesNothing()
    {
        var fixture = new Fixture();
        fixture.Nxm.OverrideWith("Vortex");
        await fixture.ViewModel.RefreshCommand.Execute();

        await fixture.ViewModel.ToggleNxmCommand.Execute();

        Assert.Equal(0, fixture.Nxm.RegisterCalls);
        Assert.Equal(0, fixture.Nxm.UnregisterCalls);
    }

    [Fact]
    public async Task ToggleModl_WhenUnregistered_RegistersAndChecks()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.RefreshCommand.Execute();

        await fixture.ViewModel.ToggleModlCommand.Execute();

        Assert.Equal(1, fixture.Modl.RegisterCalls);
        Assert.True(fixture.ViewModel.ModlChecked);
    }

    [Fact]
    public async Task SignIn_Success_StoresKeyLaunchesUriAndShowsAccount()
    {
        var fixture = new Fixture();

        await fixture.ViewModel.SignInCommand.Execute();

        Assert.Equal(1, fixture.Sso.Created);
        var opened = Assert.Single(fixture.Launcher.Opened);
        Assert.StartsWith("https://www.nexusmods.com/sso", opened);
        Assert.Equal("fresh-key", await fixture.Secrets.GetAsync("nexus-api-key"));
        Assert.True(fixture.ViewModel.SignedIn);
        Assert.Equal("Signed in as Ron Example (Premium)", fixture.ViewModel.AccountStatusText);
        Assert.False(fixture.ViewModel.SignInBusy);
        Assert.Null(fixture.ViewModel.StatusText);
    }

    [Fact]
    public async Task SignIn_Failure_KeepsSignedOutWithError()
    {
        var fixture = new Fixture();
        fixture.Sso.Session.Result = NexusSsoResult.Failed("denied");

        await fixture.ViewModel.SignInCommand.Execute();

        Assert.False(fixture.ViewModel.SignedIn);
        Assert.Equal("denied", fixture.ViewModel.StatusText);
        Assert.Null(await fixture.Secrets.GetAsync("nexus-api-key"));
    }

    [Fact]
    public async Task Refresh_WithValidStoredKey_SignsInWithoutSso()
    {
        var fixture = new Fixture();
        await fixture.Secrets.SetAsync("nexus-api-key", "stored-key");

        await fixture.ViewModel.RefreshCommand.Execute();

        Assert.Equal(0, fixture.Sso.Created);
        Assert.Empty(fixture.Launcher.Opened);
        Assert.True(fixture.ViewModel.SignedIn);
        Assert.Equal("Signed in as Ron Example (Premium)", fixture.ViewModel.AccountStatusText);
    }

    [Fact]
    public async Task Refresh_WithRejectedStoredKey_ClearsKeyAndSignsOut()
    {
        var fixture = new Fixture();
        fixture.Accounts.Account = null;
        await fixture.Secrets.SetAsync("nexus-api-key", "dead-key");

        await fixture.ViewModel.RefreshCommand.Execute();

        Assert.False(fixture.ViewModel.SignedIn);
        Assert.Null(await fixture.Secrets.GetAsync("nexus-api-key"));
        Assert.Null(fixture.AccountCache.Read("dead-key"));
        Assert.Contains("sign in again", fixture.ViewModel.StatusText);
    }

    [Fact]
    public async Task Refresh_TwiceWithSameKey_ValidatesOnlyOnce()
    {
        var fixture = new Fixture();
        await fixture.Secrets.SetAsync("nexus-api-key", "stored-key");

        await fixture.ViewModel.RefreshCommand.Execute();
        await fixture.ViewModel.RefreshCommand.Execute();

        Assert.Equal(1, fixture.Accounts.ValidateCalls);
    }

    [Fact]
    public async Task Refresh_KeyChanged_RevalidatesOnceForNewKey()
    {
        var fixture = new Fixture();
        await fixture.Secrets.SetAsync("nexus-api-key", "first-key");
        await fixture.ViewModel.RefreshCommand.Execute();

        await fixture.Secrets.SetAsync("nexus-api-key", "second-key");
        await fixture.ViewModel.RefreshCommand.Execute();

        Assert.Equal(2, fixture.Accounts.ValidateCalls);
        Assert.NotNull(fixture.AccountCache.Read("second-key"));
        Assert.True(fixture.ViewModel.SignedIn);
    }

    [Fact]
    public async Task SignOut_RemovesKeyAndResetsAccount()
    {
        var fixture = new Fixture();
        await fixture.ViewModel.SignInCommand.Execute();

        await fixture.ViewModel.SignOutCommand.Execute();

        Assert.False(fixture.ViewModel.SignedIn);
        Assert.Equal("Not signed in", fixture.ViewModel.AccountStatusText);
        Assert.Null(await fixture.Secrets.GetAsync("nexus-api-key"));
    }

    [Fact]
    public async Task ShowNxmDiagnostics_ReportsThroughInteraction()
    {
        var fixture = new Fixture();
        (string Title, string Text)? shown = null;
        fixture.ViewModel.ShowInfo.RegisterHandler(context =>
        {
            shown = context.Input;
            context.SetOutput(Unit.Default);
        });

        await fixture.ViewModel.ShowNxmDiagnosticsCommand.Execute();

        Assert.NotNull(shown);
        Assert.Equal("nxm report", shown.Value.Text);
    }

#if DEBUG
    [Fact]
    public async Task Refresh_WithEnvironmentKey_SignsInMarkingDevSource()
    {
        SetDevKey("dev-key");
        try
        {
            var fixture = new Fixture();

            await fixture.ViewModel.RefreshCommand.Execute();

            Assert.True(fixture.ViewModel.SignedIn);
            Assert.True(fixture.ViewModel.UsingEnvironmentKey);
            Assert.Equal(
                "Signed in as Ron Example (Premium) - dev key",
                fixture.ViewModel.AccountStatusText
            );
            Assert.Equal(0, fixture.Sso.Created);
            Assert.Empty(fixture.Launcher.Opened);
            Assert.Null(await fixture.Secrets.GetAsync("nexus-api-key"));
        }
        finally
        {
            SetDevKey(null);
        }
    }

    [Fact]
    public async Task Refresh_WithRejectedEnvironmentKey_SignsOutWithError()
    {
        SetDevKey("dev-key");
        try
        {
            var fixture = new Fixture();
            fixture.Accounts.Account = null;

            await fixture.ViewModel.RefreshCommand.Execute();

            Assert.False(fixture.ViewModel.SignedIn);
            Assert.Contains(
                SourcesMenuViewModel.DevKeyEnvironmentVariable,
                fixture.ViewModel.StatusText
            );
        }
        finally
        {
            SetDevKey(null);
        }
    }

    [Fact]
    public async Task SignIn_WithEnvironmentKey_DoesNotStartSso()
    {
        SetDevKey("dev-key");
        try
        {
            var fixture = new Fixture();
            await fixture.ViewModel.RefreshCommand.Execute();

            await fixture.ViewModel.SignInCommand.Execute();

            Assert.Equal(0, fixture.Sso.Created);
            Assert.Empty(fixture.Launcher.Opened);
            Assert.Contains(
                SourcesMenuViewModel.DevKeyEnvironmentVariable,
                fixture.ViewModel.StatusText
            );
        }
        finally
        {
            SetDevKey(null);
        }
    }

    [Fact]
    public async Task SignOut_WithEnvironmentKey_RemainsSignedInWithNotice()
    {
        SetDevKey("dev-key");
        try
        {
            var fixture = new Fixture();
            await fixture.ViewModel.RefreshCommand.Execute();

            await fixture.ViewModel.SignOutCommand.Execute();

            Assert.True(fixture.ViewModel.SignedIn);
            Assert.Contains(
                SourcesMenuViewModel.DevKeyEnvironmentVariable,
                fixture.ViewModel.StatusText
            );
        }
        finally
        {
            SetDevKey(null);
        }
    }

    private static void SetDevKey(string? value)
    {
        Environment.SetEnvironmentVariable(SourcesMenuViewModel.DevKeyEnvironmentVariable, value);
    }
#endif
}
