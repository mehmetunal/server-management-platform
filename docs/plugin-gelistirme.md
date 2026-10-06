# Eklenti geliştirme: servis şablonları

Bu rehber, **Servisler** modülüne (tek tıkla kurulan Docker servisleri) yeni servis türü ekleyen eklentilerin nasıl yazılacağını anlatır. Genel eklenti yapısı (proje, `plugin.json`, kurulum ve etkinleştirme) için README'deki "Eklenti geliştirme" bölümüne bakın.

Çalışan örnek: `src/Plugins/ServerManager.Plugin.Services.Extra`

- **Meilisearch** ve **ClickHouse**: kod gerektirmeyen JSON şablonları (`templates/meilisearch.json`, `templates/clickhouse.json`).
- **Keycloak**: C# sağlayıcısı (`KeycloakTemplateProvider`) ve kancalar (`KeycloakTemplateHooks`).

## Nasıl çalışır

1. Açılışta eklenti assembly'si yüklenir ve `IPluginStartup.ConfigureServices` çağrılır.
2. Servis şablon kataloğu (`IServiceTemplateCatalog`) ilk kullanımda şunları birleştirir:
   - yerleşik şablonlar (`ServiceTemplates.BuiltIn`),
   - eklentilerin DI'a kaydettiği `IServiceTemplateProvider` sağlayıcıları,
   - her eklenti klasöründeki `templates/*.json` dosyaları.
3. Her eklenti şablonu doğrulanır. Hatalı, çakışan veya ad alanı dışındaki şablon **atlanır**; nedeni uygulama loguna yazılır ve **Eklentiler** sayfasında eklenti kartında uyarı olarak görünür.
4. Hangi şablonların listelendiği her istekte eklentinin etkinlik durumuna göre belirlenir. Eklentiyi etkinleştirmek veya devre dışı bırakmak yeniden başlatma gerektirmez.

## Proje

```xml
<!-- src/Plugins/ServerManager.Plugin.Acme.Services/ServerManager.Plugin.Acme.Services.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PluginSystemName>Acme.Services</PluginSystemName>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\ServerManager.Web.Framework\ServerManager.Web.Framework.csproj" Private="false" />
  </ItemGroup>
</Project>
```

- Proje adı `ServerManager.Plugin.{SystemName}` biçimindedir; projeyi `ServerManager.slnx` dosyasına ve `Dockerfile` içindeki csproj kopyalama satırlarına ekleyin.
- `plugin.json`, `Content/**` ve `templates/**` derleme çıktısına (`src/ServerManager.Web/Plugins/{SystemName}/`) otomatik kopyalanır (`src/Plugins/Directory.Build.targets`).
- Sözleşmeler `ServerManager.Application.ManagedServices` ad alanındadır: `ServiceTemplate`, `IServiceTemplateProvider`, `IServiceTemplateHooks`, `ServiceTemplateCategory`.
- JSON şablonları bir assembly'ye ihtiyaç duymaz, ancak eklenti yükleyicisi her eklentide bir assembly bekler. Yalnızca JSON şablonu sunan bir eklenti için de boş bir C# projesi yeterlidir.

## Şablon anahtarı ve ad alanı

- Anahtar servis kaydında **kalıcı olarak** saklanır (`ManagedServices.TemplateKey`, en fazla 96 karakter). Yayınlandıktan sonra değiştirmeyin; değişirse kurulu servisler şablonsuz kalır.
- Eklenti şablonlarında anahtar `<SystemName küçük harf>.<ad>` biçiminde olmalıdır. Örnek: `Acme.Services` eklentisi için `acme.services.redis-stack`.
- `<ad>` küçük harf veya rakamla başlar; küçük harf, rakam ve tire içerebilir (en fazla 40 karakter).
- Önekiyle başlamayan veya aynı anahtarı ikinci kez tanımlayan şablon atlanır. İlk yüklenen kazanır: önce C# sağlayıcıları, sonra JSON dosyaları (dosya adı sırasıyla).
- Grup (kategori) anahtarları da aynı önekle başlamalıdır. Yerleşik gruplar `database` (Veritabanları) ve `application` (Uygulamalar) her şablonda kullanılabilir.

## C# ile şablon

```csharp
public sealed class AcmeStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IServiceTemplateProvider, AcmeTemplates>();
        services.AddSingleton<IServiceTemplateHooks, AcmeHooks>(); // isteğe bağlı
    }
}

public sealed class AcmeTemplates : IServiceTemplateProvider
{
    private static readonly ServiceTemplate Cache = new()
    {
        Key = "acme.services.cache",
        DisplayName = "Acme Cache",
        Category = ManagedServiceCategory.Database,
        Description = "Bellek içi önbellek.",
        LogoFile = "cache.svg",               // Content/cache.svg
        Color = "#0EA5E9",
        Image = "acme/cache",
        Tags = ["2.1", "2.0"],
        Ports = [new ServicePortDefinition("db", 7000, ServicePortRole.Primary, "Cache")],
        Credentials = new ServiceCredentialSpec { UsernameKind = ServiceUsernameKind.None },
        DataPath = _ => "/data",
        Environment = c => [new("ACME_PASSWORD", c.Password ?? string.Empty, Secret: true)],
        HealthCommand = "acme-cli ping | grep -q PONG",
        ConnectionString = (e, c) => $"acme://:{Uri.EscapeDataString(c.Password ?? "")}@{ServiceConnectionStrings.HostPort(e)}"
    };

    public IReadOnlyList<ServiceTemplate> GetTemplates() => [Cache];

    public IReadOnlyList<ServiceTemplateCategory> GetCategories() => []; // isteğe bağlı
}
```

Sağlayıcı ve kancalar **singleton** olarak kaydedilmelidir. Şablonlar uygulama açıkken bir kez okunur.

## JSON ile şablon (kodsuz)

Eklenti klasörüne `templates/<ad>.json` ekleyin. Dosya tek bir şablon nesnesi veya `{ "categories": [...], "templates": [...] }` biçiminde olabilir. Editör otomatik tamamlaması için `$schema` alanına `docs/schemas/service-template.schema.json` dosyasının göreli yolunu yazın.

```json
{
  "$schema": "../../../../docs/schemas/service-template.schema.json",
  "key": "acme.services.cache",
  "displayName": "Acme Cache",
  "category": "database",
  "description": "Bellek içi önbellek.",
  "logo": "cache.svg",
  "color": "#0EA5E9",
  "image": "acme/cache",
  "tags": ["2.1", "2.0"],
  "ports": [ { "name": "db", "containerPort": 7000, "role": "primary", "label": "Cache" } ],
  "credentials": { "username": "none", "password": "standard" },
  "dataPath": "/data",
  "environment": [ { "key": "ACME_PASSWORD", "value": "{{password}}", "secret": true } ],
  "healthCommand": "acme-cli ping | grep -q PONG",
  "connectionString": "acme://:{{password:url}}@{{host}}:{{port}}",
  "suggestedEnvironment": { "ACME_URL": "acme://:{{password:url}}@{{host}}:{{port}}" }
}
```

Bilinmeyen alanlar hata sayılır (yazım hatası yakalanır). `//` yorumları ve sondaki virgüller kabul edilir.

### Yer tutucular

| Yer tutucu | Değer | Kullanılabildiği yer |
| --- | --- | --- |
| `{{host}}` | Container adı veya sunucu adresi (IPv6 köşeli parantezle) | `connectionString`, `suggestedEnvironment` |
| `{{port}}` | Ana port (iç port veya yayınlanan port) | `connectionString`, `suggestedEnvironment` |
| `{{username}}`, `{{password}}`, `{{database}}` | Formdaki kimlik bilgileri | `environment`, `connectionString`, `suggestedEnvironment` |
| `{{encryptionKey}}` | Kurulumda üretilen anahtar (`generateEncryptionKey: true`) | `environment`, `connectionString`, `suggestedEnvironment` |

Biçimleyiciler: `{{password:url}}` URL için yüzde kodlar, `{{password:ado}}` ise ADO.NET bağlantı metni değeri olarak kaçışlar. `defaultEnvironment` yer tutucu içeremez. Bilinmeyen yer tutucu ya da biçimleyici şablonun atlanmasına yol açar.

## Alan başvurusu

| C# özelliği | JSON alanı | Zorunlu | Açıklama |
| --- | --- | --- | --- |
| `Key` | `key` | Evet | Kalıcı anahtar (ad alanlı) |
| `DisplayName` | `displayName` | Evet | Kart başlığı, en fazla 64 karakter |
| `Category` | `category` | Evet | `Database` / `Application` (JSON: `database` / `application`) |
| `CategoryKey` | `group` | Hayır | Servis ekle sayfasındaki grup; yoksa `category` grubu |
| `Description` | `description` | Evet | Kısa açıklama, en fazla 500 karakter |
| `LogoFile` | `logo` | Hayır | `Content/` içindeki `.svg`, `.png` veya `.webp` dosyası; `/plugins/<systemname>/<dosya>` adresinden sunulur |
| `Color` | `color` | Hayır (JSON) | `#RRGGBB` |
| `Image` | `image` | Evet | Etiketsiz Docker imajı (`getmeili/meilisearch`, `quay.io/keycloak/keycloak`) |
| `Tags` | `tags` | Evet | 1–20 sürüm etiketi; ilki önerilir |
| `Ports` | `ports` | Evet | En fazla 10 port: `name`, `containerPort`, `role` (`primary`/`webUi`), `label`, `publishByDefault` |
| `Credentials` | `credentials` | Hayır | `username` (`none`/`name`/`fixed`/`email`), `defaultUsername`, `usernameLabel`, `password` (`none`/`standard`/`mssqlComplex`), `database`, `defaultDatabase`, `reservedUsernames`, `generateEncryptionKey` |
| `DataPath` | `dataPath` | Hayır | Container içindeki kalıcı veri klasörü (mutlak yol) |
| `DataOwner` | `dataOwner` | Hayır | Sunucu klasörü modunda sahiplik, `uid:gid` |
| `Environment` | `environment` | Hayır | Kimlik bilgisi değişkenleri; kullanıcı ezemez |
| `DefaultEnvironment` | `defaultEnvironment` | Hayır | Kullanıcının değiştirebileceği varsayılan değişkenler |
| `Command` | `command` | Hayır | İmajdan sonraki argümanlar (en fazla 32) |
| `HealthCommand` | `healthCommand` | Evet (eklenti) | Docker sağlık kontrolü, container içinde |
| `ReadinessCommand` | `readinessCommand` | Hayır | Kurulumdan sonra kimlik bilgileriyle bağlantı testi |
| `ConsoleCommand` / `ConsoleLabel` | `consoleCommand` / `consoleLabel` | Hayır | Konsol sekmesindeki istemci; yoksa bash/sh açılır |
| `ConnectionString` | `connectionString` | Hayır | Uygulamaların bağlanacağı adres |
| `SuggestedEnvironment` | `suggestedEnvironment` | Hayır | "Projeye bağla" ile önerilen değişkenler |
| `RequiresX86` | `requiresX86` | Hayır | Yalnızca x86_64 sunucularda kurulabilir |
| `MinMemoryMb` / `MemoryHint` | `minMemoryMb` / `memoryHint` | Hayır | Bellek sınırının alt değeri ve ipucu |
| `WarnOnMajorUpgrade` | `warnOnMajorUpgrade` | Hayır | Ana sürüm değişiminde onay ister |
| `HealthTimeoutSeconds` | `healthTimeoutSeconds` | Hayır | 30–1800 saniye, varsayılan 180 |
| `Notes` | `notes` | Hayır | Formda gösterilen not |

Eklenti grubu tanımı (`ServiceTemplateCategory` veya JSON `categories`): `key`, `displayName` ve `order` alanlarından oluşur. Yerleşik gruplar 10 ve 20 sırasındadır.

## Doğrulama kuralları

Bir şablon aşağıdaki kurallardan herhangi birini ihlal ederse yüklenmez:

- Anahtar ad alanında ve benzersiz olmalıdır. Grup anahtarı ya yerleşik bir grup ya da aynı eklentinin tanımladığı bir grup olmalıdır.
- `image` küçük harfli Docker referansı olmalıdır (isteğe bağlı kayıt sunucusu ve port içerebilir, etiket içeremez). Etiketler Docker etiket kurallarına uymalıdır.
- Portlar 1–65535 aralığında olmalıdır; port numaraları ve adları şablon içinde benzersizdir.
- `dataPath` mutlak olmalıdır. `..`, boşluk ve özel karakter içeremez; her etiket için kontrol edilir.
- Ortam değişkeni adları `[A-Za-z_][A-Za-z0-9_]*` biçiminde olmalıdır. Değerler satır sonu veya NUL içeremez. Parola veya anahtar içeren değişken `secret: true` olmalıdır.
- `healthCommand` zorunludur. Sağlık, hazır olma ve konsol komutları tek satır olmalı, en fazla 2000 karakter olabilir.
- C# şablonunun fonksiyonları (`DataPath`, `Environment`, `ConnectionString`, `SuggestedEnvironment`) örnek değerlerle çağrılır; hata fırlatan şablon geçersiz sayılır.
- Logo dosyası bulunamazsa şablon yine yüklenir, ancak uyarı gösterilir.

## Güvenlik kuralları

- **Ham kabuk yok.** Sunucuda çalışan komutların (`docker create`, `docker pull`, ağ, volume, güvenlik duvarı) tamamı çekirdek tarafından üretilir ve her değer `ShellQuote` ile kaçışlanır. Şablon yalnızca veri sağlar.
- `healthCommand`, `readinessCommand` ve `consoleCommand` **container içinde** `sh -c` ile çalışır, sunucunun kabuğunda çalışmaz.
- **Gizli değerler yalnızca ortam dosyasıyla** verilir: çekirdek değerleri sunucuya stdin ile 0600 izinli bir `--env-file` olarak yazar. Komutlarda parolayı doğrudan yazmayın; container ortamından okuyun (`"$ACME_PASSWORD"`). Gizli değerler işlem loglarında maskelenir.
- `IServiceTemplateHooks.BuildExtraArgs` yalnızca imajdan sonraki argümanları döner. Satır sonu veya NUL içeren, 1024 karakteri aşan ya da parola veya anahtar barındıran argüman işlemi durdurur, çünkü bu argümanlar `docker inspect` çıktısında görünür.
- Kancalar yalnızca kendi eklentisinin şablonuna bağlanabilir. Eklenti devre dışıyken hiçbir kanca çağrılmaz.
- Kanca iletileri işlem loguna yazılırken kontrol karakterleri (ANSI kaçışları dahil) temizlenir.

## Kancalar (isteğe bağlı)

```csharp
public sealed class AcmeHooks : IServiceTemplateHooks
{
    public string TemplateKey => "acme.services.cache";

    // Kurulum ve Ayarlar formunun ek doğrulaması (parola verilmez).
    public IEnumerable<ServiceTemplateHookError> Validate(ServiceTemplateFormContext context) => [];

    // İmajdan sonra eklenecek argümanlar (kurulum, yeniden oluşturma, yükseltme).
    public IReadOnlyList<string> BuildExtraArgs(ServiceTemplateCommandContext context) =>
        context.Environment.ContainsKey("ACME_CLUSTER") ? ["--cluster"] : [];

    // Kurulum başarıyla bitince; hata işlemi düşürmez, logda uyarı olarak görünür.
    public Task AfterInstallAsync(ServiceTemplateInstallContext context, IServiceTemplateHookLog log, CancellationToken ct) =>
        log.InfoAsync($"Hazır: {context.ContainerName}", ct);
}
```

`Validate` veya `BuildExtraArgs` hata fırlatırsa işlem güvenli biçimde durur ve ayrıntı uygulama loguna yazılır. Keycloak örneği, `KC_HOSTNAME` girildiğinde `start` (üretim) kipini, girilmediğinde `start-dev` kipini seçer.

## Eklenti devre dışı veya kaldırılmışsa

- Kayıtlı servisin `TemplateKey` değeri değişmez.
- Liste ve detay sayfasında **Şablon eklentisi devre dışı** (veya **bulunamadı**) etiketi görünür.
- Loglar, başlat/durdur/yeniden başlat, güvenlik duvarını yeniden uygulama ve kaldırma kullanılabilir. Konsol, eklentinin istemci komutu yerine kabuk açar.
- **Ayarlar** (yeniden oluşturma) ve **Sürüm** (yükseltme) engellenir. Eklenti yeniden etkinleştirildiğinde tekrar açılır.
- Otomatik yedekleme yalnızca yerleşik veritabanı şablonlarında (PostgreSQL, MySQL/MariaDB, MongoDB, Redis, SQL Server) desteklenir.

## Test ipuçları

- `ServiceTemplateValidator.Validate(template, "Acme.Services")` boş liste dönmelidir. Bunu birim testinde doğrulayın.
- JSON dosyalarını `ServiceTemplateJson.Parse(File.ReadAllText(path), name)` ile okuyun; `Errors` boş olmalıdır.
- Katalog testi: `new PluginCatalog([new LoadedPlugin(descriptor, eklentiKlasoru, typeof(AcmeTemplates).Assembly, null)])` oluşturun, `SetState(systemName, true)` çağırın ve `new ServiceTemplateCatalog(plugins, [new AcmeTemplates()], [], NullLogger<ServiceTemplateCatalog>.Instance)` ile `Issues` listesinin boş olduğunu doğrulayın. Örnekler: `tests/ServerManager.Application.Tests/ManagedServices/ServiceTemplateCatalogTests.cs`.
- Uçtan uca kontrol: eklentiyi **Eklentiler** sayfasından kurup etkinleştirin. **Servisler → Servis ekle** sayfasında kartın görünmesi, kurulumun sağlık kontrolünden geçmesi ve **Eklentiler** kartında uyarı olmaması gerekir.
- `tests/ServerManager.Web.Tests/PluginLoadingTests` paketlenen her eklentinin yüklendiğini denetler. Yeni eklentiniz de bu testten geçmelidir.
