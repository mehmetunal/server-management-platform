namespace ServerManager.Application.DTOs.Cloud;

/// <param name="Regions">Boyut yalnızca bu bölgelerde varsa dolu; boşsa her bölgede kullanılabilir.</param>
public sealed record CloudOption(string Id, string Name, string? Description = null, decimal? MonthlyPrice = null, IReadOnlyList<string>? Regions = null);
