using Azure;
using Azure.Security.KeyVault.Secrets;

namespace WorkSafeBC.Claims.Infrastructure.Secrets;

public sealed class KeyVaultSecretProvider(SecretClient? secretClient) : ISecretProvider
{
    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
    {
        if (secretClient is null)
        {
            return null;
        }

        Response<KeyVaultSecret> response = await secretClient.GetSecretAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Value.Value;
    }
}
