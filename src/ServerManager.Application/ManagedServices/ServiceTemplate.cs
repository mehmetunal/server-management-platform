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

/// <summary>Şablonun container içinde dinlediği port.</summary>
/// <param name="Name">Kısa ad (log ve formda alan adı): <c>db</c>, <c>api</c>, <c>console</c>. Şablon içinde benzersizdir.</param>
/// <param name="ContainerPort">Container içi TCP portu (1–65535); şablon içinde benzersizdir.</param>
/// <param name="Role">Portun rolü; ilk <see cref="ServicePortRole.Primary"/> port bağlantı adresinde kullanılır.</param>
/// <param name="Label">Formda ve detay sayfasında gösterilen ad (ör. "Web arayüzü").</param>
/// <param name="PublishByDefault">Formda varsayılan olarak sunucu portuna yayınlanır mı.</param>
public sealed record ServicePortDefinition(string Name, int ContainerPort, ServicePortRole Role, string Label, bool PublishByDefault = true);

/// <summary>Ortam değişkeni; <paramref name="Secret"/> değerler loglarda maskelenir.</summary>
/// <param name="Key">Değişken adı (<c>[A-Za-z_][A-Za-z0-9_]*</c>).</param>
/// <param name="Value">Değer; satır sonu içeremez. Container'a sunucudaki 0600 izinli ortam dosyası (<c>--env-file</c>) ile verilir.</param>
/// <param name="Secret">Parola, anahtar gibi gizli değer mi.</param>
public sealed record ServiceEnvironmentValue(string Key, string Value, bool Secret = false);

/// <summary>Bağlantı bilgisinin üretildiği uç: container adı + iç port veya sunucu adresi + yayınlanan port.</summary>
public sealed record ServiceEndpoint(string Host, int Port);

/// <summary>Kurulum formunda istenen kimlik bilgileri ve kuralları.</summary>
public sealed class ServiceCredentialSpec
{
    /// <summary>Kullanıcı adı alanının türü; <see cref="ServiceUsernameKind.None"/> ise alan gösterilmez.</summary>
    public ServiceUsernameKind UsernameKind { get; init; }

    /// <summary>Formda önerilen kullanıcı adı; <see cref="ServiceUsernameKind.Fixed"/> için zorunlu sabit değer.</summary>
    public string? DefaultUsername { get; init; }

    /// <summary>Kullanıcı adı alanının etiketi.</summary>
    public string UsernameLabel { get; init; } = "Kullanıcı adı";

    /// <summary>Parola kuralı; <see cref="ServicePasswordPolicy.None"/> ise parola istenmez. Boş bırakılırsa güçlü parola üretilir.</summary>
    public ServicePasswordPolicy PasswordPolicy { get; init; } = ServicePasswordPolicy.Standard;

    /// <summary>Formda veritabanı adı istenir mi.</summary>
    public bool HasDatabase { get; init; }

    /// <summary>Formda önerilen veritabanı adı.</summary>
    public string? DefaultDatabase { get; init; }

    /// <summary>Kullanıcı adı olarak kullanılamayacak değerler (MySQL/MariaDB: root).</summary>
    public IReadOnlyList<string> ReservedUsernames { get; init; } = [];

    /// <summary>Kurulumda üretilen ve formda gösterilmeyen şifreleme anahtarı (n8n: N8N_ENCRYPTION_KEY, Meilisearch: master key).</summary>
    public bool GeneratesEncryptionKey { get; init; }

    public bool HasPassword => PasswordPolicy != ServicePasswordPolicy.None;

    public bool HasUsername => UsernameKind != ServiceUsernameKind.None;
}

/// <summary>
/// Tek tıkla kurulabilen servisin tanımı: imaj ve sürümler, portlar, veri klasörü, kimlik bilgisi → ortam değişkeni eşlemesi,
/// sağlık ve bağlantı testi, bağlantı adresi biçimleri ve konsol komutu. Komut metinleri container içinde <c>sh -c</c> ile
/// çalışır ve gizli değerleri yalnızca container'ın kendi ortam değişkenlerinden okur; sunucudaki komut satırına parola yazılmaz.
/// </summary>
/// <remarks>
/// Eklentiler için kararlı genel API'dir: yeni özellikler yalnızca isteğe bağlı (varsayılan değerli) olarak eklenir.
/// Eklenti şablonları <see cref="IServiceTemplateProvider"/> veya eklenti klasöründeki <c>templates/*.json</c> ile sunulur ve
/// yüklenirken <see cref="ServiceTemplateValidator"/> ile doğrulanır. Sunucuda çalışan komutların tamamı çekirdek tarafından
/// üretilir ve her değer kabuk kaçışıyla verilir; şablon sunucuda ham kabuk komutu çalıştıramaz.
/// </remarks>
public sealed class ServiceTemplate
{
    /// <summary>
    /// Kalıcı anahtar; servis kaydında saklanır ve sonradan değiştirilmemelidir. Yerleşik şablonlarda <c>postgres</c> gibi
    /// kısa ad, eklenti şablonlarında <c>&lt;systemname küçük harf&gt;.&lt;ad&gt;</c> (ör. <c>services.extra.meilisearch</c>).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>Kartta ve başlıkta gösterilen ad (ör. "PostgreSQL").</summary>
    public required string DisplayName { get; init; }

    /// <summary>Temel tür; yedekleme ve bağlama ekranları veritabanı/uygulama ayrımını buradan okur.</summary>
    public required ManagedServiceCategory Category { get; init; }

    /// <summary>
    /// Servis ekleme sayfasındaki grup. Null ise <see cref="Category"/> grubu (<c>database</c>/<c>application</c>) kullanılır;
    /// eklenti kendi tanımladığı bir <see cref="ServiceTemplateCategory"/> anahtarını verebilir.
    /// </summary>
    public string? CategoryKey { get; init; }

    /// <summary>Kartta gösterilen kısa açıklama.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// Logo dosya adı (ör. <c>postgres.svg</c>). Yerleşik şablonlarda <c>wwwroot/images/services</c>, eklenti şablonlarında
    /// eklentinin <c>Content</c> klasöründen sunulur. Boş olabilir.
    /// </summary>
    public required string LogoFile { get; init; }

    /// <summary>Marka rengi (<c>#RRGGBB</c>).</summary>
    public required string Color { get; init; }

    /// <summary>Etiketsiz Docker imajı (ör. <c>postgres</c>, <c>mcr.microsoft.com/mssql/server</c>).</summary>
    public required string Image { get; init; }

    /// <summary>Formda sunulan kararlı sürümler; ilki önerilen sürümdür. Listede olmayan geçerli bir etiket de girilebilir.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Container portları; ilk <see cref="ServicePortRole.Primary"/> (yoksa ilk) port bağlantı adresinde kullanılır.</summary>
    public required IReadOnlyList<ServicePortDefinition> Ports { get; init; }

    /// <summary>Formda istenen kimlik bilgileri; varsayılanı kimlik bilgisi istemez.</summary>
    public ServiceCredentialSpec Credentials { get; init; } = new() { PasswordPolicy = ServicePasswordPolicy.None };

    /// <summary>Container içindeki veri klasörü (mutlak yol); etiket sürüme göre değişebilir (PostgreSQL 18+). Null ise kalıcı veri yoktur.</summary>
    public Func<string, string?> DataPath { get; init; } = _ => null;

    /// <summary>Sunucu klasörü bağlanırken klasöre verilecek sahiplik (<c>uid:gid</c>); null ise imaj kendisi ayarlar.</summary>
    public string? DataOwner { get; init; }

    /// <summary>Kimlik bilgilerinden üretilen ortam değişkenleri; kullanıcı bunları ek değişkenlerle ezemez.</summary>
    public Func<ServiceCredentials, IReadOnlyList<ServiceEnvironmentValue>> Environment { get; init; } = _ => [];

    /// <summary>Kullanıcının ek değişkenlerle değiştirebileceği varsayılan değişkenler (MSSQL_PID, saat dilimi …).</summary>
    public IReadOnlyList<ServiceEnvironmentValue> DefaultEnvironment { get; init; } = [];

    /// <summary>İmajdan sonra verilen argümanlar (ör. MinIO <c>server /data</c>). Her argüman ayrı ayrı kabuk kaçışıyla verilir.</summary>
    public IReadOnlyList<string> Command { get; init; } = [];

    /// <summary>Docker sağlık kontrolü (<c>--health-cmd</c>, container içinde); null ise imajın kendi kontrolü veya "çalışıyor" durumu esas alınır.</summary>
    public string? HealthCommand { get; init; }

    /// <summary>Kurulumdan sonra kimlik bilgileriyle bağlantıyı doğrulayan komut (<c>docker exec … sh -c</c>, container içinde).</summary>
    public string? ReadinessCommand { get; init; }

    /// <summary>Konsol sekmesinde container içinde çalışan istemci komutu; null ise bash/sh açılır.</summary>
    public string? ConsoleCommand { get; init; }

    /// <summary>Konsol sekmesinde gösterilen istemci adı (ör. <c>psql</c>).</summary>
    public string? ConsoleLabel { get; init; }

    /// <summary>Uygulamaların bağlanacağı adres (iç ağ veya dış adres için aynı biçim); null ise bağlantı adresi yoktur.</summary>
    public Func<ServiceEndpoint, ServiceCredentials, string?> ConnectionString { get; init; } = (_, _) => null;

    /// <summary>Bu servise bağlanacak uygulamalar için önerilen ortam değişkenleri ("Projeye bağla").</summary>
    public Func<ServiceEndpoint, ServiceCredentials, IReadOnlyDictionary<string, string>> SuggestedEnvironment { get; init; } =
        (_, _) => new Dictionary<string, string>();

    /// <summary>Yalnızca x86_64 sunucularda çalışır (SQL Server).</summary>
    public bool RequiresX86 { get; init; }

    /// <summary>Bellek sınırı için alt sınır (MB); 0 ise genel alt sınır geçerlidir.</summary>
    public int MinMemoryMb { get; init; }

    /// <summary>Bellek alanının altında gösterilen öneri.</summary>
    public string? MemoryHint { get; init; }

    /// <summary>Ana sürüm değişikliği veri dosyası biçimini değiştirebilir; yükseltmede uyarı gösterilir.</summary>
    public bool WarnOnMajorUpgrade { get; init; }

    /// <summary>Kurulumda sağlıklı duruma geçmek için beklenecek en uzun süre (saniye, 30–1800).</summary>
    public int HealthTimeoutSeconds { get; init; } = 180;

    /// <summary>Formun yanında gösterilen not (lisans, yükseltme uyarısı …).</summary>
    public string? Notes { get; init; }

    /// <summary>Şablonu sunan eklentinin SystemName değeri; yerleşik şablonlarda null. Katalog tarafından atanır.</summary>
    public string? PluginSystemName { get; internal set; }

    public bool IsBuiltIn => PluginSystemName is null;

    /// <summary>Anahtarın son parçası (<c>services.extra.meilisearch</c> → <c>meilisearch</c>); yeni servis adı önerisi.</summary>
    public string LocalKey => Key[(Key.LastIndexOf('.') + 1)..];

    /// <summary>Web köküne göre logo yolu (baştaki <c>/</c> olmadan); logo yoksa null.</summary>
    public string? LogoPath => string.IsNullOrEmpty(LogoFile)
        ? null
        : PluginSystemName is null
            ? $"images/services/{LogoFile}"
            : $"plugins/{PluginSystemName.ToLowerInvariant()}/{LogoFile}";

    /// <summary>Servis ekleme sayfasındaki grup anahtarı.</summary>
    public string GroupKey => CategoryKey ?? ServiceTemplateCategories.KeyOf(Category);

    public ServicePortDefinition? PrimaryPort => Ports.FirstOrDefault(p => p.Role == ServicePortRole.Primary) ?? Ports.FirstOrDefault();

    public ServicePortDefinition? WebUiPort => Ports.FirstOrDefault(p => p.Role == ServicePortRole.WebUi);

    public string DefaultTag => Tags[0];

    public string ImageReference(string tag) => $"{Image}:{tag}";
}
