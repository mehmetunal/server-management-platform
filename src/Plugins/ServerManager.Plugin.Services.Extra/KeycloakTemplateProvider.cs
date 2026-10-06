using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Plugin.Services.Extra;

/// <summary>
/// C# ile tanımlanan şablon örneği. Çalışma kipi (<c>start-dev</c> / <c>start</c>) <see cref="KeycloakTemplateHooks"/> ile seçilir;
/// gizli değerler yalnızca ortam değişkeniyle (env-file) verilir.
/// </summary>
public sealed class KeycloakTemplateProvider : IServiceTemplateProvider
{
    /// <summary>Yönetim (sağlık) portu; Keycloak 25+ sağlık uçlarını burada sunar.</summary>
    public const int ManagementPort = 9000;

    public const int HttpPort = 8080;

    /// <summary>Kullanıcı bu değişkeni ek değişkenlerde verirse üretim kipi (<c>start</c>) kullanılır.</summary>
    public const string HostnameVariable = "KC_HOSTNAME";

    private static readonly ServiceTemplate Keycloak = new()
    {
        Key = ExtraServicesPlugin.KeycloakKey,
        DisplayName = "Keycloak",
        Category = ManagedServiceCategory.Application,
        CategoryKey = ExtraServicesPlugin.IdentityCategory,
        Description = "Açık kaynak kimlik ve erişim yönetimi: tek oturum açma (SSO), OpenID Connect ve SAML.",
        LogoFile = "keycloak.svg",
        Color = "#4D9FD6",
        Image = "quay.io/keycloak/keycloak",
        Tags = ["26.3", "26.2", "26.1"],
        Ports = [new ServicePortDefinition("http", HttpPort, ServicePortRole.WebUi, "Yönetim konsolu ve HTTP")],
        Credentials = new ServiceCredentialSpec
        {
            UsernameKind = ServiceUsernameKind.Name,
            DefaultUsername = "admin",
            UsernameLabel = "Yönetici kullanıcı"
        },
        DataPath = _ => "/opt/keycloak/data",
        DataOwner = "1000:0",
        Environment = c =>
        [
            new("KC_BOOTSTRAP_ADMIN_USERNAME", c.Username ?? string.Empty),
            new("KC_BOOTSTRAP_ADMIN_PASSWORD", c.Password ?? string.Empty, true),
            new("KC_HEALTH_ENABLED", "true")
        ],
        DefaultEnvironment =
        [
            new("KC_HTTP_ENABLED", "true"),
            new("KC_PROXY_HEADERS", "xforwarded"),
            new("KC_HOSTNAME_STRICT", "false")
        ],
        // İmajda curl/wget yok; bash'in /dev/tcp yönlendirmesiyle hazır olma ucu sorgulanır (container içinde).
        HealthCommand = $"exec 3<>/dev/tcp/127.0.0.1/{ManagementPort} && printf 'GET /health/ready HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && grep -q UP <&3",
        ConnectionString = (e, _) => ServiceConnectionStrings.Http(e),
        SuggestedEnvironment = (e, _) => new Dictionary<string, string>
        {
            ["KEYCLOAK_URL"] = ServiceConnectionStrings.Http(e),
            ["OIDC_ISSUER_BASE"] = ServiceConnectionStrings.Http(e) + "/realms"
        },
        MinMemoryMb = 768,
        MemoryHint = "Keycloak JVM ile çalışır; en az 768 MB, üretimde 2 GB önerilir.",
        WarnOnMajorUpgrade = true,
        HealthTimeoutSeconds = 300,
        Notes = "Varsayılan kip start-dev'dir (gömülü H2 veritabanı, geliştirme/deneme için). Üretimde ek değişkenlere KC_HOSTNAME " +
                "(ör. https://sso.example.com) ve KC_DB* ayarlarını girin; o zaman \"start\" kipi kullanılır. HTTPS'i ters vekil (proxy) sonlandırmalıdır."
    };

    private static readonly ServiceTemplateCategory Identity = new(ExtraServicesPlugin.IdentityCategory, "Kimlik ve erişim", 40);

    public IReadOnlyList<ServiceTemplate> GetTemplates() => [Keycloak];

    public IReadOnlyList<ServiceTemplateCategory> GetCategories() => [Identity];
}
