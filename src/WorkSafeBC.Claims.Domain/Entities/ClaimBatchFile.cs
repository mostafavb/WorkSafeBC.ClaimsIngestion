namespace WorkSafeBC.Claims.Domain.Entities;

public sealed class ClaimBatchFile
{
    public ClaimBatchFile(string fileName, DateTimeOffset receivedAtUtc, int recordCount)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        FileName = fileName.Trim();
        ReceivedAtUtc = receivedAtUtc;
        RecordCount = recordCount;
    }

    public string FileName { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    public int RecordCount { get; }
}
