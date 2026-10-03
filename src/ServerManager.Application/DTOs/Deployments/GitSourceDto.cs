namespace ServerManager.Application.DTOs.Deployments;

/// <summary>Entegrasyondaki bir bağlantı (ör. bir hesaba/kuruma yapılmış GitHub App kurulumu).</summary>
public sealed record GitSourceDto(string Id, string Name);
