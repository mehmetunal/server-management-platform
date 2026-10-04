# Mag Server Manager — Teknik doküman

Çalışan sistemin mimarisi, güvenlik modeli, veri kuralları ve modül davranışıdır. Ekran adımları [kullanma kılavuzunda](kullanim-kilavuzu.md), sade anlatım [son kullanıcı kılavuzundadır](son-kullanici.md).

Ürün hedefi, faz listesi ve her yapılandırma anahtarının tam tablosu depo kökündeki [README.md](../README.md) içindedir. README’nin 1–82. bölümleri hedef spesifikasyondur; çalışan davranış **Geliştirme Ortamı** başlığından itibaren yazılır. Bu dosya o davranışı tek yerde toplar. Anahtar tablosu README’de daha genişse README esas alınır.

## 1. Çözüm

```text
src/ServerManager.Domain
src/ServerManager.Application          servis, DTO, validator, arayüz
src/ServerManager.Infrastructure       EF Core, repository, Identity, SSH, şifreleme
src/ServerManager.Web.Framework         yetki, JSON yanıt, sunucu sayfası, eklenti yükleyici
src/ServerManager.Web                   MVC, Razor, wwwroot
src/Plugins/ServerManager.Plugin.*      host'a derleme referansı vermeyen eklentiler
tests/
```

Akış `Controller → Service → Repository` şeklindedir. Mediator yoktur. Her `.cs` dosyasında tek üst düzey tip bulunur. Kod tanımlayıcıları İngilizce, arayüz metinleri Türkçedir.

Web, eklenti projelerini çözümdeki `src/Plugins/*/*.csproj` üzerinden derler ama onlara assembly referansı vermez. Çıktı `src/ServerManager.Web/Plugins/{SystemName}/` altına yazılır ve açılışta yüklenir.

## 2. Çalışma zamanı

| Katman | Seçim |
| --- | --- |
| Çalışma zamanı | .NET 10, ASP.NET Core MVC |
| Arayüz | Razor, Tailwind CSS v4, Sass, ES module |
| Onay ve bildirim | SweetAlert2, toastr |
| Terminal ve editör | xterm.js, CodeMirror 5 |
| Kimlik | ASP.NET Core Identity, Guid anahtar, permission claim |
| Veri | Microsoft SQL Server, EF Core yalnızca sorgu, şema FluentMigrator |
| Doğrulama | FluentValidation |
| SSH | SSH.NET |
| Canlı veri | SignalR, Chart.js |
| Log | Serilog, konsol ve `logs/` |

Gizli değerler `appsettings.json` içine yazılmaz. Geliştirmede `dotnet user-secrets`, sunucuda ortam değişkeni (`Security__MasterKey` biçimi) kullanılır.

Yerel veritabanı `docker compose up -d db` ile yalnızca `127.0.0.1:14340` üzerinde MSSQL 2022 çalıştırır. Açılışta migration, rol, izin ve ilk SuperAdmin idempotent uygulanır. Mevcut kullanıcının parolası ve rolü seed tarafından değiştirilmez.

```bash
dotnet run --project src/ServerManager.Web --launch-profile https
dotnet test --solution ServerManager.slnx
```

HTTP profili `http://localhost:5180`, HTTPS profili `https://localhost:7180`. Testler canlı veritabanına ve gerçek sunucuya bağlanmaz.

## 3. İstek ve yanıt

Sayfalar varsayılan olarak oturum ister. Controller ve action `[HasPermission]` ile korunur. POST istekleri antiforgery doğrular. AJAX, `RequestVerificationToken` başlığını gönderir.

JSON gövdesi:

```json
{ "isSuccess": true, "message": "", "data": {}, "errors": {} }
```

Başarısızlıkta HTTP kodu sonuca göredir (400, 401, 403, 404, 409, 429, 500). İş kuralı controller’da durmaz.

Hassas uçlar hız sınırına tabidir. Örnekler: giriş ve bağlantı testi, Docker aksiyonu dakika başına 30, Dokploy ve Dokku dakika başına 20, agent raporu IP başına 120. Pencere kullanıcıya, oturum yoksa IP’ye göredir.

## 4. Kimlik ve yetki

İzinler `AspNetRoleClaims` tablosunda `permission` claim’i olarak durur. Seeder yalnızca eksik izni ekler; elle verilmiş izni silmez. SuperAdmin, eklenti izinleri dahil tümünü açılışta alır. `plugin.manage` başka role verilmez.

| Rol | Özet |
| --- | --- |
| SuperAdmin | Tümü, eklenti yönetimi ve ayarlar |
| Admin | Sunucu, Docker, terminal, dosya, dağıtım, alarm, yedek, güvenlik, audit, komut, bulut, ayarlar. Kullanıcı yönetimi yok |
| Operator | Görüntüleme, bağlantı testi, Docker yaşam döngüsü, terminal, dosya yazma, dağıtım çalıştırma, alarm üstlenme, yedek çalıştırma, güvenlik taraması |
| Developer | Görüntüleme, Docker yeniden başlatma, dosya indirme, dağıtım çalıştırma |
| Viewer | Dashboard, sunucu, Docker, dağıtım ve alarm görüntüleme |

Pasif veya kilitli hesabın açık oturumu en geç bir dakika içinde düşer. Beş hatalı girişte 15 dakika kilit. İki adımlı doğrulama TOTP’dir. `TwoFactor:Required` ile zorunlu olur.

Oturum çerezi `SameSite=Strict` olduğu için dış siteden dönüşte (GitHub App kurulumu) çerez gitmez. Bu akış girişsiz bir ara sayfadan aynı siteye geçer ve asıl işlem orada izinle yapılır.

## 5. Gizliler

`Security:MasterKey` 32 bayt base64’tür. SSH parolası, private key, passphrase, sudo parolası, yedek parolası, bulut API anahtarı, bildirim sırrı ve Dokploy API anahtarı AES-256-GCM ile şifrelenir. Şifreli değer `v{KeyVersion}:` öneki taşır. Anahtar değişirse eski kayıtlar çözülemez.

Anahtar loga, audit metnine ve HTML’e yazılmaz. Formda secret alan kayıttan sonra boş gelir. Boş gönderilirse eski değer kalır.

Agent token’ı `sma_` artı 43 URL-güvenli karakterdir. Yalnızca oluşturulduğu anda, kurulum komutunun içinde bir kez gösterilir. Veritabanında SHA-256 özeti durur.

## 6. SSH ve ölçüm

Bağlantı agentless’tır: panel SSH açar, komut çalıştırır, dosya yazmaz. Host key ilk başarılı bağlantı testinde kaydedilir (TOFU). Sonraki bağlantıda farklı anahtar gelirse bağlantı reddedilir ve sunucu Offline olur. Adres veya port değişince parmak izi silinir.

Sudo: parola varsa `sudo -S -p '' --` ve parola stdin’den gider, komut satırına yazılmaz. Parola yoksa `sudo -n`. Yükseltme yalnızca sunucuda sudo işaretliyse uygulanır.

`MetricsCollectorWorker` izlemesi açık ve parmak izi doğrulanmış sunucuya, agent’ı son üç dakika içinde raporladıysa SSH açmaz. Aralık `Monitoring:IntervalSeconds` (varsayılan 30, en az 10), eşzamanlılık `MaxConcurrency` (4). Tek salt okunur komut `/proc`, `df`, `ps` ve `ip` okur.

Durum: eşik yoksa Healthy, uyarı eşiği Warning, kritik eşik Critical. `OfflineAfterFailures` (2) ardışık hatada Offline. Bakımdayken durum otomatik değişmez, ölçüm sürer. Her geçiş `server.status_changed` olarak, kullanıcı “Sistem” diye audit’e yazılır.

Ham ölçüm `RawRetentionHours` (48), saatlik özet `HourlyRetentionDays` (90), sağlık kaydı `HealthCheckRetentionDays` (30). Kısa grafikler ham veriden, 7 ve 30 gün saatlik özettendir. Her sunucunun son ölçümü ve son 50 sağlık kaydı süre dolsa da kalır. Silme yalnızca izin listesindeki geçici tablolarda, 5000 satırlık partilerle çalışır. Liste dışı tablo istenirse iş durur. Profil, bakiye, kullanıcı adı ve audit silinmez.

Agent `POST /api/agent/report` ile aynı ölçüm metnini yollar. Oturum yoktur. Bearer token ve `X-Agent-Version` gerekir. İki rapor arası en az 20 saniye, gövde en fazla 256 KB. İzleme kapalıysa rapor reddedilir. Kurulum betiği `GET /api/agent/install.sh` token içermez. `SM_URL` ve `SM_TOKEN` ortamdan gelir. systemd varsa dakikalık zamanlayıcı, yoksa cron kurulur.

Canlı ölçüm `/hubs/monitoring` üzerindedir. Hub `server.view` ister. Sunucu sayfası kendi kimliğini, liste ve dashboard tümünü dinler.

## 7. Docker, terminal, dosya

Docker CLI SSH ile çalışır. Docker API dışarı açılmaz. Adlar allowlist regex’inden geçer, argümanlar POSIX tek tırnakla kaçışlanır.

Container terminali ve sunucu terminali `/hubs/terminal` kullanır. Çıktı loglanmaz. Girilen satır `TerminalCommands` tablosuna yazılır. Parola istemi ve tam ekran program (vim, less, top) komut sayılmaz. Ok, Tab veya geçmişle değişen satır yaklaşık işaretlenir.

Tehlikeli komut modu `Terminal:DangerousCommandMode`: `Off`, `Warn`, `Confirm` (varsayılan), `Block`. Varsayılan kalıplar `rm -r`, `mkfs`, `dd of=`, kapatma, reboot, güvenlik duvarı, `userdel`, disk aygıtı ve fork bomb’tur. `DangerousCommands` doluysa o liste kullanılır. Onay `ConfirmationTimeoutSeconds` (120) içinde gelmezse komut iptal edilir.

Boşta kalma `IdleTimeoutMinutes` (30). Kopan tarayıcı `ReconnectGraceSeconds` (120) boyunca oturumu tutar. Kullanıcı başına `MaxSessionsPerUser` (5) oturum, sunucu ve container birlikte sayılır. Uygulama kapanırken açık oturum silinmeden kapandı işaretlenir. Terminal geçmişi yaşa göre silinmez.

Dosya yöneticisi SFTP’dir. Yol normalleştirilir, kök dışına çıkılmaz. Düzenlenebilir dosya boyutu yapılandırma ile sınırlıdır.

Toplu komut hedef başına `sh -c` çalıştırır, en fazla 5 sunucu paralel, çıktının son 32.000 karakteri saklanır. Süre 5–900 saniye, metin en fazla 8000 karakter, hedef en fazla 100 sunucu. Sudo kutusu, sudo kapalı sunucuda komutu çalıştırmaz, o hedefi başarısız yazar. HTTP isteği bitince iş sürer. Süreç ölürse kayıt “kesildi” olur. Geçmiş silinmez.

## 8. Yedekleme

SSH stdout’u panele akar, sunucuda geçici arşiv oluşmaz. Panel akışı depolamaya yazar.

| Tür | Komut | Yetki |
| --- | --- | --- |
| Dosya | `tar -czf -` | İlgili yolları okuyacak sudo veya root |
| Volume | `alpine:3` container, volume salt okunur, `--network none` | docker grubu veya yalnızca docker için sudo |
| Veritabanı, container | `docker exec` içinde `pg_dump` veya `mariadb-dump`/`mysqldump`, sonra gzip | docker |
| Veritabanı, sunucu | Sunucudaki istemci ve gzip | İstemci kurulu olmalı |

PostgreSQL `--clean --if-exists --no-owner --no-privileges`. MySQL `--single-transaction --routines --triggers`. Parola stdin ve yalnızca döküm sürecinin ortam değişkeniyle gider.

Nesne adı `{önek}{iş}/{yyyyMMdd-HHmmss}-{kısa}.tar.gz` veya veritabanında `.sql.gz`. Şifreliyse sonuna `.smbk` eklenir. SMBK sürüm 1: 36 bayt başlık, PBKDF2-HMAC-SHA256, 1 MiB AES-256-GCM parçaları. Tur sayısı `Backup:KeyDerivationIterations`. Her çalıştırma parola kopyasını kendinde tutar. İşin parolası değişse de eski dosya kendi kopyasıyla açılır.

Saatler `Backup:TimeZone` yerel saatine göredir. Kaçırılan çalışma açılışta bir kez telafi edilir. Aynı işte tek işlem. Toplam eşzamanlılık `Backup:MaxConcurrency`. İptal yarım nesneyi siler. S3’te yarım multipart yükleme iptal edilir. Azure’da başarısız yüklemeden sonra blob silinmeye çalışılır. Kapanışta süren iş “kesildi” olur.

Saklama, yeni yedek başarıldıktan sonra yalnızca o işin başarılı dosyalarını siler. Son N (1–365) ve isteğe bağlı N günden eski. En yeni başarılı dosya silinmez. Çalışma satırı, log ve audit kalır.

İş ve depolama yumuşak silinir. `GetDetails` silinmiş işi de okur. Düzenleme, silme ve yeni çalıştırma kapalıdır. Geçmiş indirme ve geri yükleme açıktır.

Sağlayıcılar `IBackupStorageProvider` uygular. Çekirdek yerel diski, `Storage.S3` ve `Storage.AzureBlob` eklentileri nesne deposunu getirir. Kayıtlı `ProviderSystemName` değiştirilmez. Eklenti kapalıysa o hedef kullanılamaz, kayıt durur.

## 9. Dağıtım ve GitHub

Proje çekirdektedir. Dokploy veya Dokku dağıtımının yerine geçmez. Git, hedef sunucuda SSH ile çalışır. Erişim anahtarı `x-access-token` olarak stdin’den gider, diske ve loga yazılmaz.

GitHub App eklentisi `contents: read` ve `metadata: read` ister. Private key ile kısa ömürlü JWT, oradan kurulum anahtarı üretilir. Anahtar önbelleğe alınmaz ve tek depoya daraltılır. Private key, client secret ve webhook secret şifrelidir. Uygulamayı kullanan proje varken kayıt kaldırılamaz. Kaldırma yumuşak siler, GitHub’daki uygulamayı silmez.

`GitHub:PublicBaseUrl` boşsa isteğin adresi dönüş adresi olur. Ters vekil varsa doldurulmalıdır. Manifest ve kurulum dönüşü, Strict çerez yüzünden ara sayfa kullanır.

## 10. Alarm, uptime, SSL, güvenlik taraması

Değerlendirici `Alerting:EvaluationIntervalSeconds` (60) ile kural bakar. Türler doluluk, çevrimdışı, uptime, SSL bitişi ve yedek başarısızlığını kapsar. Bildirim e-posta, Telegram ve Discord eklentileriyle gider. Teslim kaydı saklama süresi dolunca silinebilir. Kural ve kanal yumuşak silinir.

Uptime ve SSL kendi aralıklarında çalışır. Sonuç satırları yaşa göre temizlenebilir. Kontrolün kendisi ve audit kalır.

Güvenlik taraması SSH ile `sshd -T` veya `sshd_config`, port, güvenlik duvarı ve bekleyen güncelleme okur. Sunucuyu değiştirmez. Parmak izi yoksa tarama açılmaz.

## 11. Bulut ve maliyet

`Cloud.Hetzner`, `Cloud.DigitalOcean`, `Cloud.Vultr`, `Cloud.Linode` ve `Cloud.Scaleway` eklentidir. API anahtarı kayıttan önce doğrulanır. İstemci yönlendirme izlemez. 401, 403 ve 429 Türkçe mesaja çevrilir. Scaleway anahtarı `X-Auth-Token` başlığıyla gider; proje kimliği `account/v3/projects` içinden, adı `default` olan proje tercih edilerek okunur.

Eşitleme `Cloud:SyncIntervalHours` (6, `0` kapatır) veya elle çalışır. Aynı IP birden fazla panel kaydındaysa otomatik bağlanmaz. Hetzner fiyatı konumun KDV hariç aylık EUR tutarıdır. DigitalOcean, Vultr ve Linode aylık USD tutarıdır. Scaleway katalog saatlik EUR yayınlarsa aylık tutar saatlik fiyat × 730 olur. Fiyat dönmeyen sunucunun elle girilmiş maliyeti değişmez. Scaleway hesabın açmadığı bölge 400/404 döner ve atlanır.

Oluşturma onayı sunucu adını ister. SSH public key cloud-init içine eklenir. Hetzner’in tek seferlik root parolası, Vultr `default_password` alanı ve Linode’un üretilen root parolası yalnızca HTTP yanıtında gösterilir, veritabanı ve audit’e yazılmaz. Vultr `user_data` ve Linode `metadata.user_data` base64 gider. Scaleway cloud-init ayrı bir PATCH ile yazılır, sunucu `stopped` ise ardından `poweron` çağrılır. Linode etiketi harfle başlar, 3–64 karakterdir ve nokta, art arda tire veya alt çizgi kabul etmez.

Maliyet sayfası kur çevirmez. Para birimi yoksa ve tutar varsa USD sayılır. Grup silinince sunucu silinmez, gruptan çıkar.

## 12. Eklentiler

`plugin.json` alanları: `SystemName`, `FriendlyName`, `Group`, `Version`, `Author`, `Description`, `DisplayOrder`, `AssemblyFileName`, isteğe bağlı `Logo`. `SystemName` yalnızca harf, rakam ve noktadır, sonradan değişmez. Logo yalnızca `Content` altındaki düz dosya adıdır. `..` ve klasör ayracı reddedilir. Adres `/plugins/{systemname-küçük}/logo.svg` olur.

`IPluginStartup.ConfigureServices` eklenti kapalıyken de çalışır. Controller, hub ve sunucu sekmesi kapalı eklentide 404 veya gizli olur. Arka plan işi her turda `IPluginCatalog.IsEnabled` bakmalıdır.

Kurulum migration, izin ve `InstalledPlugins` satırı yazar. Devre dışı bırakmak tabloyu silmez. Uninstall yoktur. Yeni dll veya yeni klasör için süreç yeniden başlar. Kur, etkinleştir ve kapat yeniden başlatma istemez.

`Plugins:InstallOnStartup` listesindeki eklenti hiç kurulmamışsa açılışta kurulur. Sonradan kapatılan eklenti bu liste yüzünden yeniden açılmaz.

| SystemName | Grup | İş |
| --- | --- | --- |
| DevOps.Dokploy | DevOps | Kurulum sihirbazı, sağlık kontrolü, API ile proje özeti |
| DevOps.Dokku | DevOps | Sürüm, uygulama listesi, bootstrap kurulumu, `ps:restart` |
| Git.GitHub | Git | GitHub App |
| Notifications.Email | Bildirim | SMTP |
| Notifications.Telegram | Bildirim | Bot |
| Notifications.Discord | Bildirim | Webhook |
| Storage.S3 | Yedekleme | S3 API |
| Storage.AzureBlob | Yedekleme | Azure Blob ve Azurite |
| Cloud.Hetzner | Bulut | Hetzner Cloud |
| Cloud.DigitalOcean | Bulut | DigitalOcean |
| Cloud.Vultr | Bulut | Vultr |
| Cloud.Linode | Bulut | Linode (Akamai) |
| Cloud.Scaleway | Bulut | Scaleway |

Dokku kurulum betiği `https://dokku.com/install/{Dokku:Version}/bootstrap.sh` adresindendir. Sürüm `v0.38.31` biçiminde doğrulanır, kabuğa kaçışlanarak yazılır. Çıktı süreç belleğindedir, veritabanına yazılmaz. Süreç ölürse sunucudaki betik sürebilir. Sonuç `dokku.install_complete` audit kaydıdır.

Dokploy kurulum kaydı ve çıktısı eklenti tablosundadır. Sayfa kapansa da iş sürer, dönünce çıktı yeniden basılır. API anahtarı başka hosta yönlendirilmesin diye HTTP istemcisi yönlendirme izlemez.

Eklenti şeması yalnızca kendi migration’ındadır. Sürüm numarası tüm uygulamada benzersiz bir zaman damgasıdır. Host tablosuna yalnızca foreign key bağlanır. Host tablosunun kolonu eklentiyle değişmez. Şema değişikliği eklemedir: kolon silinmez, yeniden adlandırılmaz, tipi değişmez.

## 13. Veri kuralları

Silme yumuşaktır. Profil, maliyet, istatistik, satın alma ve audit otomatik işlerle silinmez. Okunamayan satır (sahibi veya tarihi yok) silinmez.

Silinebilen geçici veri ölçümü oluşturulma zamanına değil son harekete bakar ve pay bırakır. Geçmiş listeleri yaşa göre boşaltılmaz. Uygulamanın gösterdiği son N kayıt kalır.

Eski istemciyle uyum: alan eklenir, silinmez. Yeni alan opsiyoneldir ve varsayılanla okunur. Sıkılaştırılmış kural, mağazadaki eski sürüm çoğunlukla güncellenmeden yüklenmez. Bu depoda mağaza istemcisi yoktur. Kural yine de API ve şema içindir.

Audit HMAC zinciriyle imzalanır ve panelden doğrulanabilir. Satır silinmez ve güncellenmez.

## 14. Arayüz

`ViewData["Page"]` layout’a `css/pages/{sayfa}.css` ve `js/pages/{sayfa}.js` ekletir. Eklenti view’ı ayrıca `ViewData[PluginContent.PagePluginKey]` atar. Dosya `/plugins/{systemname}/` altından gelir.

JavaScript yalnızca ES module’dür. Host modülü `@app/` import map’i ile sürüm parametreli adrese gider. Liste ve form AJAX’tır. Tehlikeli onayda ad yazılır. Her düğme ve bağlantıda `title` vardır.

Sayfa stili `npm run build:css` ile üretilir. Eklenti SCSS’i aynı komutla eklentinin `Content/css/pages` klasörüne çıkar. Derlenmiş CSS repoya dahildir.

## 15. Gözlemlenebilirlik ve sınırlar

Serilog konsol ve dosyaya yazar. İstek süresi ve sonuç kodu loglanır. Gizli değer ve terminal çıktısı loglanmaz.

Ayarlar sayfası (`settings.view`) sürüm, ortam, süreç süresi ve şema sürümünü okur. `settings.manage` izleme, alarm, yedek, tarama ve bulut aralıklarını `PanelSettings` tablosuna yazar ve aynı süreçteki seçenek nesnesine uygular. Agent aralığı, bağlantı dizesi, master key ve API anahtarı bu sayfadan değişmez.

Sunucu durumu Bakımda iken otomatik durum geçişi yapılmaz, ölçüm sürer. `MetricsMaintenanceWorker` ham ölçümü saatlik özete çevirir ve saklama süresini uygular. Aralık `Monitoring:MaintenanceIntervalMinutes` (10).

Bu dokümanın kapsamı çalışan modüllerdir. README’de hedef olarak yazılmış olup çekirdekte ayrı modülü olmayan işler (yapay zeka asistanı, genel port tarayıcı, webhook ürünü, Nginx yönetim ekranı) burada yok sayılır.
