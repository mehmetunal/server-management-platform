using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.ManagedServices;

public class ManagedServiceListItemDto
{
    public Guid Id { get; init; }

    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public string ServerAddress { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string TemplateKey { get; init; } = string.Empty;

    /// <summary>Şablon katalogdan kaldırıldıysa null.</summary>
    public ServiceTemplate? Template { get; init; }

    public string TemplateName => Template?.DisplayName ?? TemplateKey;

    public string ImageTag { get; init; } = string.Empty;

    public string ContainerName { get; init; } = string.Empty;

    public ManagedServiceStatus Status { get; init; }

    public string? LastError { get; init; }

    public bool ExposePublicly { get; init; }

    public bool HasAllowList { get; init; }

    public IReadOnlyList<PublishedPort> Ports { get; init; } = [];

    /// <summary>Web arayüzü dışarıya açıksa tarayıcıda açılacak adres.</summary>
    public string? WebUiUrl { get; init; }

    public DateTime CreatedAt { get; init; }

    public bool IsBusy => Status is ManagedServiceStatus.Installing or ManagedServiceStatus.Updating or ManagedServiceStatus.Removing;
}

public sealed class ManagedServiceDetailsDto : ManagedServiceListItemDto
{
    public string? Username { get; init; }

    public string? Database { get; init; }

    public bool HasPassword { get; init; }

    public ManagedServiceVolumeMode VolumeMode { get; init; }

    public string? HostDataPath { get; init; }

    public string VolumeName { get; init; } = string.Empty;

    public string? DataPath { get; init; }

    public int? MemoryLimitMb { get; init; }

    public decimal? CpuLimit { get; init; }

    public bool JoinProxyNetwork { get; init; }

    /// <summary>Container'ın katıldığı ağlar (panel ağı dahil).</summary>
    public IReadOnlyList<string> Networks { get; init; } = [];

    public IReadOnlyList<string> AllowedSources { get; init; } = [];

    public IReadOnlyList<string> EnvironmentKeys { get; init; } = [];

    /// <summary>Docker ağı içindeki adres: container adı ve iç port.</summary>
    public ServiceEndpoint? InternalEndpoint { get; init; }

    /// <summary>Sunucu adresi ve yayınlanan ana port; port yayınlanmamışsa null.</summary>
    public ServiceEndpoint? ExternalEndpoint { get; init; }

    /// <summary>Parolası <c>****</c> ile gizlenmiş bağlantı adresleri.</summary>
    public string? InternalConnectionPreview { get; init; }

    public string? ExternalConnectionPreview { get; init; }

    public Guid? RunningOperationId { get; init; }

    public string? CreatedBy { get; init; }
}

public sealed class ManagedServiceOperationDto
{
    public Guid Id { get; init; }

    public Guid ServiceId { get; init; }

    public Guid ServerId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public ManagedServiceOperationKind Kind { get; init; }

    public ManagedServiceOperationStatus Status { get; init; }

    public string? Stage { get; init; }

    public string? FromTag { get; init; }

    public string? ToTag { get; init; }

    public bool RemoveData { get; init; }

    public string? FailureReason { get; init; }

    public string Log { get; init; } = string.Empty;

    public string? UserName { get; init; }

    public string? IpAddress { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? FinishedAt { get; init; }

    public bool IsRunning => Status == ManagedServiceOperationStatus.Running;
}

/// <summary>"Parolayı göster" yanıtı: yetkili kullanıcıya gösterilir ve audit log'a yazılır.</summary>
public sealed class ManagedServiceSecretsDto
{
    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? Database { get; init; }

    public string? EncryptionKey { get; init; }

    public string? InternalConnectionString { get; init; }

    public string? ExternalConnectionString { get; init; }

    public IReadOnlyDictionary<string, string> SuggestedEnvironment { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Başka modüllerin (ör. deployment projesine servis bağlama) kullanacağı bağlantı bilgisi. Adres Docker iç ağına göredir:
/// uygulama container'ı <see cref="Networks"/> ağlarından birine katılmışsa <see cref="Host"/>:<see cref="Port"/> ile bağlanır.
/// Parola içerir; yalnızca sunucu tarafında kullanılmalı, loglara ve tarayıcıya yazılmamalıdır.
/// </summary>
public sealed class ManagedServiceConnectionInfo
{
    public Guid ServiceId { get; init; }

    public Guid ServerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string TemplateKey { get; init; } = string.Empty;

    public ManagedServiceCategory Category { get; init; }

    /// <summary>Container adı (<c>sm-svc-&lt;slug&gt;</c>); Docker ağındaki DNS adıdır.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Container içi ana port (ör. 5432).</summary>
    public int Port { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? Database { get; init; }

    /// <summary>İç ağ biçiminde bağlantı adresi (ör. <c>postgres://app:…@sm-svc-db:5432/app</c>); şablonda yoksa null.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Bağlanan uygulamaya verilebilecek ortam değişkenleri (DATABASE_URL, REDIS_URL, ConnectionStrings__Default …).</summary>
    public IReadOnlyDictionary<string, string> SuggestedEnvironment { get; init; } = new Dictionary<string, string>();

    /// <summary><see cref="SuggestedEnvironment"/> ile aynı anahtarlar; parola <c>****</c> ile gizlenmiş (önizleme için tarayıcıya gönderilebilir).</summary>
    public IReadOnlyDictionary<string, string> SuggestedEnvironmentPreview { get; init; } = new Dictionary<string, string>();

    /// <summary>Servis container'ının katıldığı Docker ağları; ilki her zaman panel ağı <c>sm-services</c>'tir.</summary>
    public IReadOnlyList<string> Networks { get; init; } = [];

    public override string ToString() => $"ManagedServiceConnectionInfo {{ ServiceId = {ServiceId}, Host = {Host}, Port = {Port}, Password = *** }}";
}
