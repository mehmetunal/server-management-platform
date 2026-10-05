using ServerManager.Domain.Enums;

namespace ServerManager.Application.ManagedServices;

public enum ServicePortRole
{
    /// <summary>İstemcilerin bağlandığı ana port (veritabanı portu, S3 API, AMQP).</summary>
    Primary = 1,

    /// <summary>Tarayıcıdan açılan web arayüzü; dışarıya açıksa "Aç" bağlantısı gösterilir.</summary>
    WebUi = 2
}

public enum ServiceUsernameKind
{
    /// <summary>Şablon kullanıcı adı istemez (Redis, Uptime Kuma).</summary>
    None = 0,

    /// <summary>Serbest kullanıcı adı.</summary>
    Name = 1,

    /// <summary>Sabit kullanıcı adı (MSSQL: sa); formda değiştirilemez.</summary>
    Fixed = 2,

    /// <summary>E-posta adresi (pgAdmin).</summary>
    Email = 3
}

public enum ServicePasswordPolicy
{
    /// <summary>Parola istenmez.</summary>
    None = 0,

    /// <summary>En az 12 karakter.</summary>
    Standard = 1,

    /// <summary>SQL Server sa kuralları: en az 8 karakter ve büyük harf, küçük harf, rakam, sembolden en az üçü.</summary>
    MssqlComplex = 2
}

/// <param name="Name">Kısa ad (log ve formda alan adı): <c>db</c>, <c>api</c>, <c>console</c>.</param>
/// <param name="PublishByDefault">Formda varsayılan olarak sunucu portuna yayınlanır mı.</param>
public sealed record ServicePortDefinition(string Name, int ContainerPort, ServicePortRole Role, string Label, bool PublishByDefault = true);

/// <summary>Ortam değişkeni; <paramref name="Secret"/> değerler loglarda maskelenir.</summary>
public sealed record ServiceEnvironmentValue(string Key, string Value, bool Secret = false);

/// <summary>Bağlantı bilgisinin üretildiği uç: container adı + iç port veya sunucu adresi + yayınlanan port.</summary>
public sealed record ServiceEndpoint(string Host, int Port);

public sealed class ServiceCredentialSpec
{
    public ServiceUsernameKind UsernameKind { get; init; }

    public string? DefaultUsername { get; init; }

    public string UsernameLabel { get; init; } = "Kullanıcı adı";

    public ServicePasswordPolicy PasswordPolicy { get; init; } = ServicePasswordPolicy.Standard;

    public bool HasDatabase { get; init; }

    public string? DefaultDatabase { get; init; }

    /// <summary>Kullanıcı adı olarak kullanılamayacak değerler (MySQL/MariaDB: root).</summary>
    public IReadOnlyList<string> ReservedUsernames { get; init; } = [];

    /// <summary>Kurulumda üretilen ve formda gösterilmeyen şifreleme anahtarı (n8n: N8N_ENCRYPTION_KEY).</summary>
    public bool GeneratesEncryptionKey { get; init; }

    public bool HasPassword => PasswordPolicy != ServicePasswordPolicy.None;

    public bool HasUsername => UsernameKind != ServiceUsernameKind.None;
}

/// <summary>
/// Tek tıkla kurulabilen servisin tanımı: imaj ve sürümler, portlar, veri klasörü, kimlik bilgisi → ortam değişkeni eşlemesi,
/// sağlık ve bağlantı testi, bağlantı adresi biçimleri ve konsol komutu. Komut metinleri container içinde <c>sh -c</c> ile
/// çalışır ve gizli değerleri yalnızca container'ın kendi ortam değişkenlerinden okur; sunucudaki komut satırına parola yazılmaz.
/// </summary>
public sealed class ServiceTemplate
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public required ManagedServiceCategory Category { get; init; }

    public required string Description { get; init; }

    /// <summary>Logo yerine gösterilen kısa harf işareti (ör. PG) ve marka rengi.</summary>
    public required string LogoText { get; init; }

    public required string Color { get; init; }

    public required string Image { get; init; }

    /// <summary>Formda sunulan kararlı sürümler; ilki önerilen sürümdür. Listede olmayan geçerli bir etiket de girilebilir.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<ServicePortDefinition> Ports { get; init; }

    public ServiceCredentialSpec Credentials { get; init; } = new() { PasswordPolicy = ServicePasswordPolicy.None };

    /// <summary>Container içindeki veri klasörü; etiket sürüme göre değişebilir (PostgreSQL 18+). Null ise kalıcı veri yoktur.</summary>
    public Func<string, string?> DataPath { get; init; } = _ => null;

    /// <summary>Sunucu klasörü bağlanırken klasöre verilecek sahiplik (uid:gid); null ise imaj kendisi ayarlar.</summary>
    public string? DataOwner { get; init; }

    /// <summary>Kimlik bilgilerinden üretilen ortam değişkenleri; kullanıcı bunları ek değişkenlerle ezemez.</summary>
    public Func<ServiceCredentials, IReadOnlyList<ServiceEnvironmentValue>> Environment { get; init; } = _ => [];

    /// <summary>Kullanıcının ek değişkenlerle değiştirebileceği varsayılan değişkenler (MSSQL_PID, saat dilimi …).</summary>
    public IReadOnlyList<ServiceEnvironmentValue> DefaultEnvironment { get; init; } = [];

    /// <summary>İmajdan sonra verilen argümanlar (ör. redis-server parolası, MinIO konsol portu).</summary>
    public IReadOnlyList<string> Command { get; init; } = [];

    /// <summary>Docker sağlık kontrolü (<c>--health-cmd</c>); null ise imajın kendi kontrolü veya "çalışıyor" durumu esas alınır.</summary>
    public string? HealthCommand { get; init; }

    /// <summary>Kurulumdan sonra kimlik bilgileriyle bağlantıyı doğrulayan komut (<c>docker exec … sh -c</c>).</summary>
    public string? ReadinessCommand { get; init; }

    /// <summary>Konsol sekmesinde container içinde çalışan istemci komutu; null ise bash/sh açılır.</summary>
    public string? ConsoleCommand { get; init; }

    public string? ConsoleLabel { get; init; }

    /// <summary>Uygulamaların bağlanacağı adres (iç ağ veya dış adres için aynı biçim); null ise bağlantı adresi yoktur.</summary>
    public Func<ServiceEndpoint, ServiceCredentials, string?> ConnectionString { get; init; } = (_, _) => null;

    /// <summary>Bu servise bağlanacak uygulamalar için önerilen ortam değişkenleri.</summary>
    public Func<ServiceEndpoint, ServiceCredentials, IReadOnlyDictionary<string, string>> SuggestedEnvironment { get; init; } =
        (_, _) => new Dictionary<string, string>();

    /// <summary>Yalnızca x86_64 sunucularda çalışır (SQL Server).</summary>
    public bool RequiresX86 { get; init; }

    public int MinMemoryMb { get; init; }

    public string? MemoryHint { get; init; }

    /// <summary>Ana sürüm değişikliği veri dosyası biçimini değiştirebilir; yükseltmede uyarı gösterilir.</summary>
    public bool WarnOnMajorUpgrade { get; init; }

    public int HealthTimeoutSeconds { get; init; } = 180;

    public string? Notes { get; init; }

    public ServicePortDefinition? PrimaryPort => Ports.FirstOrDefault(p => p.Role == ServicePortRole.Primary) ?? Ports.FirstOrDefault();

    public ServicePortDefinition? WebUiPort => Ports.FirstOrDefault(p => p.Role == ServicePortRole.WebUi);

    public string DefaultTag => Tags[0];

    public string ImageReference(string tag) => $"{Image}:{tag}";
}
