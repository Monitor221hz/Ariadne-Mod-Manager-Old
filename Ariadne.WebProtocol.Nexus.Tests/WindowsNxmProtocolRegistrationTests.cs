using System.Runtime.Versioning;
using Ariadne.WebProtocol.Nexus;
using Microsoft.Win32;
using Xunit;

namespace Ariadne.WebProtocol.Nexus.Tests;

[SupportedOSPlatform("windows")]
public class WindowsNxmProtocolRegistrationTests
{
    private const string ProtocolKeyPath = @"Software\Classes\nxm";

    private static readonly (string? SubKey, string Name)[] CapturedValues =
    {
        (null, string.Empty),
        (null, "URL Protocol"),
        ("DefaultIcon", string.Empty),
        (@"shell\open\command", string.Empty),
    };

    private static NxmRegistrationOptions CreateOptions()
    {
        return new NxmRegistrationOptions
        {
            ApplicationName = $"AriadneNxmTests-{Guid.NewGuid():N}",
            ApplicationDescription = "Round-trip test entry",
            ExecutablePath = @"C:\Windows\notepad.exe",
        };
    }

    [SkippableFact]
    public void Register_ThenUnregister_RoundTrips()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var options = CreateOptions();
        using var snapshot = ProtocolKeySnapshot.Capture();
        var registration = new WindowsNxmProtocolRegistration(options);

        try
        {
            var initial = registration.GetState();
            Assert.False(initial.IsRegistered);
            Assert.False(initial.HasRegistrationEntries);

            Assert.True(registration.Register());
            Assert.True(registration.Register());

            var registered = registration.GetState();
            Assert.True(registered.IsRegistered);
            Assert.True(registered.HasRegistrationEntries);

            var removal = registration.Unregister();
            Assert.True(removal.Succeeded);
            Assert.True(removal.RemovedRegistration);
            Assert.False(removal.RetainedUserChoice);

            var cleaned = registration.GetState();
            Assert.False(cleaned.IsRegistered);
            Assert.False(cleaned.HasRegistrationEntries);
        }
        finally
        {
            registration.Unregister();
        }
    }

    [SkippableFact]
    public void Unregister_WithoutRegistration_SucceedsWithoutRemoving()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var registration = new WindowsNxmProtocolRegistration(CreateOptions());

        var result = registration.Unregister();

        Assert.True(result.Succeeded);
        Assert.False(result.RemovedRegistration);
        Assert.False(result.RetainedUserChoice);
    }

    [SkippableFact]
    public void GetDiagnosticReport_ContainsStatusAndExpectedCommand()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var options = CreateOptions();
        var registration = new WindowsNxmProtocolRegistration(options);

        var report = registration.GetDiagnosticReport();

        Assert.Contains("-- NXM Protocol --", report);
        Assert.Contains("Status:", report);
        Assert.Contains(options.Command, report);
    }

    private sealed class ProtocolKeySnapshot : IDisposable
    {
        private readonly bool _existed;
        private readonly string?[] _values;

        private ProtocolKeySnapshot(bool existed, string?[] values)
        {
            _existed = existed;
            _values = values;
        }

        public static ProtocolKeySnapshot Capture()
        {
            using var protocolKey = Registry.CurrentUser.OpenSubKey(ProtocolKeyPath);
            var values = new string?[CapturedValues.Length];
            for (var i = 0; i < CapturedValues.Length; i++)
            {
                values[i] = ReadValue(CapturedValues[i].SubKey, CapturedValues[i].Name);
            }
            return new ProtocolKeySnapshot(protocolKey is not null, values);
        }

        public void Dispose()
        {
            if (!_existed)
            {
                Registry.CurrentUser.DeleteSubKeyTree(ProtocolKeyPath, false);
                return;
            }

            using var protocolKey = Registry.CurrentUser.CreateSubKey(ProtocolKeyPath);
            for (var i = 0; i < CapturedValues.Length; i++)
            {
                var (subKey, name) = CapturedValues[i];
                using var createdKey = subKey is null ? null : protocolKey.CreateSubKey(subKey);
                var key = createdKey ?? protocolKey;
                var value = _values[i];
                if (value is null)
                {
                    key.DeleteValue(name, false);
                }
                else
                {
                    key.SetValue(name, value);
                }
            }
        }

        private static string? ReadValue(string? subKey, string name)
        {
            var path = subKey is null ? ProtocolKeyPath : $@"{ProtocolKeyPath}\{subKey}";
            using var key = Registry.CurrentUser.OpenSubKey(path);
            return key?.GetValue(name)?.ToString();
        }
    }
}
