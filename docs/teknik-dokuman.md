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

HTTP profili `http://localhost:5180`, HTTPS profili `https://localhost:7180`. Birim testleri canlı veritabanına ve gerçek sunucuya bağlanmaz. `tests/ServerManager.Web.Tests` uygulamayı `WebApplicationFactory<Program>` ile açıp kimlik, antiforgery, izin, kilit, hız sınırı, 2FA, agent token'ı ve sağlık uçlarını dener; açılış migration gerektirdiği için `SM_TEST_SQL` (veritabanı adı içermeyen SQL Server bağlantı dizesi) ister, yoksa atlanır. Her çalıştırma geçici bir veritabanı açıp siler.

**Uçtan uca testler:** `tests/ServerManager.E2E.Tests` uygulamayı Production ortamında açar ve uygulama servisleri üzerinden gerçek bir SSH sunucusuna karşı çalışır. Sunucu `docker/e2e-ubuntu` imajıdır (Ubuntu 24.04, sshd, parolalı ve NOPASSWD sudo kullanıcıları, privileged Docker-in-Docker; systemd/journald yok) ve `docker compose -p sm-e2e --profile e2e up -d --build sm-e2e-ubuntu` ile `127.0.0.1:${E2E_SSH_PORT:-2224}` üzerinde açılır. Kapsam: sunucu ekleme, host key sabitleme ve uyuşmazlık, Docker genel bakışı, Redis/PostgreSQL kurulumu, sağlık, bağlantı, loglar, yedek ve indirme, verisiyle kaldırma, temizlik taraması ve önizlemesi, kaynak kullanımı ve `file://` bare git deposundan Dockerfile deployment'ı. `SM_TEST_SQL` ve `SM_E2E_SSH_HOST/PORT/USER/PASSWORD` (isteğe bağlı `SM_E2E_SSH_NOPASSWD_USER`) tanımlı değilse testler atlanır. Her test kendi kayıtlarını ve sunucudaki kaynaklarını siler.

**CI:** `.github/workflows/ci.yml` her push ve PR'da şunları çalıştırır: Release derleme ve SQL Server servis konteyneriyle tüm testler (TRX artifact'ı), güvenlik açığı olan NuGet paketi kontrolü, ön yüz derlemesi ve commit edilmiş `wwwroot`/eklenti `Content` çıktısının güncelliği, kök `Dockerfile` derlemesi. E2E işi yalnızca elle tetiklemede, gece koşusunda ve `main` push'unda çalışır. `.github/dependabot.yml` NuGet, npm, Actions ve Docker güncellemelerini haftalık ve gruplanmış açar.

**Docker:** kökteki `Dockerfile` SDK 10 ile yayınlar, `aspnet:10.0` üzerinde ayrıcalıksız `app` kullanıcısıyla `8080` portunda çalışır. Eklentiler yayın çıktısındaki `Plugins/` klasörüyle imaja girer. `tzdata` kuruludur. Volume'ler `/app/App_Data` (yerel yedekler) ve `/app/logs`. `HEALTHCHECK` `/health` adresini yoklar. `docker compose --profile app up -d --build` uygulamayı `db` sağlıklı olunca başlatır. Bağlantı dizesi, `Security__MasterKey` ve seed değerleri `.env` dosyasından gelir (`.env.example`). Production'da çerezler `Secure` olduğundan panel TLS sonlandıran bir vekil arkasında kullanılır.

**Sağlık uçları:** `GET /health` canlılıktır, oturumsuz `200` döner. `GET /health/ready` hazırlıktır, oturumsuzdur ve veritabanına erişilemezse `503` döner.

## 3. İstek ve yanıt

Sayfalar varsayılan olarak oturum ister. Controller ve action `[HasPermission]` ile korunur. POST istekleri antiforgery doğrular. AJAX, `RequestVerificationToken` başlığını gönderir.

JSON gövdesi:

```json
{ "isSuccess": true, "message": "", "data": {}, "errors": {} }
```

Başarısızlıkta HTTP kodu sonuca göredir (400, 401, 403, 404, 409, 429, 500). İş kuralı controller’da durmaz.

`Proxy:TrustForwardedHeaders` açıkken `X-Forwarded-For` / `X-Forwarded-Proto` yalnızca `Proxy:KnownProxies` (IP listesi) veya `Proxy:KnownNetworks` (CIDR listesi) içindeki vekilden gelirse işlenir. Listede olmayan vekilin başlıkları yok sayılır; bu durumda IP bazlı hız sınırları ve audit IP'si vekilin adresini görür.

Hassas uçlar hız sınırına tabidir. Örnekler: giriş ve bağlantı testi, Docker aksiyonu dakika başına 30, Dokploy ve Dokku dakika başına 20, agent raporu IP başına 120. Pencere kullanıcıya, oturum yoksa IP’ye göredir.

## 4. Kimlik ve yetki

İzinler `AspNetRoleClaims` tablosunda `permission` claim’i olarak durur. Bir kullanıcının birden fazla rolü olabilir; izinler rollerin birleşimidir. SuperAdmin `IsInRole` ile her izni geçer ve değiştirilemez. `plugin.manage` başka role verilmez; `user.manage` varsayılan olarak yalnızca SuperAdmin'dedir, `roles.manage` SuperAdmin ve Admin'dedir.

**Seed ve bilinen izinler.** Seeder (`IdentitySeeder.SeedAsync`, açılışta çekirdek matris, eklenti kurulum/yükseltmede eklentinin `IPermissionProvider` önerileri) her rol için daha önce önerdiği izinleri `RoleKnownPermissions (RoleId, Permission)` tablosunda tutar (M029). Hesap `PermissionSeedPlanner`'dadır: bilinen kümede olmayan varsayılan izin role eklenir ve bilinen olarak işaretlenir; bilinen ama rolde olmayan izin (yönetici bilerek kaldırmıştır) geri eklenmez. Böylece yükseltmede yalnızca YENİ izinler eklenir. SuperAdmin istisnadır: eksik her izni alır. M029 yükseltmede rollerde o an bulunan izinleri bilinen sayar; bu sürümde gelen `roles.manage` Admin'e bir kez eklenir. **Varsayılana döndür** (`IRoleManagementService.ResetToDefaultAsync`) rolü `IPermissionCatalog.DefaultsFor` kümesine (çekirdek matris + eklenti önerileri) eşitler.

**Rol yönetimi** (`RolesController`, `RoleManagementService`, `roles.manage`). Özel rol adı en çok 64 karakter (harf, rakam, boşluk, `.`, `-`, `_`); yerleşik rol adı değişmez ve silinemez. Kurallar:

- Yetki yükseltme yok: SuperAdmin olmayan bir rol yöneticisi role yalnızca kendisinde olan izinleri ekleyebilir (`RoleLockoutGuard.NotGrantable`).
- Kilitlenme koruması: `user.manage` veya `roles.manage` değişiklikten önce en az bir aktif (pasif / kilitli olmayan) kullanıcıda varken sonra hiçbirinde kalmayacaksa rol düzenleme, varsayılana döndürme, rol silme ve kullanıcının rollerini / aktifliğini değiştirme reddedilir (`RoleLockoutGuard.LostPermissions`). Kendi rolünden kendi yönetim yetkisini kaldıran düzenleme `ConfirmSelfLockout=true` ile onaylanmadan uygulanmaz.
- Kullanıcısı olan özel rol, kullanıcıları başka role taşınmadan silinemez (taşıma `user.manage` ister; SuperAdmin'e taşıma SuperAdmin ister). SuperAdmin rolünü yalnızca SuperAdmin atar.
- Rolden izin kaldırılınca rol üyelerinin security stamp'i yenilenir (oturum en geç `SecurityStampValidator` aralığında, 1 dk, düşer) ve `IUserSessionRevoker` açık terminalleri kapatır. Yalnızca ekleme yapıldıysa stamp değişmez; claim'ler stamp doğrulamasında (≤ 1 dk) yeniden üretilir. Yeniden adlandırmada stamp yenilenir.
- Audit: `role.create`, `role.update`, `role.permissions_change` (eklenen / kaldırılan izinler), `role.delete`, `role.assign` (kullanıcı rolleri değişince).

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

Data Protection anahtarları (kimlik çerezi, antiforgery, 2FA ara adımı) veritabanında saklanır; container yeniden oluşturulunca veya birden fazla örnek çalışınca oturumlar düşmez. Bu anahtarlar `Security:MasterKey`'den bağımsızdır.

Agent token’ı `sma_` artı 43 URL-güvenli karakterdir. Yalnızca oluşturulduğu anda, kurulum komutunun içinde bir kez gösterilir. Veritabanında SHA-256 özeti durur.

### API anahtarları ve REST API v1

Kişisel API anahtarı `smk_<önek>_<gizli>` biçimindedir: önek 12 küçük harf/rakam (benzersiz indeks, kaydı bulmak için), gizli kısım 43 harf/rakam (~256 bit). `ApiKeys` tablosunda (M029) yalnızca önek ve tam anahtarın SHA-256 özeti durur; doğrulama `CryptographicOperations.FixedTimeEquals` ile yapılır (`ApiKeyToken`). Anahtar yalnızca oluşturma yanıtında bir kez döner.

- **Kapsam:** oluştururken kullanıcının o anki izinlerinin alt kümesi. İstek anında etkin izinler = kapsam ∩ kullanıcının o anki izinleri (`ApiKeyRules.EffectivePermissions`; SuperAdmin için tüm tanımlı izinler). Oluşan kimlikte rol claim'i yoktur, bu yüzden SuperAdmin atlaması kapsamı aşamaz.
- **Reddedilen:** biçimi bozuk, özeti tutmayan, iptal edilmiş, süresi dolmuş, IP izin listesinde olmayan (CIDR; IPv4-mapped IPv6 normalize edilir) anahtar ve sahibi pasif / kilitli olan anahtar → 401.
- **Son kullanım:** `LastUsedAt` / `LastUsedIp` en sık dakikada bir (veya IP değişince) yazılır. `api_key.use` audit kaydı anahtar başına saatte en çok bir kez veya kaynak IP değişince yazılır; audit log her istekle dolmaz. Anahtarla yapılan değişiklikler (deploy, yedek, servis başlatma …) ilgili servislerin kendi audit kayıtlarına anahtar sahibinin adı ve IP'siyle düşer. Ayrıca `api_key.create` ve `api_key.revoke`.
- **Kimlik doğrulama:** `ApiKeyAuthenticationHandler` (şema `ApiKey`), `Authorization: Bearer smk_…`. `/api/v1` denetleyicileri (`ApiV1ControllerBase`) `[Authorize(AuthenticationSchemes = "ApiKey")]` taşır; policy değerlendirmesi `HttpContext.User`'ı yalnızca bu şemanın sonucuyla değiştirir, bu yüzden çerez oturumu /api/v1'e erişemez (401). Tarayıcı oturumu kabul edilmediği için bu uçlar `[IgnoreAntiforgeryToken]`'dır. `[HasPermission]` panelle aynıdır. 2FA zorunluluğu /api/v1'e uygulanmaz (anahtar kendi kimlik bilgisidir). Tek istisna OpenAPI belgesi `/api/v1/openapi.json`: salt okunur GET olduğu için `ApiKeyOrCookie` politika şemasıyla anahtar veya panel oturumu kabul eder; oturum yoksa 401.
- **Hız sınırı:** `api-v1` politikası, anahtarın özeti (yoksa IP) başına dakikada `ApiKeys:RequestsPerMinute` (varsayılan 120) istek. Hız sınırı kimlik doğrulamadan önce çalıştığı için bölüm anahtar özetiyle seçilir.
- **Yanıtlar:** JSON camelCase; enum'lar metin. Listeler `{ items, page, pageSize, totalCount, totalPages }` (sayfa boyutu en çok 100). Hatalar `application/problem+json` (ProblemDetails; doğrulamada `errors`). /api/v1 altında status code pages kapalıdır; gövdesiz 404/405 ProblemDetails'e çevrilir. Arka plan işleri (deploy, restart, yedek) 202 + `statusUrl` döner.
- **OpenAPI:** `Microsoft.AspNetCore.OpenApi`, belge adı `v1`, yalnızca `api/v1/` yolları; güvenlik şeması `ApiKey` (HTTP bearer).

| Yapılandırma | Varsayılan | Açıklama |
| --- | --- | --- |
| `ApiKeys:Enabled` | `true` | `false` ise anahtar oluşturulamaz, /api/v1 401 döner |
| `ApiKeys:MaxLifetimeDays` | `365` | En uzun geçerlilik; seçenekler 30/90/365 bununla sınırlanır |
| `ApiKeys:AllowNoExpiry` | `false` | Süresiz anahtara izin |
| `ApiKeys:RequestsPerMinute` | `120` | Anahtar başına dakikalık istek sınırı |
| `ApiKeys:MaxKeysPerUser` | `20` | Kullanıcı başına etkin anahtar sayısı |

Uçlar: `GET /me`; `GET /servers`, `/servers/{id}`, `/servers/{id}/status` (`server.view`); `GET /projects`, `/projects/{id}/deployments` (`deployment.view`); `POST /projects/{id}/deployments`, `/projects/{id}/restart` (`deployment.execute`); `GET /deployments/{id}`, `/deployments/{id}/log?tail=` (`deployment.view`); `GET /services`, `/services/{id}`, `/services/{id}/status` (`services.view`); `POST /services/{id}/start|stop|restart` (`services.manage`); `GET /backups/jobs`, `/backups/runs`, `/backups/runs/{id}` (`backup.view`); `POST /backups/jobs/{id}/run` (`backup.execute`); `GET /backups/runs/{id}/download` (`backup.download`, `IBackupDownloadService` akışı); `GET /alerts?status=firing|resolved|all` (`alert.view`); `POST /alerts/{id}/acknowledge` (`alert.acknowledge`). Hepsi `/api/v1` önekiyle.

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

### Kaynak Kullanımı ve Temizlik

İkisi de agentless sunucu sekmesidir (`ServerResourcesController`, `ServerCleanupController`); veri remote panel ile AJAX'la gelir. Anlık görünüm migration gerektirmez; kaynak geçmişi tabloları M028 ile gelir.

- **Kaynak Kullanımı** (`system.view`): `IResourceUsageService` → `SshResourceUsageInspector` tek bağlantıda `ResourceCommands.Snapshot` (nproc, `/proc/loadavg`, `/proc/meminfo`, `vmstat 1 2` ya da `/proc/stat` farkı, swap doluysa `VmSwap`, son 24 saat `journalctl -k` ya da `dmesg` OOM satırları, varsa `pidstat -d 1 1`), mevcut `ServerSystemCommands.Processes/Storage` ve `docker.view` varsa `docker stats --no-stream` + `docker ps -a` etiketlerini okur. Betik sudo açıksa önce sudo ile, eksik çıktıda normal kullanıcıyla çalışır. Container eşlemesi `PanelOwnership` ile yapılır. Bulgular `ResourceHeuristics.Evaluate`. Disk taraması `ResourceCommands.DiskScan`: `timeout 60 nice -n 19 ionice -c3` ile `du -x -k -d 2` (GNU'da `--threshold=10M`) ve `find -xdev -type f -size +102400k -exec stat`; yol `ResourceRules.IsValidScanPath` ile doğrulanır, `ShellQuote` ile kaçışlanır; timeout kodu 124 (GNU) / 143 (BusyBox) eksik sonuç sayılır.
- **Kaynak geçmişi** (M028): `ResourceHistoryWorker` ana metrik toplayıcıdan ayrı, `Monitoring:ResourceHistoryIntervalMinutes` (5; 0 kapatır, `Monitoring:Enabled` de gerekir) aralıkla izlenen, host key'i doğrulanmış ve çevrimdışı olmayan sunuculardan en fazla 4 eşzamanlı SSH ile örnek alır. `SshResourceHistoryInspector`: `ResourceHistoryCommands.Processes` (yetkisiz; `getconf CLK_TCK`, `/proc/stat` ve tüm `/proc/[pid]/stat` iki saniye arayla iki kez → utime+stime farkıyla gerçek process CPU'su ve sunucu meşguliyeti; `ps -eo pid,user,pcpu,pmem,rss,args` kullanıcı/bellek/komut için, `/proc` yoksa ps'in ömür boyu ortalaması) ve `ResourceHistoryCommands.Docker` (sudo; `docker inspect --format '{{.Name}}|{{.RestartCount}}|{{.State.Status}}|{{.State.Health.Status}}'` tüm container'lar, `docker stats --no-stream`). Ayrıştırma `ResourceHistoryParser`. `ContainerMetricSamples` satırı çalışan/yeniden başlayan container'lar, yeniden başlama sayısı > 0 olanlar ve yönetilen servis container'ları için yazılır; `ProcessSnapshots` satırı CPU'ya göre ilk 10 ∪ RSS'e göre ilk 10 process'i kısa JSON (`p,u,c,m,r,n,a`) olarak tutar. Her turdan sonra `ContainerMetricsHourly` özetlenir (son 7 gün, zaten özetlenen saat atlanır). Sorgu `GET /ServerResources/History/{id}?range=1h|6h|24h|7d` veya `from`/`to` (panel saat diliminde `yyyy-MM-ddTHH:mm`, en fazla 31 gün); 48 saatten uzun aralık saatlik özetten çizilir, ~120 kova. `GET /ServerResources/ProcessSnapshot/{id}?at=<UTC ISO>` en yakın anlık görüntüyü döner. İkisi de `system.view`. Saklama `Retention:ContainerMetricDays` (7), `ContainerMetricHourlyDays` (90), `ProcessSnapshotDays` (7); `RetentionAllowList` hedefleri `ContainerMetrics`, `ContainerMetricsHourly`, `ProcessSnapshots` (bölüm `ServerId`, en yeni satır korunur), silme `RetentionDeleter` ile (önce parametresiz komutla `#RetentionKeep`, sonra doldurma).
- **Temizlenebilir alan taraması:** aynı worker `Monitoring:ReclaimableScanIntervalHours` (6; 0 kapatır) aralıkla `IServerCleanupService.ScanAsync` çağırır (silme yok) ve `ServerReclaimableSpace` (sunucu başına tek satır, `MERGE`) tablosuna toplam ve güvenli silinebilir baytı yazar. Başarısız tarama bellekte işaretlenir, aralık dolmadan tekrarlanmaz.
- **Temizlik** (`server.cleanup`): `IServerCleanupService` → `SshServerCleanupInspector`. Tarama `docker system df -v --format '{{json .}}'`, `docker network ls --filter dangling=true` ve `CleanupCommands.Scan` (paket önbelleği, `journalctl --disk-usage`, `/var/log` ve `/tmp` için `find … -exec stat | sort | awk` toplamı, `snap list --all`, `dpkg-query` / `rpm -q kernel`, `uname -r`). Liste, seviye ve ön seçim saf `CleanupClassifier.Classify` kurallarıdır: korunan ağlar (`sm-proxy`, `sm-services`, `bridge`, `host`, `none`, `docker_gwbridge`, `ingress`) listelenmez, volume'lar önceden seçilmez, panele ait kaynaklar ve `sm-<slug>` geri dönüş imajları Dikkat'tir, çekirdekler silinemez.
- `POST /ServerCleanup/Execute/{id}` (`keys`, `logDays`, `tempDays`, `journalMaxMegabytes`, `dryRun`; antiforgery, `system-action` hız sınırı). Sunucu yeniden taranır, yalnızca taramadaki silinebilir anahtarlar işlenir. Docker adımları `DockerCommands` kurucularıyla, sistem adımları `CleanupCommands` ile kurulur; değerler `ShellQuote.Quote` ile kaçışlanır. Dosya silme taramadaki `find` ifadesine `-delete -print` ekler; önizlemede yıkıcı komut kurulmaz. Kazanç `df -PkT` aygıt başına boş alan farkıdır. Yanıt `application/x-ndjson` (`log` satırları, son satır `done`); istemci ayrılsa da işlem tamamlanır. Gerçek çalıştırma `server.cleanup` audit kaydı üretir.

## 8. Yedekleme

SSH stdout’u panele akar, sunucuda geçici arşiv oluşmaz. Panel akışı depolamaya yazar.

| Tür | Komut | Yetki |
| --- | --- | --- |
| Dosya | `tar -czf -` | İlgili yolları okuyacak sudo veya root |
| Volume | `alpine:3` container, volume salt okunur, `--network none` | docker grubu veya yalnızca docker için sudo |
| Veritabanı, container | `docker exec` içinde `pg_dump`, `mariadb-dump`/`mysqldump`, `mongodump --archive`, `redis-cli BGSAVE` + RDB dosyası veya `sqlcmd BACKUP DATABASE … COPY_ONLY`, sonra gzip | docker |
| Veritabanı, sunucu | Sunucudaki istemci ve gzip | İstemci kurulu olmalı |

PostgreSQL `--clean --if-exists --no-owner --no-privileges`. MySQL `--single-transaction --routines --triggers`. Parola stdin'in ilk satırından okunur, argv'ye girmez: PostgreSQL `PGPASSWORD`, MySQL `MYSQL_PWD`, Redis `REDISCLI_AUTH`, SQL Server `SQLCMDPASSWORD`; MongoDB araçları parolayı ortamdan okumadığı için `mktemp -d` içinde 0600 YAML dosyası + `--config` (Database Tools 100.3+, dosya EXIT tuzağıyla silinir).

Motor kuralları `BackupDatabaseEngines` içindedir (zorunlu alanlar, varsayılan port/kullanıcı, uzantı, geri yükleme desteği). Uzantılar: `.sql.gz`, MongoDB `.archive.gz`, Redis `.rdb.gz`, SQL Server `.bak.gz`. Redis ve SQL Server dosyası veritabanı sunucusunun diskinde oluştuğundan uzak adres reddedilir. SQL Server geçici .bak dosyasını `/var/opt/mssql/backup` altına yazar ve siler; geri yüklemede `SINGLE_USER WITH ROLLBACK IMMEDIATE` → `RESTORE … WITH REPLACE` → her durumda `MULTI_USER`; farklı adda `RESTORE FILELISTONLY` ile `MOVE` üretilir. MongoDB geri yüklemesi isteğe bağlı `--drop`, farklı adda `--nsFrom/--nsTo`. Redis geri yüklemesi panelde yoktur; formda `BackupDatabaseEngines.RedisManualRestoreGuidance` gösterilir.

Diğer modüller container veritabanı için iş açarken `IBackupJobService.CreateForContainerDatabaseAsync(ContainerDatabaseBackupRequest)` kullanır; istek `BackupJobSpecs.ForContainerDatabase` ile forma çevrilip `CreateAsync` yolundan geçer. MongoDB kimlik doğrulama veritabanı `BackupJobs.DatabaseAuthSource` sütunundadır (M025).

Nesne adı `{önek}{iş}/{yyyyMMdd-HHmmss}-{kısa}.tar.gz` veya veritabanında `.sql.gz`. Şifreliyse sonuna `.smbk` eklenir. SMBK sürüm 1: 36 bayt başlık, PBKDF2-HMAC-SHA256, 1 MiB AES-256-GCM parçaları. Tur sayısı `Backup:KeyDerivationIterations`. Her çalıştırma parola kopyasını kendinde tutar. İşin parolası değişse de eski dosya kendi kopyasıyla açılır.

Saatler `Backup:TimeZone` yerel saatine göredir. Kaçırılan çalışma açılışta bir kez telafi edilir. Aynı işte tek işlem. Toplam eşzamanlılık `Backup:MaxConcurrency`. İptal yarım nesneyi siler. S3’te yarım multipart yükleme iptal edilir. Azure’da başarısız yüklemeden sonra blob silinmeye çalışılır. Kapanışta süren iş “kesildi” olur.

Saklama, yeni yedek başarıldıktan sonra yalnızca o işin başarılı dosyalarını siler. Son N (1–365) ve isteğe bağlı N günden eski. En yeni başarılı dosya silinmez. Çalışma satırı, log ve audit kalır.

İş ve depolama yumuşak silinir. `GetDetails` silinmiş işi de okur. Düzenleme, silme ve yeni çalıştırma kapalıdır. Geçmiş indirme ve geri yükleme açıktır.

Sağlayıcılar `IBackupStorageProvider` uygular. Çekirdek yerel diski, `Storage.S3` ve `Storage.AzureBlob` eklentileri nesne deposunu getirir. Kayıtlı `ProviderSystemName` değiştirilmez. Eklenti kapalıysa o hedef kullanılamaz, kayıt durur.

## 9. Dağıtım ve GitHub

Proje çekirdektedir. Dokploy veya Dokku dağıtımının yerine geçmez. Git, hedef sunucuda SSH ile çalışır. Erişim anahtarı `x-access-token` olarak stdin’den gider, diske ve loga yazılmaz.

Domain kaydı `DeploymentDomains` tablosundadır. Aynı sunucuda aynı host ve yol iki kez kullanılamaz. Sunucuda bir kez `sm-traefik` ve `sm-proxy` ağı kurulur. Compose dosyası değiştirilmez; yanına `sm-proxy.override.yml` yazılır. Dockerfile container'ına etiket `docker run` ile eklenir. Özel sertifika ve anahtar şifreli saklanır, komut satırına yazılmaz, `/var/lib/sm-traefik/dynamic` altına stdin ile gider. Domain'i olmayan proje eskisi gibi yalnızca port veya compose ile ayağa kalkar. Proje silinince kayıt panelden kalkar; onay kutusundaki kalıcı silme sunucudaki klasörü, container'ı, imajı, volume'ları ve bu projenin vekil dosyalarını da kaldırır. `sm-traefik` durmaz. Deployment geçmişi silinmez. `Deployment:AcmeEmail` Let's Encrypt hesabıdır; varsayılanı boştur ve yapılandırılmadan vekil kurulmaz.

Ortam değişkenleri projede tek şifreli `.env` metni olarak durur (`DeploymentProjects.EncryptedEnvironment`) ve satır bazında düzenlenir; yorum ve sıra korunur. Okuma `deployment.view` (yalnızca anahtarlar), yazma `deployment.manage`, değer görme ve `.env` indirme `deployment.secrets` ister ve audit'e anahtar adıyla yazılır. Compose `.env`'yi container'a aktarmadığı için `docker compose config --services` ile servisler okunur ve override her servise `env_file` ekler; Dockerfile container'ı `--env-file` ile başlar. Diğer modüller `IProjectEnvironmentService.UpsertEnvironmentVariablesAsync` / `RemoveEnvironmentVariablesAsync` kullanır. **Uygula / Yeniden başlat** (`IDeploymentService.BeginRestartAsync`, `Deployment.Kind = Restart`) kaynak kod ve build olmadan `.env` ve override'ı yazıp container'ları yeniden oluşturur.

Çalışma logları sekmesi projenin container'larını (`com.docker.compose.project=sm-<proje>` veya `sm-<proje>`) `docker logs` ile okur. Push webhook'u `POST /api/webhooks/projects/{id}`: GitHub `X-Hub-Signature-256` HMAC veya GitLab `X-Gitlab-Token`, sabit zamanlı karşılaştırma, anonim, IP başına dakikada 30 istek; süren deployment varken (bu örnekte veya veritabanında, ör. başka örnek) proje başına tek takip deploy'u kalıcı kuyruğa alınır: `DeploymentProjects.PendingWebhookDeployAt/PendingWebhookCommit/PendingWebhookIpAddress` (M028). `DeploymentManager` süren deployment bitince projenin kaydını tüketir; `DeploymentLifecycleWorker` açılışta önce yarım kalan deployment'ları "kesildi" yapar, sonra kuyruğu ve ardından dakikada bir yoklar. Başlatma çakışırsa kayıt kalır; başarılı başlatma veya kalıcı hatada kayıt yalnızca okunduğu zamana kadar (`PendingWebhookDeployAt <= okunan`) silinir, böylece arada gelen push kaybolmaz. GitHub'da *Settings → Webhooks* içinde içerik türü `application/json`, gizli anahtar paneldeki değer, olay "Just the push event"; GitLab'da *Settings → Webhooks* içinde URL, "Secret token" ve "Push events". Panel ters vekil arkasındaysa adres `Deployment:PublicBaseUrl` ile üretilir. Geri dönüş (`Deployment.Kind = Rollback`) Dockerfile'da `sm-<proje>:<kısa-sha>` imajı duruyorsa build'i atlar; `Deployment:KeepImageCount` (5) başarılı deploy sonrası saklanan commit imajı sayısıdır, `latest` ve çalışan imaj silinmez.

Projeye bağlı yönetilen servis varsa (`ProjectServiceLinks`, M026) plan `JoinServicesNetwork` taşır: override tüm servislere `default`, (rota varsa) `sm-proxy` ve `sm-services` ağlarını yazar ve `sm-services`'i dış ağ tanımlar; Dockerfile container'ı rota yoksa `--network sm-services` ile başlar, rota varsa `sm-proxy` ile başlayıp `docker network connect sm-services` ile eklenir. Ağ yoksa `docker network create --label sm.managed=true sm-services` ile oluşturulur (idempotent).

GitHub App eklentisi `contents: read` ve `metadata: read` ister. Private key ile kısa ömürlü JWT, oradan kurulum anahtarı üretilir. Anahtar önbelleğe alınmaz ve tek depoya daraltılır. Private key, client secret ve webhook secret şifrelidir. Uygulamayı kullanan proje varken kayıt kaldırılamaz. Kaldırma yumuşak siler, GitHub’daki uygulamayı silmez.

`GitHub:PublicBaseUrl` boşsa isteğin adresi dönüş adresi olur. Ters vekil varsa doldurulmalıdır. Manifest ve kurulum dönüşü, Strict çerez yüzünden ara sayfa kullanır.

## 10. Servisler

Tek tıkla Docker servisleri çekirdektedir: `ManagedServices` ve `ManagedServiceOperations` tabloları (M023), şablon kataloğu `ServiceTemplates` (PostgreSQL, MySQL, MariaDB, Redis, MongoDB, SQL Server; MinIO, RabbitMQ, Adminer, pgAdmin, Uptime Kuma, n8n). Kurulum, yeniden oluşturma, yükseltme ve kaldırma `Begin*` ile kayda alınır, `ManagedServiceManager` arka planda `RunOperationAsync` ile yürütür ve çıktıyı `/hubs/services` ile yayınlar. Aynı serviste aynı anda tek işlem çalışır; uygulama kapanırken süren işlem "kesildi" olur.

Container `sm-svc-<kısa-ad>`, volume `sm-svc-<kısa-ad>-data`. Kimlik bilgileri ve ek değişkenler master key ile şifrelidir; sunucuda `/var/lib/sm-services/<kısa-ad>/.env` (klasör 700, `umask 077`) stdin ile yazılıp `--env-file` ile verilir. Sağlık, bağlantı testi ve konsol komutları parolayı container ortamından okur; argv'ye ve loglara parola girmez, işlem çıktısı maskelenir. Portlar varsayılan `127.0.0.1`'e, "Dışarıya aç" ile `0.0.0.0`'a bağlanır. Docker yayınlanan portlarda UFW'yi atladığı için IP izin listesi `DOCKER-USER` zincirine servis etiketli kurallar olarak yazılır (`--ctstate DNAT --ctorigdstport`, önce DROP, üstte izinli kaynaklar için RETURN) ve `sm-services-firewall.service` systemd birimiyle açılışta yeniden uygulanır. Kural yazılamazsa kurulum başarısız sayılır; port açık kalmaz.

Entegrasyon `IManagedServiceService.GetConnectionInfoAsync` ile yapılır (iç ağ adresi, kimlik bilgileri, önerilen ortam değişkenleri ve parolası maskeli önizlemeleri, ağlar). "Projeye bağla" `IProjectServiceLinkService` üzerinden önerilen değişkenleri (kullanıcının seçtiği ve yeniden adlandırdığı anahtarlarla) proje `.env` kaydına yazar ve bağı kaydeder; değerler tarayıcıya gitmez. `services.manage` + `deployment.manage` ister, audit `project.service_link/service_unlink`. Bağ kaldırılırken yalnızca istenirse bağla yazılan anahtarlar silinir.

Veritabanı servislerinde otomatik yedek `IManagedServiceBackupService` ile açılır: şablon → motor eşlemesi `ManagedServiceBackups` (postgres → PostgreSql, mysql/mariadb → MySql (root), mongodb → MongoDb (tüm veritabanları), redis → Redis, mssql → SqlServer (sa, ad formdan)). Sihirbazda seçilirse ayarlar kurulumdan önce doğrulanır ve işlem başlamadan `IServiceAutoBackupQueue.EnqueueAsync` ile `ManagedServiceOperations.PendingAutoBackup` kolonuna (M028) master key ile şifreli JSON olarak (parola dahil) yazılır. İş yalnızca kurulum başarılı bitince, başlatan kullanıcı adına (`CurrentUserService.RunAs`) `CreateForContainerDatabaseAsync` ile oluşturulur; istek `ClaimPendingAutoBackupAsync` ile okunup aynı anda temizlenir (iki kez işlenmez). Kurulum başarısız/kesildiyse istek düşürülür. `ManagedServiceLifecycleWorker` açılışta yarım kalan işlemleri "kesildi" yaptıktan sonra bitmiş ama isteği işlenmemiş kurulumları işler. Servise ait işler sunucu + container adıyla eşlenir. `backup.manage` ister.

Yapılandırma `ManagedServices:` altındadır: `PullTimeoutMinutes` (20), `CommandTimeoutSeconds` (120), `HealthTimeoutSeconds` (180), `MaxStoredLogKilobytes` (256), `AllowPrivilegedHostPorts` (false), `DefaultLogTail` (200). İşlem logları `Retention:ServiceOperationLogDays` (90) ile temizlenir.

## 11. Alarm, uptime, SSL, güvenlik taraması

Değerlendirici `Alerting:EvaluationIntervalSeconds` (60) ile kural bakar. Türler doluluk (CPU, RAM, disk %), çevrimdışı, uptime, SSL bitişi, deployment/yedek başarısızlığı, kritik güvenlik bulgusu ve M028 ile gelen servis türlerini kapsar:

- **Servis çalışmıyor** (`ServiceDown = 10`): hedef `ManagedServiceId` (kural `AlertRules.ManagedServiceId` ile tek servise daraltılabilir, yoksa sunucu kapsamı). Son `ContainerMetricSamples` örneği `running` değilse veya health `unhealthy` ise ve ardışık "kapalı" örnekler `DurationMinutes` kadar sürüyorsa açılır. Örnek yoksa veya `max(15 dk, 3 × ResourceHistoryIntervalMinutes)`'tan eskiyse, servis kurulum/güncellemedeyse, sunucu bakım/çevrimdışıysa durum Unknown (açık alarm kapanmaz).
- **Container yeniden başlama döngüsü** (`ContainerRestartLoop = 11`): hedef `serverId:containerName`. Pencere (`DurationMinutes`, en az 5) öncesindeki son örnek taban alınarak `RestartCount`'un pozitif artışları toplanır (container yeniden oluşturulup sayaç sıfırlanırsa negatif sayılmaz); toplam ≥ `Threshold` ise açılır.
- **Temizlenebilir alan** (`ReclaimableSpace = 12`): hedef sunucu; `ServerReclaimableSpace.ReclaimableBytes` GB (1024³) cinsinden ≥ `Threshold` ise açılır; hiç taranmadıysa Unknown.

Hepsi `AlertReconciler` histerezisini (2 ardışık OK) ve kanal/önem/hatırlatma kurallarını kullanır, mesaj `AlertMessageBuilder` ile kurulur. Servis sayfası `IAlertService.GetOpenServiceAlertsAsync` ile servise ait açık alarmları (`alert.view`) gösterir. Bildirim e-posta, Telegram ve Discord eklentileriyle gider. Teslim kaydı saklama süresi dolunca silinebilir. Kural ve kanal yumuşak silinir.

Uptime ve SSL kendi aralıklarında çalışır. Sonuç satırları yaşa göre temizlenebilir. Kontrolün kendisi ve audit kalır.

Güvenlik taraması SSH ile `sshd -T` veya `sshd_config`, port, güvenlik duvarı ve bekleyen güncelleme okur. Sunucuyu değiştirmez. Parmak izi yoksa tarama açılmaz.

## 12. Bulut ve maliyet

`Cloud.Hetzner`, `Cloud.DigitalOcean`, `Cloud.Vultr`, `Cloud.Linode` ve `Cloud.Scaleway` eklentidir. API anahtarı kayıttan önce doğrulanır. İstemci yönlendirme izlemez. 401, 403 ve 429 Türkçe mesaja çevrilir. Scaleway anahtarı `X-Auth-Token` başlığıyla gider; proje kimliği `account/v3/projects` içinden, adı `default` olan proje tercih edilerek okunur.

Eşitleme `Cloud:SyncIntervalHours` (6, `0` kapatır) veya elle çalışır. Aynı IP birden fazla panel kaydındaysa otomatik bağlanmaz. Hetzner fiyatı konumun KDV hariç aylık EUR tutarıdır. DigitalOcean, Vultr ve Linode aylık USD tutarıdır. Scaleway katalog saatlik EUR yayınlarsa aylık tutar saatlik fiyat × 730 olur. Fiyat dönmeyen sunucunun elle girilmiş maliyeti değişmez. Scaleway hesabın açmadığı bölge 400/404 döner ve atlanır.

Oluşturma onayı sunucu adını ister. SSH public key cloud-init içine eklenir. Hetzner’in tek seferlik root parolası, Vultr `default_password` alanı ve Linode’un üretilen root parolası yalnızca HTTP yanıtında gösterilir, veritabanı ve audit’e yazılmaz. Vultr `user_data` ve Linode `metadata.user_data` base64 gider. Scaleway cloud-init ayrı bir PATCH ile yazılır, sunucu `stopped` ise ardından `poweron` çağrılır. Linode etiketi harfle başlar, 3–64 karakterdir ve nokta, art arda tire veya alt çizgi kabul etmez.

Maliyet sayfası kur çevirmez. Para birimi yoksa ve tutar varsa USD sayılır. Grup silinince sunucu silinmez, gruptan çıkar.

## 13. Eklentiler

`plugin.json` alanları: `SystemName`, `FriendlyName`, `Group`, `Version`, `Author`, `Description`, `DisplayOrder`, `AssemblyFileName`, isteğe bağlı `Logo`. `SystemName` yalnızca harf, rakam ve noktadır, sonradan değişmez. Logo yalnızca `Content` altındaki düz dosya adıdır. `..` ve klasör ayracı reddedilir. Adres `/plugins/{systemname-küçük}/logo.svg` olur.

`IPluginStartup.ConfigureServices` eklenti kapalıyken de çalışır. Controller, hub ve sunucu sekmesi kapalı eklentide 404 veya gizli olur. Arka plan işi her turda `IPluginCatalog.IsEnabled` bakmalıdır.

Kurulum migration, izin ve `InstalledPlugins` satırı yazar. Devre dışı bırakmak tabloyu silmez. Uninstall yoktur. Yeni dll veya yeni klasör için süreç yeniden başlar. Kur, etkinleştir ve kapat yeniden başlatma istemez.

`Plugins:InstallOnStartup` varsayılanı boştur. Listeye yazılan eklenti hiç kurulmamışsa açılışta kurulur. Sonradan kapatılan eklenti bu liste yüzünden yeniden açılmaz.

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

Dokploy kurulum betiği `Dokploy:InstallScriptUrl` adresinden indirilir. `Dokploy:ExpectedSha256` doluysa betiğin SHA-256 özeti bu değerle eşleşmeden kurulum çalıştırılmaz.

Dokploy kurulum kaydı ve çıktısı eklenti tablosundadır. Sayfa kapansa da iş sürer, dönünce çıktı yeniden basılır. API anahtarı başka hosta yönlendirilmesin diye HTTP istemcisi yönlendirme izlemez.

Eklenti şeması yalnızca kendi migration’ındadır. Sürüm numarası tüm uygulamada benzersiz bir zaman damgasıdır. Host tablosuna yalnızca foreign key bağlanır. Host tablosunun kolonu eklentiyle değişmez. Şema değişikliği eklemedir: kolon silinmez, yeniden adlandırılmaz, tipi değişmez.

## 14. Veri kuralları

Silme yumuşaktır. Profil, maliyet, istatistik, satın alma ve audit otomatik işlerle silinmez. Okunamayan satır (sahibi veya tarihi yok) silinmez.

Silinebilen geçici veri ölçümü oluşturulma zamanına değil son harekete bakar ve pay bırakır. Uygulamanın gösterdiği son N kayıt kalır.

Geçmiş kayıtların saklama süresi `Retention:` anahtarlarıyla gün cinsinden verilir: `DeploymentLogDays`, `BackupRunLogDays`, `CommandRunDays`, `TerminalSessionDays`, `AlertEventDays`, `ServiceOperationLogDays` (Servisler işlem logları), `ContainerMetricDays` (7), `ContainerMetricHourlyDays` (90), `ProcessSnapshotDays` (7) (kaynak geçmişi). `0` süresiz saklar. Audit log hiçbir ayarla silinmez.

Eski istemciyle uyum: alan eklenir, silinmez. Yeni alan opsiyoneldir ve varsayılanla okunur. Sıkılaştırılmış kural, mağazadaki eski sürüm çoğunlukla güncellenmeden yüklenmez. Bu depoda mağaza istemcisi yoktur. Kural yine de API ve şema içindir.

Audit HMAC zinciriyle imzalanır ve panelden doğrulanabilir. Satır silinmez ve güncellenmez.

## 15. Arayüz

`ViewData["Page"]` layout’a `css/pages/{sayfa}.css` ve `js/pages/{sayfa}.js` ekletir. Eklenti view’ı ayrıca `ViewData[PluginContent.PagePluginKey]` atar. Dosya `/plugins/{systemname}/` altından gelir.

JavaScript yalnızca ES module’dür. Host modülü `@app/` import map’i ile sürüm parametreli adrese gider. Liste ve form AJAX’tır. Tehlikeli onayda ad yazılır. Her düğme ve bağlantıda `title` vardır.

Sayfa stili `npm run build:css` ile üretilir. Eklenti SCSS’i aynı komutla eklentinin `Content/css/pages` klasörüne çıkar. Derlenmiş CSS repoya dahildir.

## 16. Gözlemlenebilirlik ve sınırlar

Serilog konsol ve dosyaya yazar. İstek süresi ve sonuç kodu loglanır. Gizli değer ve terminal çıktısı loglanmaz.

Ayarlar sayfası (`settings.view`) sürüm, ortam, süreç süresi ve şema sürümünü okur. `settings.manage` izleme, alarm, yedek, tarama ve bulut aralıklarını `PanelSettings` tablosuna yazar ve aynı süreçteki seçenek nesnesine uygular. Agent aralığı, bağlantı dizesi, master key ve API anahtarı bu sayfadan değişmez.

Sunucu durumu Bakımda iken otomatik durum geçişi yapılmaz, ölçüm sürer. `MetricsMaintenanceWorker` ham ölçümü saatlik özete çevirir ve saklama süresini uygular. Aralık `Monitoring:MaintenanceIntervalMinutes` (10).

Bu dokümanın kapsamı çalışan modüllerdir. README’de hedef olarak yazılmış olup çekirdekte ayrı modülü olmayan işler (yapay zeka asistanı, genel port tarayıcı, webhook ürünü, Nginx yönetim ekranı) burada yok sayılır.
