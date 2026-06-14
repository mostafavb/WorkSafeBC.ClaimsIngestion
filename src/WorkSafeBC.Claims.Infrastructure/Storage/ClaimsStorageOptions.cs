namespace WorkSafeBC.Claims.Infrastructure.Storage;

public sealed class ClaimsStorageOptions
{
    public const string SectionName = "ClaimsStorage";

    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";

    public string ContainerName { get; set; } = "claims-inbox";
}
