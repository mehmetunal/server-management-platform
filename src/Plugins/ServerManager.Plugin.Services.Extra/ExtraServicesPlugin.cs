namespace ServerManager.Plugin.Services.Extra;

public static class ExtraServicesPlugin
{
    /// <summary>plugin.json içindeki SystemName ile aynı olmalıdır; şablon anahtarlarının öneki bundan türetilir.</summary>
    public const string SystemName = "Services.Extra";

    /// <summary>Şablon anahtarı öneki: SystemName küçük harfle + nokta.</summary>
    public const string KeyPrefix = "services.extra.";

    /// <summary>JSON ile tanımlanan şablonlar (templates/*.json).</summary>
    public const string MeilisearchKey = KeyPrefix + "meilisearch";

    public const string ClickHouseKey = KeyPrefix + "clickhouse";

    /// <summary>C# sağlayıcısıyla tanımlanan şablon.</summary>
    public const string KeycloakKey = KeyPrefix + "keycloak";

    /// <summary>C# ile tanımlanan şablon grubu.</summary>
    public const string IdentityCategory = KeyPrefix + "identity";

    /// <summary>JSON ile tanımlanan şablon grubu (templates/meilisearch.json).</summary>
    public const string SearchCategory = KeyPrefix + "search";
}
