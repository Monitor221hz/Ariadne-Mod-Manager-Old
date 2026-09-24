using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace Daedalus.WebProtocol.Nexus;

[SupportedOSPlatform("windows")]
public sealed class WindowsNxmProtocolRegistration : INxmProtocolRegistration
{
    private const string ProtocolName = "nxm";
    private const string ProtocolDescription = "URL:Nexus Mods Protocol";
    private const string ClassesPath = @"Software\Classes";
    private const string UrlAssociationsSubKey = "UrlAssociations";
    private const string RegisteredApplicationsPath = @"Software\RegisteredApplications";
    private const string UserChoicePath =
        @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\nxm\UserChoice";

    private readonly NxmRegistrationOptions _options;

    private string CapabilitiesPath => $@"{_options.SoftwarePath}\Capabilities";

    public WindowsNxmProtocolRegistration(NxmRegistrationOptions options)
    {
        _options = options;
    }

    public bool Register()
    {
        try
        {
            using (
                var progIdKey = Registry.CurrentUser.CreateSubKey(
                    $@"{ClassesPath}\{_options.ProgId}"
                )
            )
            {
                progIdKey.SetValue(string.Empty, ProtocolDescription);
                progIdKey.SetValue("URL Protocol", string.Empty);

                using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
                {
                    iconKey.SetValue(string.Empty, _options.Icon);
                }
                using (var commandKey = progIdKey.CreateSubKey(@"shell\open\command"))
                {
                    commandKey.SetValue(string.Empty, _options.Command);
                }
            }

            using (
                var protocolKey = Registry.CurrentUser.CreateSubKey(
                    $@"{ClassesPath}\{ProtocolName}"
                )
            )
            {
                protocolKey.SetValue(string.Empty, ProtocolDescription);
                protocolKey.SetValue("URL Protocol", string.Empty);

                using (var iconKey = protocolKey.CreateSubKey("DefaultIcon"))
                {
                    iconKey.SetValue(string.Empty, _options.Icon);
                }
                using (var commandKey = protocolKey.CreateSubKey(@"shell\open\command"))
                {
                    commandKey.SetValue(string.Empty, _options.Command);
                }
            }

            using (var capabilitiesKey = Registry.CurrentUser.CreateSubKey(CapabilitiesPath))
            {
                capabilitiesKey.SetValue("ApplicationName", _options.ApplicationName);
                capabilitiesKey.SetValue("ApplicationDescription", _options.ApplicationDescription);
                capabilitiesKey.SetValue("ApplicationIcon", _options.Icon);

                using (var urlAssociationsKey = capabilitiesKey.CreateSubKey(UrlAssociationsSubKey))
                {
                    urlAssociationsKey.SetValue(ProtocolName, _options.ProgId);
                }
            }

            using (
                var registeredApplicationsKey = Registry.CurrentUser.CreateSubKey(
                    RegisteredApplicationsPath
                )
            )
            {
                registeredApplicationsKey.SetValue(_options.ApplicationName, CapabilitiesPath);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public NxmUnregistrationResult Unregister()
    {
        var removedRegistration = false;
        var retainedUserChoice = false;

        try
        {
            var expectedCommand = _options.Command;

            var userChoiceProgId = GetUserChoiceProgId();
            if (
                !string.IsNullOrEmpty(userChoiceProgId)
                && IsOwnHandler(userChoiceProgId, expectedCommand)
            )
            {
                if (TryDeleteUserChoice())
                {
                    removedRegistration = true;
                }
                else
                {
                    retainedUserChoice = true;
                }
            }

            if (DeleteUserSubKeyTree($@"{ClassesPath}\{_options.ProgId}"))
            {
                removedRegistration = true;
            }

            if (
                IsExpectedCommand(GetUserCommandForProgId(ProtocolName), expectedCommand)
                && DeleteUserSubKeyTree($@"{ClassesPath}\{ProtocolName}")
            )
            {
                removedRegistration = true;
            }

            if (RemoveCapabilities())
            {
                removedRegistration = true;
            }
        }
        catch (Exception)
        {
            return new NxmUnregistrationResult(false, removedRegistration, retainedUserChoice);
        }

        return new NxmUnregistrationResult(true, removedRegistration, retainedUserChoice);
    }

    public NxmAssociationState GetState()
    {
        try
        {
            var expectedCommand = _options.Command;
            var hasProgIdKey = IsExpectedCommand(
                GetCommandForProgId(_options.ProgId),
                expectedCommand
            );
            var hasProtocolKey = IsExpectedCommand(
                GetCommandForProgId(ProtocolName),
                expectedCommand
            );
            var isRegistered = hasProgIdKey && hasProtocolKey && HasCapabilities();

            var status = NxmAssociationStatus.Unregistered;
            if (isRegistered)
            {
                status = NxmAssociationStatus.Registered;
            }
            else if (hasProgIdKey || hasProtocolKey)
            {
                status = NxmAssociationStatus.Incomplete;
            }

            var userChoiceProgId = GetUserChoiceProgId();
            if (
                string.IsNullOrEmpty(userChoiceProgId)
                || string.Equals(userChoiceProgId, ProtocolName, StringComparison.OrdinalIgnoreCase)
            )
            {
                return new NxmAssociationState
                {
                    Status = status,
                    IsRegistered = isRegistered,
                    HasRegistrationEntries = HasRegistrationEntries(expectedCommand),
                    HandlerName = _options.ApplicationName,
                };
            }

            var userChoiceCommand = GetCommandForProgId(userChoiceProgId);
            if (
                string.Equals(userChoiceProgId, _options.ProgId, StringComparison.OrdinalIgnoreCase)
                || IsExpectedCommand(userChoiceCommand, expectedCommand)
            )
            {
                return new NxmAssociationState
                {
                    Status = status,
                    IsRegistered = isRegistered,
                    HasRegistrationEntries = HasRegistrationEntries(expectedCommand),
                    UserChoiceProgId = userChoiceProgId,
                    UserChoiceCommand = userChoiceCommand,
                    HandlerName = _options.ApplicationName,
                };
            }

            return new NxmAssociationState
            {
                Status = NxmAssociationStatus.Overridden,
                IsRegistered = isRegistered,
                HasRegistrationEntries = HasRegistrationEntries(expectedCommand),
                UserChoiceProgId = userChoiceProgId,
                UserChoiceCommand = userChoiceCommand,
                HandlerName = GetDisplayName(userChoiceProgId, userChoiceCommand),
            };
        }
        catch (Exception)
        {
            return new NxmAssociationState
            {
                Status = NxmAssociationStatus.Unregistered,
                IsRegistered = false,
                HasRegistrationEntries = false,
                HandlerName = _options.ApplicationName,
            };
        }
    }

    public string GetDiagnosticReport()
    {
        var state = GetState();

        var report = new StringBuilder();
        report.AppendLine("-- NXM Protocol --");
        report.AppendLine($"Status: {state.Status}");
        report.AppendLine($"Expected Command: {_options.Command}");
        report.AppendLine($"UserChoice ProgId: {GetLoggableValue(state.UserChoiceProgId)}");
        report.AppendLine($"UserChoice Command: {GetLoggableValue(state.UserChoiceCommand)}");
        report.AppendLine(
            $"ProgId Command: {GetLoggableValue(GetCommandForProgId(_options.ProgId))}"
        );
        report.AppendLine(
            $"Protocol Key Command: {GetLoggableValue(GetCommandForProgId(ProtocolName))}"
        );
        report.AppendLine(
            $"Machine Protocol Key Command: {GetLoggableValue(GetMachineCommandForProgId(ProtocolName))}"
        );
        report.AppendLine($"Capabilities Registered: {HasCapabilities()}");
        report.AppendLine($"Registration Entries Present: {state.HasRegistrationEntries}");
        report.Append($"Resolved Handler: {state.HandlerName}");

        return report.ToString();
    }

    private bool HasCapabilities()
    {
        using var urlAssociationsKey = Registry.CurrentUser.OpenSubKey(
            $@"{CapabilitiesPath}\{UrlAssociationsSubKey}"
        );
        if (
            !string.Equals(
                urlAssociationsKey?.GetValue(ProtocolName)?.ToString(),
                _options.ProgId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        using var registeredApplicationsKey = Registry.CurrentUser.OpenSubKey(
            RegisteredApplicationsPath
        );
        return string.Equals(
            registeredApplicationsKey?.GetValue(_options.ApplicationName)?.ToString(),
            CapabilitiesPath,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private bool HasRegistrationEntries(string expectedCommand)
    {
        if (HasUserSubKey($@"{ClassesPath}\{_options.ProgId}") || HasUserSubKey(CapabilitiesPath))
        {
            return true;
        }

        if (IsExpectedCommand(GetUserCommandForProgId(ProtocolName), expectedCommand))
        {
            return true;
        }

        var userChoiceProgId = GetUserChoiceProgId();
        if (
            !string.IsNullOrEmpty(userChoiceProgId)
            && IsOwnHandler(userChoiceProgId, expectedCommand)
        )
        {
            return true;
        }

        using var registeredApplicationsKey = Registry.CurrentUser.OpenSubKey(
            RegisteredApplicationsPath
        );
        return string.Equals(
            registeredApplicationsKey?.GetValue(_options.ApplicationName)?.ToString(),
            CapabilitiesPath,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private bool IsOwnHandler(string progId, string expectedCommand)
    {
        if (string.Equals(progId, _options.ProgId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsExpectedCommand(GetUserCommandForProgId(progId), expectedCommand);
    }

    private bool RemoveCapabilities()
    {
        var removedCapabilities = false;

        using (
            var urlAssociationsKey = Registry.CurrentUser.OpenSubKey(
                $@"{CapabilitiesPath}\{UrlAssociationsSubKey}",
                writable: true
            )
        )
        {
            if (
                urlAssociationsKey is not null
                && urlAssociationsKey.GetValue(ProtocolName) is not null
            )
            {
                urlAssociationsKey.DeleteValue(ProtocolName, false);
                removedCapabilities = true;
            }
        }

        if (!IsUserSubKeyEmpty($@"{CapabilitiesPath}\{UrlAssociationsSubKey}"))
        {
            return removedCapabilities;
        }

        if (DeleteUserSubKeyTree(CapabilitiesPath))
        {
            removedCapabilities = true;
        }

        using (
            var registeredApplicationsKey = Registry.CurrentUser.OpenSubKey(
                RegisteredApplicationsPath,
                writable: true
            )
        )
        {
            if (
                registeredApplicationsKey is not null
                && string.Equals(
                    registeredApplicationsKey.GetValue(_options.ApplicationName)?.ToString(),
                    CapabilitiesPath,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                registeredApplicationsKey.DeleteValue(_options.ApplicationName, false);
                removedCapabilities = true;
            }
        }

        if (IsUserSubKeyEmpty(_options.SoftwarePath))
        {
            DeleteUserSubKeyTree(_options.SoftwarePath);
        }

        return removedCapabilities;
    }

    private static bool TryDeleteUserChoice()
    {
        try
        {
            return DeleteUserSubKeyTree(UserChoicePath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool DeleteUserSubKeyTree(string path)
    {
        if (!HasUserSubKey(path))
        {
            return false;
        }

        Registry.CurrentUser.DeleteSubKeyTree(path, false);
        return true;
    }

    private static bool HasUserSubKey(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        return key is not null;
    }

    private static bool IsUserSubKeyEmpty(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        if (key is null)
        {
            return true;
        }

        return key.SubKeyCount is 0 && key.ValueCount is 0;
    }

    private static string? GetUserChoiceProgId()
    {
        using var userChoiceKey = Registry.CurrentUser.OpenSubKey(UserChoicePath);
        return userChoiceKey?.GetValue("ProgId")?.ToString();
    }

    private static string? GetCommandForProgId(string progId)
    {
        var command = GetUserCommandForProgId(progId);
        if (!string.IsNullOrEmpty(command))
        {
            return command;
        }

        return GetMachineCommandForProgId(progId);
    }

    private static string? GetUserCommandForProgId(string progId)
    {
        using var userKey = Registry.CurrentUser.OpenSubKey(
            $@"{ClassesPath}\{progId}\shell\open\command"
        );
        return userKey?.GetValue(string.Empty)?.ToString();
    }

    private static string? GetMachineCommandForProgId(string progId)
    {
        using var machineKey = Registry.LocalMachine.OpenSubKey(
            $@"{ClassesPath}\{progId}\shell\open\command"
        );
        return machineKey?.GetValue(string.Empty)?.ToString();
    }

    private static bool IsExpectedCommand(string? command, string expectedCommand)
    {
        return string.Equals(command, expectedCommand, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLoggableValue(string? value)
    {
        return string.IsNullOrEmpty(value) ? "(none)" : value;
    }

    private static string GetDisplayName(string progId, string? command)
    {
        if (!string.IsNullOrEmpty(command))
        {
            var executable = command.Trim();
            if (executable.StartsWith('"'))
            {
                var closingQuote = executable.IndexOf('"', 1);
                executable =
                    closingQuote > 1
                        ? executable.Substring(1, closingQuote - 1)
                        : executable.Substring(1);
            }
            else
            {
                var firstSpace = executable.IndexOf(' ');
                executable = firstSpace > 0 ? executable.Substring(0, firstSpace) : executable;
            }

            try
            {
                var name = Path.GetFileNameWithoutExtension(executable);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch (ArgumentException) { }
        }

        return progId;
    }
}
