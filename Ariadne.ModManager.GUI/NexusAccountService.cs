using Ariadne.Security;
using Ariadne.WebProtocol.Nexus;

namespace Ariadne.ModManager.GUI;

public enum NexusCredentialFailure
{
    None,
    StoredKeyRejected,
    DevKeyRejected,
}

public sealed record NexusAccountSnapshot(
    NexusAccount? Account,
    bool UsingDevKey,
    NexusCredentialFailure Failure
)
{
    public bool SignedIn => Account is not null;

    public static readonly NexusAccountSnapshot SignedOut = new(
        null,
        false,
        NexusCredentialFailure.None
    );
}

public interface INexusAccountService
{
    Task<NexusAccountSnapshot> RefreshAsync(CancellationToken cancellationToken = default);

    Task StoreApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    Task<NexusAccountSnapshot> SignOutAsync(CancellationToken cancellationToken = default);
}

public sealed class NexusAccountService(
    ISecretStore secrets,
    INexusAccountClient accountClient,
    INexusAccountCache accountCache
) : INexusAccountService
{
    public const string DevKeyEnvironmentVariable = "ARIADNE_NEXUS_API_KEY";
    private const string ApiKeySecretName = "nexus-api-key";

    public async Task<NexusAccountSnapshot> RefreshAsync(
        CancellationToken cancellationToken = default
    )
    {
        var apiKey = GetEnvironmentApiKey();
        var usingDevKey = apiKey is not null;
        apiKey ??= await secrets.GetAsync(ApiKeySecretName, cancellationToken);
        if (apiKey is null)
        {
            accountCache.Clear();
            return NexusAccountSnapshot.SignedOut;
        }

        var account = accountCache.Read(apiKey);
        if (account is null)
        {
            account = await accountClient.ValidateAsync(apiKey, cancellationToken);
            if (account is null)
            {
                if (!usingDevKey)
                {
                    await secrets.DeleteAsync(ApiKeySecretName, cancellationToken);
                }
                accountCache.Clear();
                return new NexusAccountSnapshot(
                    null,
                    usingDevKey,
                    usingDevKey
                        ? NexusCredentialFailure.DevKeyRejected
                        : NexusCredentialFailure.StoredKeyRejected
                );
            }
            accountCache.Write(apiKey, account);
        }

        return new NexusAccountSnapshot(account, usingDevKey, NexusCredentialFailure.None);
    }

    public Task StoreApiKeyAsync(string apiKey, CancellationToken cancellationToken = default) =>
        secrets.SetAsync(ApiKeySecretName, apiKey, cancellationToken);

    public async Task<NexusAccountSnapshot> SignOutAsync(
        CancellationToken cancellationToken = default
    )
    {
        await secrets.DeleteAsync(ApiKeySecretName, cancellationToken);
        accountCache.Clear();
        return GetEnvironmentApiKey() is not null
            ? await RefreshAsync(cancellationToken)
            : NexusAccountSnapshot.SignedOut;
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
}
