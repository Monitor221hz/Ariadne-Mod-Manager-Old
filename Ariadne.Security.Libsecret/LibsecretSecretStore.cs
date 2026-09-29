using System.ComponentModel;
using System.Diagnostics;

namespace Ariadne.Security.Libsecret;

public sealed class LibsecretSecretStore : ISecretStore
{
    private const string SecretTool = "secret-tool";
    private const int NotFoundExitCode = 1;

    private readonly string _applicationName;

    public LibsecretSecretStore(string applicationName)
    {
        _applicationName = applicationName;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(
            cancellationToken,
            null,
            "lookup",
            "application",
            _applicationName,
            "key",
            key
        );
        if (result.ExitCode == NotFoundExitCode)
        {
            return null;
        }
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"secret-tool lookup failed ({result.ExitCode}): {result.StandardError}"
            );
        }

        var value = result.StandardOutput.TrimEnd('\r', '\n');
        return value.Length == 0 ? null : value;
    }

    public async Task SetAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        var result = await RunAsync(
            cancellationToken,
            value,
            "store",
            "--label",
            $"{_applicationName}: {key}",
            "application",
            _applicationName,
            "key",
            key
        );
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"secret-tool store failed ({result.ExitCode}): {result.StandardError}"
            );
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(
            cancellationToken,
            null,
            "clear",
            "application",
            _applicationName,
            "key",
            key
        );
        if (result.ExitCode != 0 && result.ExitCode != NotFoundExitCode)
        {
            throw new InvalidOperationException(
                $"secret-tool clear failed ({result.ExitCode}): {result.StandardError}"
            );
        }
    }

    private static async Task<ProcessResult> RunAsync(
        CancellationToken cancellationToken,
        string? standardInput,
        params string[] arguments
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = SecretTool,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Start(startInfo);
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput);
        }
        process.StandardInput.Close();

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            throw;
        }

        return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }

    private static Process Start(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start secret-tool.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "secret-tool is not available. Install your distribution's libsecret package "
                    + "so secrets can be stored in the system keyring (GNOME Keyring or KWallet).",
                exception
            );
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
