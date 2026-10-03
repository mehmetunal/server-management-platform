namespace ServerManager.Application.DTOs.Deployments;

/// <summary>Proje formundaki bağlantı seçeneği; <see cref="Key"/> <c>GitSourceKeys</c> biçimindedir.</summary>
public sealed record GitSourceOptionDto(string Key, string Name, string IntegrationName);
