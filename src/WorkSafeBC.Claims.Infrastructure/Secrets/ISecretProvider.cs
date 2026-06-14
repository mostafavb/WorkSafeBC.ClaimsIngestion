namespace WorkSafeBC.Claims.Infrastructure.Secrets;

public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);
}
