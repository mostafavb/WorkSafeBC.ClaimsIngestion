using System.Globalization;
using System.Xml.Linq;
using WorkSafeBC.Claims.Application.Abstractions;
using WorkSafeBC.Claims.Application.Claims;
using WorkSafeBC.Claims.Domain.Entities;

namespace WorkSafeBC.Claims.Infrastructure.Storage;

public sealed class ClaimFileParser : IClaimFileParser
{
    public IReadOnlyCollection<InjuryClaim> Parse(InboundClaimFile file)
    {
        return file.Kind switch
        {
            ClaimFileKind.Xml => ParseXml(file),
            ClaimFileKind.FlatFile => ParseFlatFile(file),
            _ => throw new InvalidOperationException($"Unsupported file kind '{file.Kind}'.")
        };
    }

    private static IReadOnlyCollection<InjuryClaim> ParseXml(InboundClaimFile file)
    {
        var document = XDocument.Parse(file.Content);

        return document.Root?
            .Elements("Claim")
            .Select(element => new InjuryClaim(
                claimNumber: element.Element("ClaimNumber")?.Value ?? string.Empty,
                workerId: element.Element("WorkerId")?.Value ?? string.Empty,
                injuryDate: DateOnly.Parse(element.Element("InjuryDate")?.Value ?? string.Empty, CultureInfo.InvariantCulture),
                claimAmount: decimal.Parse(element.Element("ClaimAmount")?.Value ?? "0", CultureInfo.InvariantCulture),
                employerNumber: element.Element("EmployerNumber")?.Value ?? string.Empty,
                currency: element.Element("Currency")?.Value ?? "CAD",
                sourceFileName: file.FileName))
            .ToArray()
            ?? Array.Empty<InjuryClaim>();
    }

    private static IReadOnlyCollection<InjuryClaim> ParseFlatFile(InboundClaimFile file)
    {
        return file.Content
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|'))
            .Select(parts => new InjuryClaim(
                claimNumber: parts.ElementAtOrDefault(0) ?? string.Empty,
                workerId: parts.ElementAtOrDefault(1) ?? string.Empty,
                injuryDate: DateOnly.Parse(parts.ElementAtOrDefault(2) ?? string.Empty, CultureInfo.InvariantCulture),
                claimAmount: decimal.Parse(parts.ElementAtOrDefault(3) ?? "0", CultureInfo.InvariantCulture),
                employerNumber: parts.ElementAtOrDefault(4) ?? string.Empty,
                currency: parts.ElementAtOrDefault(5) ?? "CAD",
                sourceFileName: file.FileName))
            .ToArray();
    }
}
