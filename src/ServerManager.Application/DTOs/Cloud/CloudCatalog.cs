namespace ServerManager.Application.DTOs.Cloud;

public sealed record CloudCatalog(
    IReadOnlyList<CloudOption> Regions,
    IReadOnlyList<CloudOption> Sizes,
    IReadOnlyList<CloudOption> Images,
    string Currency);
