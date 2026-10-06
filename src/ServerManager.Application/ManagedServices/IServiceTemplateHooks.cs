namespace ServerManager.Application.ManagedServices;

/// <summary>
/// İleri düzey eklentiler için isteğe bağlı kancalar. <c>services.AddSingleton&lt;IServiceTemplateHooks, MyHooks&gt;()</c> ile
/// kaydedilir ve yalnızca aynı eklentinin şablonuna (<see cref="TemplateKey"/>) bağlanabilir. Eklenti devre dışıyken çağrılmaz.
/// </summary>
/// <remarks>
/// Güvenlik: kancalar sunucuda komut çalıştıramaz. <see cref="BuildExtraArgs"/> yalnızca imajdan sonra verilen argümanları
/// döner; çekirdek her argümanı ayrı ayrı kabuk kaçışıyla ekler, satır sonu/NUL içeren veya gizli değer barındıran argümanı
/// reddeder. Kanca hataları işlemi güvenli biçimde durdurur (<see cref="AfterInstallAsync"/> hariç: yalnızca uyarı yazılır).
/// </remarks>
public interface IServiceTemplateHooks
{
    /// <summary>Kancanın bağlı olduğu şablon anahtarı (ör. <c>services.extra.keycloak</c>).</summary>
    string TemplateKey { get; }

    /// <summary>Kurulum ve ayar formunun ek doğrulaması; çekirdek doğrulamasından sonra çalışır.</summary>
    IEnumerable<ServiceTemplateHookError> Validate(ServiceTemplateFormContext context) => [];

    /// <summary>
    /// Şablonun <see cref="ServiceTemplate.Command"/> argümanlarına eklenecek argümanlar (ör. çalışma kipi). Kurulum, yeniden
    /// oluşturma ve sürüm yükseltmede çağrılır.
    /// </summary>
    IReadOnlyList<string> BuildExtraArgs(ServiceTemplateCommandContext context) => [];

    /// <summary>Kurulum başarıyla bittikten sonra çağrılır; çıktı işlem loguna (gizli değerler maskelenerek) yazılır.</summary>
    Task AfterInstallAsync(ServiceTemplateInstallContext context, IServiceTemplateHookLog log, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

/// <summary>Kanca doğrulama hatası; <paramref name="Property"/> formdaki alan adıdır (ör. <c>Environment</c>, <c>ImageTag</c>).</summary>
public sealed record ServiceTemplateHookError(string Property, string Message);

/// <summary>Kurulum/ayar formunun parola içermeyen görünümü.</summary>
public sealed class ServiceTemplateFormContext
{
    public required string TemplateKey { get; init; }

    /// <summary>Yeni kurulumda true; Ayarlar sekmesinden yeniden oluşturmada false.</summary>
    public bool IsInstall { get; init; }

    public string? Name { get; init; }

    public string? ImageTag { get; init; }

    public string? Username { get; init; }

    public string? Database { get; init; }

    /// <summary>Kullanıcının girdiği ek ortam değişkenleri.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}

/// <summary>Container komutu üretilirken kancaya verilen bilgiler.</summary>
public sealed class ServiceTemplateCommandContext
{
    public required string TemplateKey { get; init; }

    /// <summary>Kurulacak imaj etiketi.</summary>
    public required string ImageTag { get; init; }

    /// <summary>Kullanıcının girdiği ek ortam değişkenleri.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}

/// <summary>Kurulum sonrası kancaya verilen bilgiler. Kimlik bilgileri yalnızca bellekte tutulmalı, loglanmamalıdır.</summary>
public sealed class ServiceTemplateInstallContext
{
    public required Guid ServiceId { get; init; }

    public required Guid ServerId { get; init; }

    public required string Name { get; init; }

    public required string TemplateKey { get; init; }

    public required string ImageTag { get; init; }

    public required string ContainerName { get; init; }

    /// <summary>Docker iç ağındaki uç (container adı + ana port); şablonda port yoksa null.</summary>
    public ServiceEndpoint? InternalEndpoint { get; init; }

    /// <summary>Sunucuya yayınlanan portlar.</summary>
    public IReadOnlyList<PublishedPort> PublishedPorts { get; init; } = [];

    public required ServiceCredentials Credentials { get; init; }
}

/// <summary>İşlem loguna satır yazar; gizli değerler otomatik maskelenir.</summary>
public interface IServiceTemplateHookLog
{
    Task InfoAsync(string message, CancellationToken cancellationToken);

    Task WarningAsync(string message, CancellationToken cancellationToken);
}
