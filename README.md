# Mag Server Manager
## Sunucu Yönetim, Docker, Dokploy ve Terminal Yönetim Sistemi

> Bu proje; birden fazla Linux sunucunun tek bir web panelinden güvenli şekilde tanımlanmasını, izlenmesini ve yönetilmesini amaçlayan profesyonel bir Server Management Platform'dur.

## Dokümanlar

| Doküman | Kim için | Ne anlatır |
| --- | --- | --- |
| [Son kullanıcı kılavuzu](docs/son-kullanici.md) | Paneli kullanan herkes | Ekranların ne işe yaradığı, günlük dilde. Kurulum ve teknik terim yok. |
| [Kullanma kılavuzu](docs/kullanim-kilavuzu.md) | Operatör ve yönetici | Menü menü nasıl yapılır: sunucu ekleme, izleme, Docker, yedek, alarm, dağıtım. |
| [Teknik doküman](docs/teknik-dokuman.md) | Geliştirici ve kurulum sorumlusu | Mimari, güvenlik, veri, eklentiler ve yapılandırma. |

Bu dosyanın 1–82. bölümleri ürün hedefi ve yol haritasıdır. Çalışan sistemin kurulumu ve ayrıntılı davranışı [Geliştirme Ortamı](#geliştirme-ortamı) bölümünden başlar; teknik doküman oradaki ayrıntıya bağlanır.

## Lisans

Telif hakkı Mehmet Ünal'a aittir. Yazılım [MIT Lisansı](LICENSE) ile açık kaynak olarak yayımlanır: kullanılabilir, değiştirilebilir ve dağıtılabilir; ticari ürünlerde de kullanılabilir. Kopyalarda telif ve izin bildirimi korunur.

---

## 1. Proje Amacı

Sistem yöneticisinin SSH ile tek tek sunuculara bağlanmak yerine;

- Sunucu eklemesi
- CPU / RAM / Disk / Network takibi
- Docker container yönetimi
- Dokploy kurulumu
- Terminal erişimi
- Dosya yönetimi
- Kullanıcı ve yetki yönetimi
- Log görüntüleme
- Servis yönetimi
- Backup / restore
- Alert ve bildirimler
- Sunucu sağlık kontrolü

gibi işlemleri merkezi bir panelden yapabilmesi hedeflenmektedir.

Sistem özellikle VPS, Dedicated Server, Cloud Server ve Docker tabanlı uygulama sunucularını yönetmek için tasarlanacaktır.

---

# 2. Ana Modüller

## 2.1 Dashboard

Ana ekran tüm sunucuların genel durumunu göstermelidir.

### Genel kartlar

- Toplam sunucu
- Online sunucu
- Offline sunucu
- Kritik durumdaki sunucu
- Toplam CPU kullanımı
- Toplam RAM kullanımı
- Toplam disk kullanımı
- Çalışan Docker container sayısı
- Duran container sayısı
- Hatalı container sayısı
- Aktif alarm sayısı

### Sunucu durumları

- 🟢 Healthy
- 🟡 Warning
- 🔴 Critical
- ⚫ Offline
- 🔵 Maintenance

### Dashboard grafikler

- CPU kullanım grafiği
- RAM kullanım grafiği
- Disk kullanım grafiği
- Network RX/TX
- Load Average
- Container sayısı
- Son sistem olayları
- Son deployment işlemleri

Dashboard gerçek zamanlı veya kısa polling aralıklarıyla güncellenebilmelidir.

---

# 3. Sunucu Yönetimi

## 3.1 Sunucu Ekleme

Panelden yeni sunucu eklenebilmelidir.

Alanlar:

- Sunucu adı
- Hostname
- IP adresi
- SSH Port
- Kullanıcı adı
- Authentication Type
- Root / Sudo bilgisi
- SSH Password veya SSH Private Key
- Sunucu açıklaması
- Etiketler
- Environment
- Lokasyon
- Provider
- İşletim sistemi

### Authentication

Desteklenmesi gereken yöntemler:

1. SSH Password
2. SSH Private Key
3. SSH Key + Passphrase
4. Sudo User

Root kullanımı mümkün olduğunca sınırlandırılmalı; normal kullanıcı + sudo desteklenmelidir.

---

# 4. Güvenlik

Sunucu şifreleri ve private key bilgileri plaintext olarak veritabanında tutulmamalıdır.

### Secret Management

- AES-256 veya güçlü envelope encryption
- Application master key
- Environment variable / secret store
- Maskeli gösterim
- Audit log
- Secret rotation
- Şifre dışa aktarma engeli
- Private key indirme engeli

### SSH Security

- Host key fingerprint doğrulama
- Known Hosts yönetimi
- SSH timeout
- Connection retry
- Brute-force koruması
- Session timeout
- Komut çalıştırma audit log'u

---

# 5. Sunucu Detay Sayfası

Her sunucunun kendine ait detay ekranı olacaktır.

## Sekmeler

- Overview
- Metrics
- Docker
- Terminal
- Files
- Services
- Processes
- Logs
- Network
- Storage
- Security
- Deployments
- Backups
- Alerts
- Activity

---

# 6. Sistem Kaynakları

Sunucudan aşağıdaki bilgiler alınmalıdır:

### CPU

- CPU kullanım %
- Core sayısı
- Thread sayısı
- Load Average
- CPU temperature varsa
- Top CPU processes

### RAM

- Total
- Used
- Free
- Cached
- Available
- Swap

### Disk

- Disk toplam alanı
- Kullanılan alan
- Boş alan
- Kullanım %
- Mount point
- Inode kullanımı

### Network

- RX
- TX
- Paket sayısı
- Network interface
- Public IP
- Private IP

### System

- OS
- Kernel
- Architecture
- Hostname
- Uptime
- Timezone
- Last reboot

---

# 7. Resource History

Sadece anlık değerler değil geçmiş değerler de tutulmalıdır.

Örneğin:

- Son 1 saat
- Son 6 saat
- Son 24 saat
- Son 7 gün
- Son 30 gün

Grafikler:

- CPU
- RAM
- Disk
- Network
- Load Average

Retention policy uygulanmalıdır.

Örneğin yüksek çözünürlüklü metrikler kısa süre tutulurken uzun dönem için aggregation kullanılmalıdır.

---

# 8. Docker Yönetimi

Docker ekranı projenin en önemli modüllerinden biri olacaktır.

## Docker Overview

Gösterilecek bilgiler:

- Docker Engine version
- Containers
- Running
- Stopped
- Restarting
- Failed
- Images
- Volumes
- Networks
- Disk usage

---

# 9. Container Listesi

Her container için:

- Container Name
- ID
- Image
- Status
- Health
- Ports
- CPU
- RAM
- Network RX/TX
- Created At
- Started At
- Restart Count

### Durumlar

- Running
- Exited
- Restarting
- Paused
- Dead
- Unknown

---

# 10. Container İşlemleri

Panel üzerinden:

- Start
- Stop
- Restart
- Pause
- Resume
- Kill
- Remove
- Rename
- Inspect
- Logs
- Stats

işlemleri yapılabilmelidir.

Tehlikeli işlemler için confirmation modal kullanılmalıdır.

Örneğin:

> Container'ı silmek üzeresiniz. Bu işlem geri alınamaz.

---

# 11. Container Terminal

Kullanıcı container içine terminal ile girebilmelidir.

Örneğin:

```bash
docker exec -it container_name /bin/sh
```

veya uygun shell otomatik tespit edilmelidir.

Web panelinde:

- Xterm.js
- WebSocket
- SSH/Docker exec session

kullanılabilir.

Terminal resize desteklemelidir.

---

# 12. Container Dosya Yönetimi

Container dosya sistemi panel üzerinden yönetilebilmelidir.

### Özellikler

- Dosya listeleme
- Klasör oluşturma
- Dosya oluşturma
- Dosya silme
- Rename
- Copy
- Move
- Upload
- Download
- Edit
- Permission değiştirme
- Owner değiştirme

### Editor

Web tabanlı kod editörü kullanılabilir.

Örneğin:

- Monaco Editor

Destek:

- JSON
- YAML
- XML
- HTML
- CSS
- JS
- TS
- C#
- ENV
- Markdown
- Nginx config
- Dockerfile
- Shell

---

# 13. Dosya Permission Yönetimi

Linux permission yönetimi desteklenmelidir.

Örnek:

```text
chmod 755
chmod 644
chown www-data:www-data
```

UI üzerinden:

- Owner
- Group
- Read
- Write
- Execute

seçilebilir.

Raw chmod/chown işlemleri de Advanced Mode içerisinde bulunabilir.

---

# 14. Docker Compose Yönetimi

Panelden Docker Compose projeleri yönetilebilmelidir.

### Özellikler

- Compose project listesi
- Compose YAML görüntüleme
- YAML editörü
- Validate
- Up
- Down
- Restart
- Pull
- Build
- Logs
- Recreate
- Environment Variables
- Volumes
- Networks

Örnek:

```bash
docker compose up -d
```

işlemi panel üzerinden gerçekleştirilebilir.

---

# 15. Docker Image Yönetimi

### Özellikler

- Image listesi
- Size
- Created
- Repository
- Tag
- Digest

İşlemler:

- Pull
- Remove
- Inspect
- Tag
- Prune

Private registry desteği eklenebilir.

---

# 16. Docker Volume Yönetimi

Gösterilecek:

- Volume name
- Driver
- Mountpoint
- Size
- Container bağlantıları

İşlemler:

- Create
- Inspect
- Remove
- Backup
- Restore

---

# 17. Docker Network Yönetimi

- Network listesi
- Driver
- Subnet
- Gateway
- Connected containers

İşlemler:

- Create
- Remove
- Inspect
- Connect
- Disconnect

---

# 18. Dokploy Yönetimi

Sunucuya panel üzerinden Dokploy kurulabilmelidir.

Kurulum öncesi:

- OS kontrolü
- Docker kontrolü
- RAM kontrolü
- Disk kontrolü
- Port kontrolü
- Root/Sudo kontrolü
- Internet bağlantısı kontrolü

### Installation Wizard

Adımlar:

1. Server seç
2. Compatibility check
3. Docker check
4. Port check
5. Installation confirmation
6. Installation
7. Health check
8. Complete

Kurulum çıktısı canlı terminal/log ekranında gösterilmelidir.

---

# 19. Dokploy Durumu

Sunucu Dokploy kullanıyorsa:

- Dokploy version
- Status
- URL
- Port
- Container status
- Last health check

gösterilmelidir.

Mümkün olan yerlerde Dokploy API entegrasyonu tercih edilmelidir.

---

# 20. Deployment Yönetimi

İleri aşamada panel deployment merkezi haline getirilebilir.

### Git

Destek:

- GitHub
- GitLab
- Bitbucket
- Self-hosted Git

### Deployment

- Repository
- Branch
- Commit
- Dockerfile
- Compose
- Environment
- Build command
- Deploy command

### Deployment history

- Started
- Building
- Deploying
- Success
- Failed
- Cancelled

---

# 21. Terminal Yönetimi

Sistemin merkezi terminal ekranı olmalıdır.

Terminal:

- SSH
- WebSocket
- Xterm.js

ile çalışabilir.

### Özellikler

- Full screen
- Copy / Paste
- Multiple tabs
- Terminal resize
- Command history
- Search
- Download output
- Session reconnect
- Session timeout

---

# 22. Terminal Güvenliği

Terminal en yüksek riskli modüllerden biridir.

### Kurallar

- Session authentication
- Role check
- Server permission check
- Session timeout
- Command audit
- IP audit
- User audit
- Dangerous command warning

İsteğe bağlı:

```text
rm -rf
mkfs
dd
shutdown
reboot
iptables
userdel
```

gibi komutlarda ikinci onay istenebilir.

> Güvenlik sistemi komutları tamamen engellemek yerine configurable policy yaklaşımını desteklemelidir.

---

# 23. Web SSH

Normal SSH bağlantısına ek olarak tarayıcı üzerinden terminal:

```text
Browser
   ↓
WebSocket
   ↓
Server Management API
   ↓
SSH Session
   ↓
Linux Server
```

şeklinde çalışmalıdır.

SSH private key backend tarafında güvenli secret store üzerinden kullanılmalıdır.

---

# 24. Process Yönetimi

Sunucudaki process'ler gösterilmelidir.

Alanlar:

- PID
- Process
- User
- CPU
- RAM
- Start Time
- Command

İşlemler:

- View
- Kill
- Restart

Root gerektiren işlemler permission kontrolünden geçmelidir.

---

# 25. Linux Service Yönetimi

Systemd servisleri listelenebilmelidir.

Örnek:

```text
nginx
docker
redis
postgresql
mssql-server
ssh
```

İşlemler:

- Start
- Stop
- Restart
- Enable
- Disable
- Status
- Logs

---

# 26. Log Yönetimi

Merkezi log ekranı:

- Docker logs
- System logs
- Application logs
- Nginx logs
- SSH logs
- Systemd logs

### Özellikler

- Live log
- Search
- Filter
- Level
- Time range
- Download
- Auto refresh

---

# 27. Nginx Yönetimi

İleri seviye modül:

- Site listesi
- Config
- Enable / Disable
- Test configuration
- Reload
- Restart
- SSL status

Örnek:

```bash
nginx -t
systemctl reload nginx
```

---

# 28. SSL / Domain Yönetimi

Sunucu üzerinde çalışan servislerin domainlerini takip edebilmelidir.

### Özellikler

- Domain
- IP
- SSL provider
- Expiration date
- Certificate status

### Alert

SSL bitimine:

- 30 gün
- 15 gün
- 7 gün
- 3 gün

kala bildirim gönderilebilir.

---

# 29. Backup Yönetimi

Sunucu ve Docker için backup sistemi:

### Backup türleri

- File backup
- Database backup
- Docker volume backup
- Configuration backup
- Full server snapshot integration

Desteklenebilecek storage:

- S3
- Cloudflare R2
- MinIO
- Backblaze B2
- Local storage

### Backup

- Manual
- Scheduled
- Retention
- Encryption
- Compression

---

# 30. Database Yönetimi

İsteğe bağlı DB modülü:

- SQL Server
- PostgreSQL
- MySQL
- MariaDB
- Redis

Gösterilecek:

- Connection status
- Database size
- Connections
- Health
- Version

Database işlemleri mümkün olduğunca güvenli, read-only ağırlıklı ve role-based yapılmalıdır.

---

# 31. Alert Sistemi

Kullanıcı kendi alarm kurallarını oluşturabilmelidir.

Örnek:

```text
CPU > 90% for 5 minutes
RAM > 90%
Disk > 85%
Disk > 95%
Server offline
Container stopped
Container restarting
SSL expires < 7 days
Backup failed
Deployment failed
Docker daemon stopped
```

---

# 32. Bildirim Kanalları

Desteklenebilir:

- Email
- Telegram
- Discord
- Slack
- Web notification

Notification policy:

```text
Warning
Critical
Recovery
```

Recovery notification da gönderilmelidir.

---

# 33. Uptime Monitoring

Sunucu ve servislerin erişilebilirliği takip edilmelidir.

Örneğin:

```text
Server
 ├── SSH
 ├── HTTP
 ├── HTTPS
 ├── Docker
 ├── Dokploy
 └── Custom Port
```

Her servis için:

- Uptime
- Response time
- Last check
- Failure count

tutulabilir.

---

# 34. Port Scanner / Port Management

Sunucunun açık portları görüntülenebilir.

Örneğin:

```text
22    SSH
80    HTTP
443   HTTPS
2375  Docker
3000  Application
```

> Docker remote API gibi yüksek riskli servisler varsayılan olarak güvenli olmayan şekilde expose edilmemelidir.

---

# 35. Security Center

Sunucunun güvenlik durumunu gösteren ayrı ekran.

Kontroller:

- SSH root login
- Password authentication
- Firewall
- Open ports
- Failed SSH attempts
- Docker exposure
- Outdated packages
- Disk encryption status
- User list
- Sudo users

Olası öneriler:

```text
SSH root login is enabled
Password authentication is enabled
Port 2375 is publicly accessible
Firewall is inactive
```

Sistem sadece tespit etmeli; otomatik düzeltmeler açık kullanıcı onayı olmadan uygulanmamalıdır.

---

# 36. User & Role Management

RBAC sistemi:

### Super Admin

Her şeye erişebilir.

### Admin

Sunucu ve deployment yönetebilir.

### Operator

Docker, logs, services ve terminal kullanabilir.

### Developer

Deployment, logs ve sınırlı terminal kullanabilir.

### Viewer

Sadece görüntüleme.

---

# 37. Permission Matrix

Yetkiler ayrı ayrı tanımlanmalıdır.

Örneğin:

```text
server.view
server.create
server.edit
server.delete
server.connect

terminal.view
terminal.execute

docker.view
docker.start
docker.stop
docker.restart
docker.delete

file.view
file.create
file.edit
file.delete
file.upload
file.download

deployment.view
deployment.create
deployment.execute

backup.view
backup.create
backup.restore
```

---

# 38. Audit Log

Yapılan bütün kritik işlemler kayıt altına alınmalıdır.

Örnek:

```text
User:
Mehmet

Action:
Docker Restart

Server:
Production-01

Container:
api

IP:
x.x.x.x

Date:
2026-10-03 20:15
```

Audit log silinmemeli veya yalnızca özel yetkili kullanıcı tarafından retention policy kapsamında yönetilebilmelidir.

---

# 39. Maintenance Mode

Sunucu bakım moduna alınabilir.

Maintenance sırasında:

- Alert susturma
- Deployment engelleme
- Kullanıcı bilgilendirme
- Bakım notu

özellikleri bulunmalıdır.

---

# 40. Server Tags

Sunucular etiketlenebilmelidir.

Örnek:

```text
production
staging
development
database
docker
web
api
eu
tr
```

Böylece filtreleme kolaylaşır.

---

# 41. Server Groups

Sunucular gruplandırılabilir.

Örneğin:

```text
Production
 ├── Web-01
 ├── Web-02
 ├── API-01
 └── DB-01

Staging
 ├── Stage-01
 └── Stage-02
```

Grup bazlı:

- Monitoring
- Alert
- Permission
- Deployment

uygulanabilir.

---

# 42. Server Templates

Yeni sunucular için template sistemi:

```text
Ubuntu Docker Server
Ubuntu Dokploy Server
Web Server
Database Server
CI/CD Server
```

Template üzerinden:

- Package installation
- Docker installation
- Firewall configuration
- Monitoring agent
- User creation

otomatikleştirilebilir.

---

# 43. Server Provisioning

İleri aşamada:

- Ubuntu kurulumu sonrası hazırlık
- Docker kurulumu
- Dokploy kurulumu
- Nginx kurulumu
- Firewall
- Monitoring agent
- SSH hardening

tek workflow ile uygulanabilir.

Bu yapı idempotent olmalıdır.

---

# 44. Command Library

Sık kullanılan komutlar kaydedilebilir.

Örnek:

```bash
docker ps
docker system df
df -h
free -h
uptime
systemctl status docker
```

Kullanıcı komut kütüphanesi oluşturabilir.

Komutlar:

- Global
- Server specific
- Role restricted

olabilir.

---

# 45. Scheduled Tasks

Sunucularda zamanlanmış işlemler:

- Backup
- Cleanup
- Docker prune
- Log cleanup
- Health check
- Custom script

olarak planlanabilir.

Cron yönetimi desteklenebilir.

---

# 46. Remote Script Runner

Bir veya birden fazla sunucuda script çalıştırma.

Örnek:

```text
Production Group
      ↓
Script
      ↓
Web-01
Web-02
API-01
API-02
```

Sonuçlar:

- Success
- Failed
- Duration
- Output
- Error

olarak tutulmalıdır.

> Toplu çalıştırma için güçlü confirmation ve role permission uygulanmalıdır.

---

# 47. Server Compare

İki veya daha fazla sunucu karşılaştırılabilir.

Karşılaştırma:

- CPU
- RAM
- Disk
- OS
- Kernel
- Docker
- Containers
- Uptime
- Network
- Open ports

---

# 48. Resource Cost Tracking

Cloud provider bilgisi eklenirse:

- Aylık tahmini maliyet
- Server cost
- Storage cost
- Backup cost
- Traffic cost

takip edilebilir.

Provider:

- AWS
- Azure
- Hetzner
- DigitalOcean
- Vultr
- Linode
- Scaleway
- Contabo
- OVH

gibi platformlara adapter mimarisi ile bağlanabilir.

---

# 49. Architecture

Önerilen teknoloji:

```text
Frontend
   ↓
ASP.NET Core MVC / Razor
   ↓
ASP.NET Core API
   ↓
Application
   ↓
Infrastructure
   ↓
SSH / Docker / Linux / Database
```

### Backend

- ASP.NET Core 10
- C#
- Clean Architecture
- MSSQL
- Entity Framework Core
- FluentValidation
- FluentMigrator
- ASP.NET Core Identity
- Serilog
- SignalR

### Frontend

- ASP.NET Core MVC / Razor
- TailwindCSS
- Alpine.js veya minimal JavaScript
- Monaco Editor
- xterm.js
- Chart.js / ECharts

### Real-time

SignalR:

```text
Server Metrics
Docker Logs
Terminal Events
Deployment Logs
Alerts
System Events
```

---

# 50. Önerilen Solution Yapısı

```text
src/
├── ServerManager.Domain/
│   ├── Entities/
│   ├── Enums/
│   ├── ValueObjects/
│   └── Interfaces/
│
├── ServerManager.Application/
│   ├── Services/
│   ├── DTOs/
│   ├── Validators/
│   ├── Interfaces/
│   └── Mappings/
│
├── ServerManager.Infrastructure/
│   ├── Persistence/
│   ├── Repositories/
│   ├── SSH/
│   ├── Docker/
│   ├── Monitoring/
│   ├── Notifications/
│   ├── Security/
│   └── BackgroundJobs/
│
├── ServerManager.Web.Framework/      # Host ile eklentilerin ortak web altyapısı
│   ├── Authorization/
│   ├── Mvc/
│   ├── Plugins/
│   ├── Servers/
│   └── UI/
│
├── ServerManager.Web/
│   ├── Controllers/
│   ├── Hubs/
│   ├── Views/
│   ├── Components/
│   ├── wwwroot/
│   └── Middleware/
│
└── Plugins/                          # nopCommerce tarzı eklentiler (Modül / Eklenti)
    ├── ServerManager.Plugin.DevOps.Dokploy/
    └── ServerManager.Plugin.DevOps.Dokku/
```

Repository pattern kullanılmalı; database erişimi repository katmanında tutulmalıdır.

Çekirdek (sunucu, izleme, Docker, terminal, dosya, kullanıcı, audit) host uygulamada kalır. Dokploy, Dokku, Coolify, CapRover, Portainer gibi üçüncü parti DevOps araçları ve isteğe bağlı entegrasyonlar **eklenti** olarak geliştirilir: her biri kendi projesinde, kendi entity, migration, servis, controller, view, JS/CSS, izin ve audit tanımlarıyla durur; host'a dokunmadan eklenir, kurulur, etkinleştirilir veya devre dışı bırakılır.

---

# 51. Provider Adapter Architecture

Server işlemleri doğrudan controller'a yazılmamalıdır.

Örneğin:

```text
IServerProvider
   ├── SshServerProvider
   ├── DockerProvider
   └── CloudProvider
```

Docker:

```text
IDockerProvider
```

gibi abstraction kullanılmalıdır.

DevOps araçlarının provider'ları kendi eklentisinin içinde tanımlanır ve host'un ortak SSH/Docker altyapısını (`IServerConnectionProvider`, `IRemoteCommandExecutor`, `ISecretProtector`) kullanır:

```text
Plugins/
 ├── DevOps.Dokploy   → IDokployProvider (SshDokployProvider), IDokployApiClient
 ├── DevOps.Dokku     → IDokkuProvider (SSH üzerinden dokku CLI)
 └── DevOps.Coolify   → ICoolifyProvider
```

Bu yapı ileride yeni provider eklemeyi kolaylaştırır; yeni araç için host kodu değişmez (bkz. [Eklenti geliştirme](#eklenti-geliştirme)).

---

# 52. Database Ana Tablolar

Önerilen tablolar:

```text
Servers
ServerCredentials
ServerTags
ServerGroups
ServerGroupMembers
ServerMetrics
ServerHealthChecks

DockerContainers
DockerImages
DockerVolumes
DockerNetworks
DockerProjects

Deployments
DeploymentLogs

TerminalSessions
TerminalCommandLogs

Files
FileOperationLogs

Services
Processes

Backups
BackupJobs

Alerts
AlertRules
Notifications

Users
Roles
Permissions
UserRoles
RolePermissions

AuditLogs
MaintenanceWindows

CommandLibrary
ScheduledTasks
ServerScripts

SslCertificates
Domains
Ports
```

Secret değerleri doğrudan `ServerCredentials` içerisinde plaintext olarak tutulmamalıdır.

---

# 53. Background Worker

Arka plan servisleri:

```text
MetricsCollector
HealthCheckWorker
DockerMonitor
AlertWorker
BackupWorker
CertificateMonitor
CleanupWorker
DeploymentWorker
```

Örneğin MetricsCollector her 15-30 saniyede sunucuları kontrol edebilir.

Yoğun sunucu sayısında adaptive polling ve job queue kullanılmalıdır.

---

# 54. Connection Architecture

Önerilen yapı:

```text
                 Server Manager
                       |
          +------------+------------+
          |            |            |
         SSH        Docker API    HTTPS/API
          |            |            |
       Linux        Docker       Dokploy
       Server       Engine       Instance
```

Mümkün olduğunca sunuculara inbound port açmak yerine outbound bağlantı veya SSH üzerinden yönetim tercih edilmelidir.

---

# 55. Agent Mimari Opsiyonu

İleride küçük bir Linux Agent geliştirilebilir.

```text
Server Manager
       ↓
     Agent
       ↓
Linux Server
```

Agent:

- Metrics
- Docker
- Logs
- Process
- Services
- File operations

sağlayabilir.

Agent bağlantısı:

- TLS
- API key
- Certificate
- Heartbeat

ile korunmalıdır.

Agent ilk sürüm için zorunlu değildir.

---

# 56. Agentless First

MVP'de agent kurmadan:

```text
SSH
Docker CLI
HTTP API
Dokploy API
```

üzerinden ilerlemek önerilir.

Bu sayede yeni sunucuya sadece SSH bilgileri verilerek sistem kullanılabilir.

---

# 57. UI/UX

Tasarım profesyonel bir DevOps/SaaS dashboard görünümünde olmalıdır.

### Sol Sidebar

```text
Dashboard

Infrastructure
 ├── Servers
 ├── Groups
 └── Providers

Containers
 ├── Docker
 ├── Images
 ├── Volumes
 └── Networks

Deployments
 ├── Projects
 ├── Deployments
 └── History

Operations
 ├── Terminal
 ├── Files
 ├── Services
 ├── Processes
 └── Logs

Monitoring
 ├── Metrics
 ├── Uptime
 ├── Alerts
 └── SSL

Backup
Security
Users
Audit Logs
Settings
```

---

# 58. Server Detail UI

Sunucu detayında üst bölüm:

```text
Production-01
Ubuntu 24.04
Online
IP: xxx.xxx.xxx.xxx
Uptime: 24d 13h

CPU  ███████░░░  71%
RAM  █████░░░░░  52%
DISK ████████░░  81%
```

Alt bölüm:

```text
CPU Chart
RAM Chart
Network Chart
Docker Status
Recent Logs
Recent Events
Alerts
```

---

# 59. Real-time UX

SignalR ile:

- CPU değişimi
- RAM değişimi
- Docker status
- Live terminal
- Live logs
- Deployment output

sayfa refresh edilmeden güncellenmelidir.

---

# 60. Dark / Light Theme

Panel:

- Dark
- Light
- System

modlarını desteklemelidir.

Terminal ve log ekranları için dark-first UI kullanılabilir.

---

# 61. API Response Standard

API response:

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "data": {},
  "message": "",
  "validationMessages": [],
  "timeStamp": "2026-10-03T20:15:00+03:00",
  "apiVersion": "1.0"
}
```

---

# 62. Error Handling

Global exception middleware:

```text
ValidationException
AuthenticationException
AuthorizationException
ServerConnectionException
SshException
DockerException
DeploymentException
FileOperationException
```

hatalarını merkezi olarak yönetmelidir.

Sensitive bilgiler error response içerisinde gösterilmemelidir.

---

# 63. Security Rules

Kesinlikle:

- SSH password loglanmamalı
- Private key loglanmamalı
- Environment secret loglanmamalı
- Terminal output gerekiyorsa configurable retention uygulanmalı
- Tokenlar maskelenmeli
- API key hash/encryption ile korunmalı
- CSRF koruması
- XSS koruması
- Rate limiting
- RBAC
- Audit logging
- Secure headers

uygulanmalıdır.

---

# 64. Dangerous Operations

Aşağıdaki işlemler için ikinci doğrulama düşünülebilir:

- Server delete
- Container delete
- Volume delete
- Docker prune
- File delete
- Recursive folder delete
- Reboot
- Shutdown
- Firewall change
- User delete
- Database restore
- Backup restore

---

# 65. Two-Factor Authentication

Admin kullanıcıları için:

- TOTP
- Recovery codes
- Optional WebAuthn

desteklenebilir.

Özellikle terminal, server credential ve deployment işlemlerinde step-up authentication kullanılabilir.

---

# 66. API Keys

External automation için API key sistemi:

```text
Name
Key
Permissions
Expiration
Last Used
Created By
```

API key scope:

```text
server:read
docker:read
deployment:execute
backup:execute
```

gibi granular olmalıdır.

---

# 67. Webhook

Dış sistemlerden event alınabilir:

```text
Deployment completed
Git push
Monitoring event
Backup event
```

Ayrıca sistem kendi webhooklarını gönderebilir.

---

# 68. Import / Export

Sunucu bilgileri:

- JSON
- CSV

ile import/export edilebilir.

Secret değerleri export edilmemelidir.

---

# 69. Search

Global search:

```text
Server
Container
Image
Deployment
Domain
Alert
User
Log
```

üzerinde çalışmalıdır.

Örneğin:

```text
api
```

aranınca:

```text
API-01
api-container
api deployment
api logs
```

görülebilmelidir.

---

# 70. MVP Fazları

## Phase 1 — Foundation

- Authentication
- Identity
- RBAC
- Layout
- Dashboard
- Server CRUD
- Secure credential storage
- SSH connection test

## Phase 2 — Monitoring

- CPU
- RAM
- Disk
- Network
- Uptime
- Health checks
- Charts

## Phase 3 — Docker

- Containers
- Images
- Volumes
- Networks
- Container logs
- Start/Stop/Restart
- Container terminal

## Phase 4 — Terminal & Files

- Web terminal
- File browser
- File editor
- Upload/download
- Permission management

## Phase 5 — Dokploy

- Installation wizard
- Health check
- Dokploy status
- API integration

## Phase 6 — Deployment

- Git integration
- Projects
- Deployment
- Deployment logs
- History

> Durum: tamamlandı. Ayrıntılar için bkz. [Deployment](#deployment-1).

## Phase 7 — Monitoring & Alerts

- Alert rules
- Email
- Telegram
- Discord
- SSL monitoring
- Uptime

> Durum: tamamlandı. Ayrıntılar için bkz. [Alarmlar ve izleme](#alarmlar-ve-izleme).

## Phase 8 — Backup

- Docker volume backup
- Database backup
- File backup
- S3/R2
- Restore

> Durum: tamamlandı. Ayrıntılar için bkz. [Yedekleme](#yedekleme).

## Phase 9 — Security

- Security center
- SSH audit
- Port audit
- Firewall status
- 2FA
- Advanced audit

> Durum: tamamlandı. Ayrıntılar için bkz. [Güvenlik merkezi](#güvenlik-merkezi), [İki adımlı doğrulama](#iki-adımlı-doğrulama-2fa) ve [Audit log](#audit-log).

## Phase 10 — Advanced

- Agent
- Multi-server command runner
- Cloud provider integration
- Cost monitoring
- Server provisioning
- Server templates

> Durum: tamamlandı. Ayrıntılar için bkz. [Toplu komut ve şablonlar](#toplu-komut-ve-şablonlar), [Bulut sağlayıcıları](#bulut-sağlayıcıları), [Maliyet raporu](#maliyet-raporu), [Agent](#agent) ve [Ayarlar](#ayarlar).

---

# 71. İlk Sürümde Kesinlikle Olması Gerekenler

MVP'nin başarılı olması için ilk versiyonda şu özellikler tamamlanmalıdır:

### Server

- Server CRUD
- SSH authentication
- Connection test
- Server health
- CPU/RAM/Disk/Network

### Docker

- Container list
- Status
- Logs
- Start
- Stop
- Restart
- Container terminal
- Images
- Volumes
- Networks

### Terminal

- Web terminal
- Multiple sessions
- Resize
- Command history

### Files

- Browse
- Create
- Edit
- Delete
- Upload
- Download
- Permissions

### Dokploy

- Install
- Status
- Health check

### Security

- RBAC
- Encryption
- Audit logs
- 2FA
- Dangerous action confirmation

---

# 72. Gelecekte Eklenebilecek Özellikler

## Infrastructure

- Kubernetes
- Proxmox
- VMware
- LXC/LXD
- Cloud provider API
- VM lifecycle management

## DevOps

- CI/CD
- GitOps
- Deployment pipelines
- Canary deployment
- Blue/Green deployment
- Rollback

## Observability

- Prometheus
- Grafana integration
- Loki
- OpenTelemetry
- Distributed tracing

## AI Assistant

Panel içerisinde AI assistant eklenebilir.

Örneğin kullanıcı:

> "Bu sunucuda neden CPU kullanımı yüksek?"

dediğinde sistem:

1. Metrics inceler
2. Process listesini inceler
3. Docker containerlarını inceler
4. Logları analiz eder
5. Bulguları açıklar
6. Önerilen aksiyonları listeler

AI doğrudan destructive komut çalıştırmamalı; aksiyonlar kullanıcı onayıyla uygulanmalıdır.

---

# 73. AI DevOps Copilot

İleri sürümde:

```text
User
 ↓
AI Copilot
 ↓
Permission Check
 ↓
Read-only diagnostics
 ↓
Analysis
 ↓
Suggested Actions
 ↓
User Confirmation
 ↓
Execution
 ↓
Audit Log
```

Örnek:

```text
"api container sürekli restart oluyor."
```

AI:

```text
Container: api
Restart Count: 47

Muhtemel neden:
Application exited with code 1.

Son loglarda:
Connection refused

Öneri:
Database bağlantısını kontrol edin.
```

AI kendiliğinden:

```bash
docker rm -f api
```

gibi destructive işlem çalıştırmamalıdır.

---

# 74. Observability Roadmap

Uzun vadede:

```text
Server Metrics
       ↓
Metrics Collector
       ↓
Time Series Storage
       ↓
Dashboard
       ↓
Alert Engine
```

Metrik sayısı büyürse MSSQL yerine veya yanında:

- Prometheus
- VictoriaMetrics
- TimescaleDB

gibi time-series çözümleri değerlendirilebilir.

---

# 75. Performance

Sistem yüzlerce sunucuyu yönetebilecek şekilde tasarlanmalıdır.

### Kurallar

- Async I/O
- Connection pooling
- Background workers
- Job queue
- Caching
- SignalR
- Metric aggregation
- Pagination
- Lazy loading
- Rate limiting

Sunucular aynı anda sorgulanmamalı; concurrency limiti uygulanmalıdır.

---

# 76. Observability of Server Manager

Server Management uygulamasının kendisi de izlenmelidir.

- Application health
- Database health
- Redis health
- Worker health
- Queue health
- SignalR connections
- SSH failures
- Docker API failures

---

# 77. Redis

Redis şu amaçlarla kullanılabilir:

- Cache
- Distributed locks
- Session
- SignalR backplane
- Background job state
- Temporary terminal session state

---

# 78. Deployment Strategy

Server Manager'ın kendisi de Docker ile çalıştırılabilmelidir.

Örnek:

```text
server-manager-web
server-manager-worker
server-manager-db
server-manager-redis
```

Docker Compose ile production deployment desteklenmelidir.

---

# 79. Backup Strategy

Minimum:

```text
Database
Credentials metadata
Application configuration
Audit logs
```

yedeklenmelidir.

Secret encryption key backup stratejisi ayrıca tasarlanmalıdır.

Encryption key kaybedilirse encrypted credentials geri döndürülemeyebileceğinden recovery planı zorunludur.

---

# 80. Definition of Done

Bir özellik tamamlandı sayılabilmesi için:

- Backend tamamlanmış
- UI tamamlanmış
- Validation tamamlanmış
- Authorization tamamlanmış
- Audit log eklenmiş
- Error handling yapılmış
- Loading state eklenmiş
- Empty state eklenmiş
- Success/error notification eklenmiş
- Responsive kontrol edilmiş
- Test yazılmış
- Security kontrolü yapılmış
- Documentation güncellenmiş

olmalıdır.

---

# 81. Cursor AI Development Rules

Cursor AI projeyi geliştirirken:

1. Mevcut mimariyi bozmamalıdır.
2. Kullanıcı istemediği özellikleri rastgele eklememelidir.
3. Önce mevcut kodu analiz etmelidir.
4. Büyük değişikliklerde plan çıkarmalıdır.
5. Security kritik işlemleri doğrudan uygulamamalıdır.
6. Secret değerleri source code içine yazmamalıdır.
7. Repository dışından database erişmemelidir.
8. Controller içine business logic yazmamalıdır.
9. Async işlemleri tercih etmelidir.
10. Her yeni özellik için authorization kontrolü eklemelidir.
11. Destructive işlemler confirmation gerektirmelidir.
12. Audit log gerektiren işlemler audit edilmelidir.
13. UI'da Türkçe kullanıcı metinleri kullanılmalıdır.
14. Kod ve teknik isimlendirmeler İngilizce olmalıdır.
15. Gereksiz dependency eklememelidir.
16. Gereksiz refactor yapmamalıdır.
17. Test edilebilir kod yazmalıdır.
18. Production secretlarını loglamamalıdır.
19. Dokploy, Dokku gibi üçüncü parti DevOps araçlarını ve isteğe bağlı entegrasyonları host'a değil, `src/Plugins/` altında ayrı bir eklenti olarak geliştirmelidir; eklenti host projesine referans eklememeli, yalnızca `Application`, `Infrastructure` ve `Web.Framework` üzerinden çalışmalıdır.

---

# 82. Sonuç

Bu proje yalnızca basit bir "SSH paneli" olmamalıdır.

Hedef ürün:

> **Sunucu + Docker + Dokploy + Deployment + Monitoring + Terminal + Files + Backup + Security + Alerting + AI DevOps Assistant**

özelliklerini tek platformda birleştiren profesyonel bir Server Management Platform'dur.

İlk hedef küçük ve güvenli bir MVP oluşturmak; daha sonra Docker, Dokploy, deployment, monitoring, backup ve AI özelliklerini aşamalı şekilde genişletmektir.

---

## Önerilen Proje Adı

Geçici isim:

**Server Management Platform**

Alternatif marka isimleri:

- ServerPilotX
- ServerHub
- ServerDeck
- InfraPilot
- ServerOps
- DevOpsHub
- InfraManager
- OpsCenter
- ServerControl
- InfraDesk

---

## İlk Geliştirme Sırası

```text
1. Authentication / Identity
2. RBAC
3. Server Management
4. SSH Connection
5. Server Metrics
6. Dashboard
7. Docker Management
8. Container Terminal
9. File Manager
10. Dokploy Installer
11. Deployment
12. Logs
13. Alerts
14. Backup
15. Security Center
16. Audit
17. 2FA
18. AI DevOps Copilot
```

Bu sıra değiştirilmeden önce her fazın stabil ve güvenli çalışması doğrulanmalıdır.

---

## Geliştirme Ortamı

Phase 1 (Foundation) kapsamındaki özellikler: Identity ile giriş/çıkış, rol ve izin tabanlı yetkilendirme (RBAC), dashboard, sunucu ekleme/düzenleme/silme, şifreli kimlik bilgileri (AES-256-GCM), SSH bağlantı testi (host key fingerprint TOFU), kullanıcı yönetimi ve audit log.

Phase 2 (Monitoring) kapsamındaki özellikler: agentless SSH ile CPU, RAM, swap, disk, inode, network, load, uptime ve process metrikleri; otomatik sunucu durumu (Healthy / Warning / Critical / Offline); sağlık kontrolleri; saatlik özetleme ve saklama süresi politikası; SignalR ile canlı güncelleme; Chart.js grafikleri (1 saat – 30 gün).

Phase 3 (Docker) kapsamındaki özellikler: SSH üzerinden Docker CLI ile genel bakış, container listesi (canlı CPU/RAM), container detayı (inspect, port, mount, network, env, label), start/stop/restart/pause/unpause/kill/remove/rename, loglar (arama, stderr filtresi, canlı takip, indirme), xterm.js ile container terminali, image (pull, sil, prune), volume (oluştur, sil, prune) ve network (oluştur, sil) yönetimi. Sudo yetkili kullanıcılar desteklenir.

Phase 4 (Terminal & Files) kapsamındaki özellikler: tarayıcıdan SSH terminali (Browser → SignalR WebSocket → API → SSH; çoklu sekme, yeniden boyutlandırma, tam ekran, kopyala/yapıştır, arama, çıktı indirme, komut geçmişi, sayfa yenilenince/bağlantı kopunca aynı oturuma yeniden bağlanma, boşta kalma zaman aşımı), tehlikeli komutlarda ikinci onay, oturum ve komut geçmişi; SFTP tabanlı dosya yöneticisi (gezinme, oluşturma, yeniden adlandırma/taşıma, kopyalama, silme, sürükle-bırak yükleme, indirme, chmod/chown) ve CodeMirror editörü (JSON, YAML, XML, HTML, CSS, JS, TS, C#, ENV, Markdown, Nginx, Dockerfile, Shell ve daha fazlası).

Phase 5 (Dokploy) kapsamındaki özellikler — `DevOps.Dokploy` eklentisi olarak (bkz. [Eklentiler](#eklentiler)): Dokploy kurulum sihirbazı (sunucu seçimi → uyumluluk → Docker → port → onay → kurulum → sağlık kontrolü → tamamlandı), ön kontroller (işletim sistemi, mimari, container ortamı, root/sudo, RAM, disk, port, internet), canlı kurulum çıktısı (sayfa kapansa da kurulum sürer, geri dönünce çıktı yeniden oynatılır), kurulum geçmişi; Dokploy durumu (sürüm, durum, adres, port, swarm servisleri ve container'lar, son sağlık kontrolü, yanıt süresi), periyodik sağlık kontrolü ve API anahtarıyla proje özeti.

### Teknoloji

| Katman | Teknoloji |
| --- | --- |
| Web | ASP.NET Core MVC (.NET 10), Tailwind CSS v4 + Sass, ES module JavaScript |
| Arayüz bileşenleri | SweetAlert2 (onay/uyarı), toastr (bildirim), xterm.js (terminal), CodeMirror 5 (dosya editörü) |
| Kimlik | ASP.NET Core Identity (Guid anahtarlı), izin claim'leri |
| Veri | MSSQL, EF Core (yalnızca sorgu), FluentMigrator (tüm şema) |
| Doğrulama | FluentValidation |
| SSH | SSH.NET |
| Canlı veri | ASP.NET Core SignalR, Chart.js |
| Log | Serilog (konsol + `logs/` dosyası) |
| Test | xUnit v3, NSubstitute |

Mimari: `Domain` → `Application` (servisler, DTO, validator, arayüzler) → `Infrastructure` (EF Core, repository, Identity, şifreleme, SSH) → `Web.Framework` (yetkilendirme, JSON yanıtı, sunucu sayfası, eklenti yükleyici) → `Web` (controller, view). Eklentiler `src/Plugins/` altında `Infrastructure` ve `Web.Framework`'e referans veren ayrı projelerdir; host eklentilere derleme zamanında bağlı değildir. Mediator yoktur; akış `Controller → Service → Repository` şeklindedir.

### Gereksinimler

- .NET SDK 10.0
- Node.js 20+ (yalnızca stil derlemesi ve vendor dosyalarının kopyalanması için)
- Docker (yerel MSSQL için)

### 1. Veritabanını başlatma

```bash
cp .env.example .env          # MSSQL_SA_PASSWORD değerini güçlü bir parolayla değiştirin
docker compose up -d db       # MSSQL 2022, yalnızca 127.0.0.1:14340 üzerinden erişilir
```

`.env` dosyası git'e eklenmez.

### 2. Gizli ayarlar (user-secrets)

Gizli değerler kaynak koda veya `appsettings.json` dosyasına yazılmaz. Geliştirmede `dotnet user-secrets`, sunucuda ortam değişkenleri kullanılır (ör. `Security__MasterKey`).

```bash
cd src/ServerManager.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,14340;Database=ServerManager;User Id=sa;Password=<MSSQL_SA_PASSWORD>;TrustServerCertificate=True;Encrypt=True"
dotnet user-secrets set "Security:MasterKey" "$(openssl rand -base64 32)"
dotnet user-secrets set "Seed:AdminEmail" "admin@example.com"
dotnet user-secrets set "Seed:AdminPassword" "<en az 10 karakter, büyük/küçük harf ve rakam>"
dotnet user-secrets set "Seed:AdminFullName" "Sistem Yöneticisi"
```

| Anahtar | Açıklama |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | MSSQL bağlantı dizesi |
| `Security:MasterKey` | 32 baytlık base64 anahtar; SSH parolası, private key, passphrase ve sudo parolasını şifreler |
| `Security:KeyVersion` | Anahtar sürümü (varsayılan `1`); şifreli değerler `v{sürüm}:` önekiyle saklanır |
| `Seed:AdminEmail` / `Seed:AdminPassword` / `Seed:AdminFullName` | İlk SuperAdmin hesabı (bu e-postayla kullanıcı yoksa oluşturulur; mevcut kullanıcının parolası/rolü değiştirilmez) |
| `Database:AutoCreate` | `true` ise veritabanı yoksa oluşturulur (Development'ta açık) |
| `Proxy:TrustForwardedHeaders` | Reverse proxy arkasında `X-Forwarded-*` başlıklarına güvenilsin mi |

> **Önemli:** `Security:MasterKey` kaybolur veya değişirse kayıtlı sunucu kimlik bilgileri çözülemez. Anahtarı güvenli bir yerde yedekleyin.

### 3. Frontend derleme

```bash
cd src/ServerManager.Web
npm install
npm run build                # vendor kopyalama + stil derleme
npm run build:css            # yalnızca stiller (Sass → Tailwind → minify)
npm run build:css:watch      # geliştirme sırasında izleme modu
npm run build:vendor         # jQuery, toastr, SweetAlert2, Chart.js, SignalR, xterm.js → wwwroot/lib
```

Derlenmiş `wwwroot/css` ve `wwwroot/lib` dosyaları repoya dahildir; SCSS, view veya JS'te sınıf değişikliği yapıldığında ya da paket sürümü güncellendiğinde yeniden derlenmelidir. Kütüphaneler CDN yerine yerelden sunulur (CSP `script-src 'self'`).

Frontend yapısı:

| Yol | İçerik |
| --- | --- |
| `Styles/site.scss` | Ortak stiller: `base`, `layout`, `components`, `vendor` (SweetAlert2/toastr temaları) partial'ları ve Tailwind teması |
| `Styles/pages/<sayfa>.scss` | Yalnızca o sayfaya ait stiller → `wwwroot/css/pages/<sayfa>.css` |
| `wwwroot/js/core` | `http` (fetch + antiforgery), `dialog` (SweetAlert2), `notify` (toastr), `forms`, `dom`, `format`, `navigation`, `regions` |
| `wwwroot/js/components` | `ajax-actions`, `ajax-list`, `modal`, `remote-panels`, `tabs` |
| `wwwroot/js/features` | Monitoring, sunucu ve Docker modülleri |
| `wwwroot/js/pages/<sayfa>.js` | Sayfanın giriş modülü |
| `src/Plugins/<eklenti>/Styles/pages/<sayfa>.scss` | Eklenti sayfası stili → eklentinin `Content/css/pages/<sayfa>.css` dosyası (aynı `npm run build:css` derler) |

- View'da `ViewData["Page"] = "<sayfa>"` atanınca layout `css/pages/<sayfa>.css` ve `js/pages/<sayfa>.js` dosyalarını ekler. Eklenti view'larında ayrıca `ViewData[PluginContent.PagePluginKey]` atanır; dosyalar `/plugins/<eklenti-adı-küçük-harf>/` altından yüklenir.
- JavaScript yalnızca ES module'dür; layout'taki import map her modülü sürüm parametreli adrese eşler (önbellek bozma).
- Veri çekme, ekleme, düzenleme, silme ve uyarıların tamamı AJAX ile yapılır; sayfa yenilenmez. Listeler partial view + `pushState` ile filtrelenir, formlar JSON (`isSuccess`, `message`, `data`, `errors`) döner.
- Onaylar SweetAlert2 ile (tehlikeli işlemlerde ad yazarak), bildirimler toastr ile gösterilir.

### 4. Çalıştırma

```bash
dotnet run --project src/ServerManager.Web --launch-profile https
```

- Uygulama açılışta FluentMigrator migration'larını uygular, rolleri/izinleri ve ilk SuperAdmin'i oluşturur (idempotent; mevcut veri silinmez).
- Adres: `https://localhost:7180` (http profili: `http://localhost:5180`).

### 5. Testler

```bash
dotnet test --solution ServerManager.slnx
```

Testler veritabanına veya gerçek sunuculara bağlanmaz. `global.json` içinde Microsoft.Testing.Platform runner'ı seçilidir.

### Roller ve izinler

| Rol | İzinler |
| --- | --- |
| SuperAdmin | Tümü (eklenti yönetimi `plugin.manage` yalnızca SuperAdmin'dedir) |
| Admin | Dashboard, sunucu görüntüleme/ekleme/düzenleme/silme/bağlantı testi, tüm Docker, terminal, dosya ve deployment izinleri, sistem bilgisi görüntüleme/yönetim, alarm görüntüleme/üstlenme/yönetim, yedekleme, güvenlik merkezi, audit log görüntüleme/dışa aktarma, toplu komut görüntüleme/çalıştırma, şablon yönetimi, bulut sağlayıcı görüntüleme/yönetim/sunucu oluşturma, ayarlar |
| Operator | Dashboard, sunucu görüntüleme, bağlantı testi, Docker görüntüleme/başlatma/durdurma/yeniden başlatma/terminal, sunucu terminali, sistem bilgisi görüntüleme/yönetim, dosya görüntüleme/oluşturma/düzenleme/yükleme/indirme, deployment görüntüleme/çalıştırma, alarm görüntüleme/üstlenme, yedek görüntüleme/çalıştırma, güvenlik görüntüleme/tarama, toplu komut geçmişi görüntüleme, bulut sağlayıcı görüntüleme |
| Developer | Dashboard, sunucu görüntüleme, sistem bilgisi görüntüleme, Docker görüntüleme/yeniden başlatma, dosya görüntüleme/indirme, deployment görüntüleme/çalıştırma, alarm görüntüleme |
| Viewer | Dashboard, sunucu görüntüleme, Docker görüntüleme, deployment görüntüleme, alarm görüntüleme |

İzinler `AspNetRoleClaims` tablosunda `permission` claim'i olarak tutulur. Seeder yalnızca eksik izinleri ekler; elle eklenmiş izinleri kaldırmaz. Eklentiler kendi izinlerini ve varsayılan rol dağılımını getirir; bunlar eklenti kurulurken ve her açılışta eksikse eklenir (Dokploy için bkz. [Dokploy](#dokploy)).

Metrik ve grafik görüntüleme `server.view`, "Şimdi topla" (anlık metrik toplama) `server.connect` izni gerektirir.

### İzleme (Monitoring)

Arka plan servisi (`MetricsCollectorWorker`) izlemesi açık ve host key fingerprint'i doğrulanmış her sunucuya belirlenen aralıkla SSH ile bağlanır, tek bir salt okunur komutla `/proc`, `df`, `ps` ve `ip` çıktısını okur. Sunucuya agent kurulmaz, dosya yazılmaz. Toplama sırasında fingerprint ilk kez kaydedilmez (TOFU yalnızca bağlantı testinde); farklı bir anahtar gelirse sunucu hemen Offline olur.

| Anahtar (`Monitoring:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `Enabled` | `true` | Toplayıcı ve bakım servislerini açar/kapatır |
| `IntervalSeconds` | `30` | Toplama aralığı (en az 10 sn) |
| `MaxConcurrency` | `4` | Aynı anda bağlanılan en fazla sunucu |
| `OfflineAfterFailures` | `2` | Kaç ardışık başarısız toplamadan sonra Offline sayılır |
| `CpuWarningPercent` / `CpuCriticalPercent` | `80` / `95` | CPU eşikleri |
| `MemoryWarningPercent` / `MemoryCriticalPercent` | `85` / `95` | RAM eşikleri |
| `DiskWarningPercent` / `DiskCriticalPercent` | `80` / `90` | En dolu disk bölümü eşikleri |
| `RawRetentionHours` | `48` | Ham ölçümlerin saklama süresi |
| `HourlyRetentionDays` | `90` | Saatlik özetlerin saklama süresi |
| `HealthCheckRetentionDays` | `30` | Sağlık kontrolü kayıtlarının saklama süresi |
| `MaintenanceIntervalMinutes` | `10` | Özetleme ve saklama işinin çalışma aralığı |

Durum kuralları:

- Eşik aşımı yoksa **Healthy**, uyarı eşiği aşılırsa **Warning**, kritik eşik aşılırsa **Critical**.
- Ardışık başarısız toplama sayısı `OfflineAfterFailures` değerine ulaşırsa **Offline**.
- **Bakımda** durumundaki sunucunun durumu otomatik değiştirilmez; metrikleri toplanmaya devam eder.
- Her durum değişikliği audit log'a `server.status_changed` olarak (kullanıcı: "Sistem") yazılır.

Saklama politikası (`MetricsMaintenanceWorker`):

- Tamamlanmış saatler önce `ServerMetricsHourly` tablosuna özetlenir, ardından süresi dolan ham kayıtlar silinir. 1 saat – 24 saat grafikleri ham veriden, 7 gün ve 30 gün grafikleri saatlik özetten çizilir.
- Silme yalnızca geçici tablolarda (`ServerMetrics`, `ServerMetricsHourly`, `ServerHealthChecks`, `UptimeCheckResults`, `NotificationDeliveries`) çalışır; bu liste dışında bir tablo, zaman sütunu veya gruplama sütunu istenirse işlem hata verip durur. Uptime ve bildirim kayıtlarının saklaması için bkz. [Alarmlar ve izleme](#alarmlar-ve-izleme).
- Her sunucunun en son ölçümü ve son 50 sağlık kontrolü süre dolsa bile korunur. Sunucu, kullanıcı ve audit kayıtlarına dokunulmaz.
- Silme 5000 satırlık partilerle yapılır; her çalışmanın özeti (özetlenen saat ve silinen kayıt sayıları) loglanır.

Canlı güncelleme `/hubs/monitoring` SignalR hub'ı üzerinden yapılır; sunucu detay sayfası yalnızca kendi sunucusunun, liste ve dashboard tüm sunucuların güncellemelerini alır. Hub `server.view` izni gerektirir.

### Docker

Docker yönetimi agentless'tır: panel sunucuya SSH ile bağlanıp `docker` CLI komutlarını çalıştırır (Docker API'si dışarı açılmaz, sunucuya ek yazılım kurulmaz). Sunucunun host key fingerprint'i doğrulanmış olmalıdır. Sunucuda "Sudo kullanıcı" işaretliyse (SSH kullanıcısı `docker` grubunda değilse gerekir) komutlar `sudo -S -p '' -- docker …` ile çalıştırılır (parola tanımlı değilse `sudo -n`); sudo parolası şifreli saklanır, komut satırına yazılmaz, stdin'den gönderilir.

| İzin | Kapsam |
| --- | --- |
| `docker.view` | Genel bakış, container/image/volume/network listeleri, inspect, loglar |
| `docker.start` | Container başlatma / devam ettirme |
| `docker.stop` | Durdurma / duraklatma / kill |
| `docker.restart` | Yeniden başlatma |
| `docker.delete` | Container, image, volume, network silme ve prune |
| `docker.manage` | Image pull, volume/network oluşturma, container yeniden adlandırma |
| `docker.terminal` | Container terminali |

| Anahtar (`Docker:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `CommandTimeoutSeconds` | `60` | Docker komutu zaman aşımı |
| `PullTimeoutSeconds` | `600` | Image pull zaman aşımı |
| `DefaultLogTail` / `MaxLogTail` | `200` / `5000` | Log satır sayısı varsayılanı ve üst sınırı |

Container terminali sunucu terminaliyle aynı altyapıyı ve `Terminal:` ayarlarını kullanır (bkz. [Terminal](#terminal)).

Güvenlik kuralları:

- Container, image, volume ve network adları whitelist regex'iyle doğrulanır; tüm argümanlar shell quoting ile komuta eklenir.
- Stop, restart, kill, silme ve prune işlemleri onay ister; silme işlemlerinde kaynak adı yazılarak onaylanır. Her işlem audit log'a yazılır.
- Network oluştururken verilen subnet sunucunun kendi ağlarıyla veya panelin bağlandığı adresle çakışıyorsa işlem reddedilir (aksi halde yeni bridge SSH erişimini kesebilir).
- Docker aksiyonları kullanıcı başına dakikada 30 istekle sınırlandırılır.
- Container terminali `/hubs/terminal` SignalR hub'ı üzerinden çalışır; oturum açılışı, kapanışı ve girilen komutlar kayda alınır, çıktı loglanmaz.

#### Yerel Docker test sunucusu

Geliştirmede gerçek sunucular yerine izole bir Docker-in-Docker container'ı kullanılır. Host Docker soketine bağlanmaz; içindeki container'lar host'tan tamamen ayrıdır.

```bash
# .env: SSH_TEST_PASSWORD ve (isteğe bağlı) SSH_TEST_PORT=2223
docker compose --profile test up -d --build ssh-docker-test
```

- Adres: `127.0.0.1:2223` (yalnızca localhost).
- `deploy` kullanıcısı `docker` grubundadır (sudo gerekmez).
- `ops` kullanıcısı yalnızca `sudo docker` çalıştırabilir (sudo akışını test etmek için; panelde "Sudo kullanıcı" işaretlenir ve sudo parolası olarak aynı parola girilir).
- Parola yalnızca git'e eklenmeyen `.env` dosyasındadır.

### Terminal

Sunucu detayındaki **Terminal** sekmesi tarayıcıdan SSH kabuğu açar: Browser → `/hubs/terminal` (SignalR WebSocket) → API → SSH. Sunucunun host key fingerprint'i doğrulanmış olmalıdır.

| İzin | Kapsam |
| --- | --- |
| `terminal.view` | Terminal sekmesi ve oturum geçmişi (kendi oturumları; `audit.view` izni olan tüm kullanıcıların oturumlarını görür) |
| `terminal.execute` | Sunucuda terminal oturumu açma, kendi komut geçmişi |

- Birden fazla sekme açılabilir; sekme listesi tarayıcı oturumunda (`sessionStorage`) tutulur. Sayfa yenilenir veya bağlantı koparsa aynı SSH oturumuna yeniden bağlanılır ve son çıktı ekrana geri basılır. Oturum başka bir sekmede açıldığında eski sekme "Buraya al" ile geri alabilir.
- Araç çubuğu: arama (Enter / Shift+Enter), kopyala (Ctrl+Shift+C), yapıştır (Ctrl+Shift+V), çıktıyı `.txt` olarak indirme, tam ekran, komut geçmişi paneli (tıklanan komut Enter'a basılmadan satıra yazılır).
- Boşta kalan oturum `IdleTimeoutMinutes` sonunda, tarayıcısı kopmuş oturum `ReconnectGraceSeconds` sonunda kapatılır.
- **Tehlikeli komutlar** (`rm -r`, `mkfs`, `dd of=`, `shutdown`/`poweroff`/`halt`, `reboot`, `iptables`/`nft`/`ufw`, `userdel`, disk aygıtına yazma, fork bomb) varsayılan olarak engellenmez; komut sunucuya gönderilmeden bekletilir ve kullanıcıdan ikinci onay istenir. Yanıt verilmezse `ConfirmationTimeoutSeconds` sonunda iptal edilir. Mod `Off` / `Warn` / `Confirm` / `Block` olarak ayarlanabilir, kurallar `DangerousCommands` ile değiştirilebilir (`[{ "Pattern": "<regex>", "Description": "…" }]`; boş liste varsayılan kuralları kullanır).
- Oturum açılışı/kapanışı (kullanıcı, IP, süre) audit log'a, girilen komutlar `TerminalCommands` tablosuna yazılır. Tehlikeli komutlar ayrıca `terminal.dangerous_command` olarak audit log'a düşer. Parola sorusu (sudo, ssh) ekrandayken ve tam ekran uygulamalarda (vim, less, top) yazılanlar komut olarak kaydedilmez; çıktı hiç loglanmaz. Ok tuşu, Tab veya geçmişten çağırma ile değişen satırlar "yaklaşık" olarak işaretlenir.
- Terminal geçmişi otomatik silinmez. Uygulama kapanırken açık kalan oturum kayıtları silinmeden "kapandı" olarak işaretlenir.

| Anahtar (`Terminal:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `IdleTimeoutMinutes` | `30` | Boşta kalan oturumun kapatılma süresi |
| `MaxSessionsPerUser` | `5` | Kullanıcı başına eş zamanlı oturum (sunucu + container) |
| `ReconnectGraceSeconds` | `120` | Tarayıcı bağlantısı koptuğunda oturumun açık tutulduğu süre |
| `OutputBufferKilobytes` | `256` | Yeniden bağlanınca geri basılan son çıktı |
| `ConfirmationTimeoutSeconds` | `120` | Onay bekleyen tehlikeli komutun iptal süresi |
| `DangerousCommandMode` | `Confirm` | `Off`, `Warn`, `Confirm` veya `Block` |
| `DangerousCommands` | `[]` | Özel kural listesi (boşsa varsayılan kurallar) |

### Dosyalar

Sunucu detayındaki **Files** sekmesi SFTP üzerinden çalışır (sunucuya ek yazılım kurulmaz). Kopyalama, klasör silme, chmod ve chown SSH komutuyla (`cp -a`, `rm -rf`, `chmod`, `chown`; argümanlar shell quoting ile) yapılır; sunucuda "Sudo kullanıcı" işaretliyse bu komutlar sudo ile çalışır.

| İzin | Kapsam |
| --- | --- |
| `file.view` | Klasör listeleme, dosyayı editörde salt okunur açma |
| `file.create` | Dosya / klasör oluşturma, kopyalama |
| `file.edit` | Dosya kaydetme, yeniden adlandırma / taşıma |
| `file.delete` | Dosya / klasör silme |
| `file.upload` | Dosya yükleme (düğme veya sürükle-bırak) |
| `file.download` | Dosya indirme |
| `file.permissions` | chmod / chown (owner/group/rwx tablosu veya gelişmiş modda ham değer, özyinelemeli seçenek) |

- Editör: CodeMirror 5; söz dizimi dosya adından seçilir (JSON, YAML, XML, HTML, CSS, JS, TS, C#, ENV, Markdown, Nginx, Dockerfile, Shell, SQL, Python, Go, TOML, PHP, Diff) ve elle değiştirilebilir. Ctrl/Cmd+S kaydeder, Ctrl+F arar, Alt+G satıra gider. Yalnızca UTF-8 metin dosyaları açılır; BOM ve satır sonu (LF/CRLF) korunur. Dosya siz düzenlerken sunucuda değiştiyse kaydetme çakışma uyarısı verir ve üzerine yazma onayı ister. Kaydedilmemiş değişiklik varken sayfadan çıkış onay ister.
- Dosya silme onay ister; klasör silme klasör adının birebir yazılmasıyla onaylanır. Var olan bir dosyanın üzerine yükleme ayrıca onay ister.
- Sistem klasörleri (`/`, `/etc`, `/usr`, `/var` vb., `ProtectedPaths`) panelden silinemez, taşınamaz ve izinleri değiştirilemez (içlerindeki dosyalar etkilenmez).
- Tüm işlemler (görüntüleme, kaydetme, oluşturma, taşıma, kopyalama, silme, izin, yükleme, indirme) audit log'a yazılır. Dosya aksiyonları kullanıcı başına dakikada 60 istekle sınırlandırılır.

| Anahtar (`Files:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `MaxEditKilobytes` | `1024` | Editörde açılabilecek en büyük dosya |
| `MaxUploadMegabytes` | `100` | Dosya başına yükleme sınırı |
| `OperationTimeoutSeconds` | `60` | SFTP işlemleri ile kopyalama, klasör silme ve izin komutlarının zaman aşımı |
| `ProtectedPaths` | `[]` | Korunan yollar (boşsa varsayılan sistem klasörleri) |

### Dokploy

> Eklenti: `DevOps.Dokploy` (`src/Plugins/ServerManager.Plugin.DevOps.Dokploy`). Açılışta otomatik kurulmaz; devre dışı bırakılırsa sekme, sayfalar, hub ve sağlık kontrolü durur, kayıtlar silinmez.

Sunucu detayındaki **Dokploy** sekmesi Dokploy kurulumunun durumunu gösterir; **Dokploy kur** sihirbazı resmi kurulum betiğiyle (`https://dokploy.com/install.sh`) kurulum yapar. Durum agentless olarak SSH ile (`docker service ls`, `docker ps`, sunucu içinden `/api/health`) okunur; panel adresi ve API anahtarı tanımlıysa panelden HTTP ile sağlık kontrolü ve proje özeti alınır.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `dokploy.view` | Dokploy sekmesi, durum, sağlık kontrolü, kurulum geçmişi ve kurulum çıktıları | SuperAdmin, Admin, Operator, Developer, Viewer |
| `dokploy.install` | Uyumluluk kontrolü ve kurulumu başlatma | SuperAdmin, Admin |
| `dokploy.manage` | Panel adresi ve API anahtarı ayarları | SuperAdmin, Admin |

Kurulum akışı:

- Ön kontroller: işletim sistemi (Ubuntu, Debian, Fedora, CentOS/RHEL dışı dağıtımlar uyarı), mimari, container içinde çalışma (engeller; Swarm container içinde çalışmaz), root veya tam sudo yetkisi, en az `MinMemoryMb` RAM, `RecommendedDiskGb` altı boş disk (uyarı), Docker ve Swarm durumu, `RequiredPorts` portlarının boş olması, betik adresine ve Docker Hub'a erişim, `curl` varlığı. Engelleyen sorun varsa onay adımı açılmaz.
- Onay: sunucu adı birebir yazılarak onaylanır; sürüm son kararlı, canary veya belirli bir etiket (`v0.25.3` gibi) seçilebilir.
- Betik sunucuya `/tmp` altına indirilir, yalnızca sahibi okuyabilir yapılır, SHA-256 özeti kurulum kaydına yazılır, `sudo` ile (gerekirse) root olarak çalıştırılır ve işlem bitince silinir.
- Çıktı `/hubs/dokploy` SignalR hub'ı üzerinden canlı akar. Kurulum arka planda sürer; sayfa kapanıp yeniden açıldığında çıktı baştan oynatılır, biten kurulumların çıktısı geçmişten "Görüntüle" ile açılır. Aynı sunucuda aynı anda tek kurulum çalışır.
- Betik bittikten sonra Dokploy'un `/api/health` yanıtı `StartupTimeoutSeconds` boyunca beklenir; yanıt gelirse kurulum başarılı sayılır.
- Kurulum başlangıcı (`dokploy.install_start`) ve sonucu (`dokploy.install_complete`; başarılı/başarısız, betik SHA-256 özetiyle) audit log'a yazılır. Uygulama kurulum sırasında kapanırsa kayıt silinmez, "Kesildi" olarak işaretlenir.

Durum ve ayarlar:

- Durum: Çalışıyor, Sorunlu (eksik bileşen veya panelden erişilemiyor), Durdu, Kurulu değil. Arka plan servisi (`DokployHealthWorker`) kayıtlı her kurulumu `HealthCheckIntervalMinutes` aralığıyla kontrol eder.
- API anahtarı Dokploy'da **Settings → Profile → API/CLI** bölümünden oluşturulur; kaydetmeden önce panelde doğrulanır, `Security:MasterKey` ile şifrelenerek saklanır ve hiçbir yerde gösterilmez. Proje özetinde yalnızca proje adı ve uygulama/compose/veritabanı sayıları okunur; ortam değişkenleri ve parolalar eşlenmez.
- HTTP isteklerinde yönlendirme izlenmez (API anahtarı başlığı başka bir adrese taşınmasın diye); yanıt boyutu 4 MB ile sınırlıdır.
- Dokploy aksiyonları kullanıcı başına dakikada 20 istekle sınırlandırılır.

| Anahtar (`Dokploy:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `InstallScriptUrl` | `https://dokploy.com/install.sh` | Kurulum betiği |
| `RegistryCheckUrl` | `https://registry-1.docker.io/v2/` | İnternet kontrolünde erişilmesi gereken registry |
| `Port` | `3000` | Dokploy panel portu |
| `RequiredPorts` | `[80, 443, 3000]` | Boş olması gereken portlar (`appsettings` içinde değiştirilecekse ortam değişkeniyle verin; liste varsayılana eklenir) |
| `MinMemoryMb` | `2048` | En az RAM |
| `RecommendedDiskGb` | `30` | Altı uyarı olarak gösterilen boş disk |
| `CommandTimeoutSeconds` | `30` | Kontrol komutlarının zaman aşımı |
| `InstallTimeoutMinutes` | `30` | Kurulum betiğinin zaman aşımı |
| `StartupTimeoutSeconds` | `180` | Kurulumdan sonra sağlık yanıtı için bekleme süresi |
| `HttpTimeoutSeconds` | `10` | Dokploy API isteklerinin zaman aşımı |
| `HealthCheckIntervalMinutes` | `5` | Periyodik sağlık kontrolü aralığı (`0` kapatır) |
| `MaxStoredOutputKilobytes` | `512` | Kurulum kaydında saklanan çıktı (son kısım) |
| `InstallationHistoryCount` | `10` | Durum sayfasında gösterilen kurulum sayısı |

### Dokku

> Eklenti: `DevOps.Dokku` (`src/Plugins/ServerManager.Plugin.DevOps.Dokku`). Açılışta otomatik kurulmaz. Devre dışı bırakılırsa sunucu sekmesi kapanır; sunucudaki Dokku kurulumu silinmez.

Sunucu detayındaki **Dokku** sekmesi sürümü ve uygulamaları SSH ile okur (`dokku version`, `apps:list`, `ps:report`, `domains:report`). Kurulu değilse **Dokku kur** resmi bootstrap betiğini (`https://dokku.com/install/<sürüm>/bootstrap.sh`) sudo ile çalıştırır; çıktı sayfada yenilenir, uygulama yeniden başlasa bile sunucudaki betik kendi başına sürebilir. Uygulama satırındaki **Yeniden başlat** `dokku ps:restart` çağırır.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `dokku.view` | Sekme, sürüm ve uygulama listesi | SuperAdmin, Admin, Operator, Developer, Viewer |
| `dokku.manage` | Kurulum ve uygulama yeniden başlatma | SuperAdmin, Admin |

- Komutlar sudo ile çalışır. Uygulama adı ve sürüm kabuğa yazılmadan önce doğrulanır (`v1.2.3`, `my-app`).
- Kurulum kaydı veritabanına yazılmaz; süren çıktı yalnızca o süreçte tutulur. Sonuç audit log'a `dokku.install_start` ve `dokku.install_complete` olarak yazılır. Yeniden başlatma `dokku.app_restart` olur.
- İşlemler kullanıcı başına dakikada 20 istekle sınırlandırılır.

| Anahtar (`Dokku:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `Version` | `v0.38.31` | Kurulacak Dokku etiketi |
| `CommandTimeoutSeconds` | `60` | Durum ve yeniden başlatma zaman aşımı |
| `InstallTimeoutMinutes` | `20` | Bootstrap betiğinin zaman aşımı |

### Deployment

Git deposundaki bir uygulamayı kayıtlı bir sunucuya SSH üzerinden (agentless) dağıtır. Menüde **Projeler** ve **Deployment'lar** sayfaları, sunucu detayında **Deployments** sekmesi bulunur. Bu özellik çekirdeğin parçasıdır; Dokploy gibi üçüncü taraf araçlar eklenti olarak ayrı kalır.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `deployment.view` | Projeler, deployment geçmişi, canlı çıktı ve log indirme | SuperAdmin, Admin, Operator, Developer, Viewer |
| `deployment.manage` | Proje ekleme, düzenleme, silme (Git erişim anahtarı, ortam değişkenleri, build/deploy komutları dahil) | SuperAdmin, Admin |
| `deployment.execute` | Deployment başlatma, iptal etme, yeniden dağıtma, depo/dal kontrolü | SuperAdmin, Admin, Operator, Developer |

> `deployment.manage` fiilen sunucuda komut çalıştırma yetkisidir: "Komutlar" build türündeki komutlar ve Dockerfile/compose içeriği hedef sunucuda çalışır. Bu izni yalnızca sunucuya terminal erişimi verilebilecek kişilere verin.

Proje ayarları:

- **Git kaynağı:** iki yol vardır.
  - *Depo adresiyle (elle):* sağlayıcı (GitHub, GitLab, Bitbucket, kendi sunucusu), `https://` veya SSH depo adresi, dal, isteğe bağlı kullanıcı adı ve erişim anahtarı (token). SSH adreslerinde hedef sunucudaki kullanıcının anahtarı depoda yetkili olmalıdır.
  - *Bağlantıyla:* etkin bir Git entegrasyon eklentisinin (ör. [GitHub App](#github-app)) bağlantılarından biri seçilir, bağlantının erişebildiği depolar listelenir. Erişim anahtarı girilmez ve saklanmaz; her depo kontrolü ve deployment için bağlantıdan yalnızca o depoya okuma yetkili kısa ömürlü bir anahtar alınır. Projede yalnızca eklenti adı, bağlantı kimliği ve depo adı (`sahip/ad`) tutulur.
- **Dal seçimi:** "Depodaki dalları getir" düğmesi dalları listeler. Bağlantılı projelerde dallar sağlayıcı API'sinden, elle girilen adreslerde hedef sunucudan `git ls-remote --heads` ile okunur (bu yüzden önce sunucu seçilmelidir). Listeden seçilebilir veya dal adı elle yazılabilir.
- **Kayıtlı erişim anahtarı:** düzenlemede boş bırakılırsa korunur. Depo adresinin sunucusu (host) değişirse kayıtlı anahtar yeni adrese gönderilmez; yeni anahtar girilmeli veya "kaldır" seçilmelidir. Bağlantılı kaynağa geçildiğinde kayıtlı anahtar silinir.
- **Build türü:** Docker Compose (compose dosyası; proje adı `sm-<proje>`), Dockerfile (image `sm-<proje>:<kısa-sha>` ve `:latest`, tek container `sm-<proje>`, `--restart unless-stopped`, `sm.project` etiketi, isteğe bağlı port eşlemeleri) veya Komutlar (proje klasöründe `sh` ile çalışan build ve deploy komutları; isteğe bağlı sudo).
- **Sunucudaki klasör:** mutlak yol; sistem klasörleri (`/etc`, `/usr`, `/bin`, `/root` vb.) reddedilir. SSH kullanıcısının yazabildiği bir yer olmalıdır.
- **Ortam değişkenleri:** `.env` içeriği; değerler `Security:MasterKey` ile şifrelenir ve arayüzde geri gösterilmez (yalnızca anahtar adları listelenir). Düzenlemede boş bırakılırsa korunur, yazılırsa tamamı değişir.

Deployment akışı (aşamalar ekranda adım adım görünür):

1. **Hazırlık:** proje ve sunucu bağlantısı okunur, gizli bilgiler çözülür.
2. **Kaynak kod:** klasör yoksa oluşturulur ve `git init` yapılır; dal (veya belirli commit) `--depth 1` ile çekilir, çalışma ağacı o commit'e alınır. Alınan commit hemen kayda yazılır; iptal edilen veya başarısız olan deployment da hangi commit'te olduğunu bilir ve yeniden dağıtılabilir. `.env` tanımlıysa yazılır.
3. **Build:** türe göre `docker compose build`, `docker build` veya build komutu.
4. **Deploy:** `docker compose up -d --remove-orphans`, eski container kaldırılıp `docker run`, veya deploy komutu.

Çıktı `/hubs/deployments` SignalR hub'ı üzerinden canlı akar; sayfa yeniden açıldığında o ana kadarki çıktı baştan oynatılır. Deployment arka planda sürer, sayfayı kapatmak onu durdurmaz. Aynı projede aynı anda tek deployment çalışır. Liste sayfaları çalışan deployment varken 5 saniyede bir kendini yeniler. Log, deployment sayfasından düz metin olarak indirilebilir.

- **Belirli commit:** Deploy formuna tam SHA girilebilir; geçmişteki bir deployment "Yeniden deploy" ile aynı commit'e, projenin güncel ayarlarıyla dağıtılır. SHA ile fetch, Git sunucusunun buna izin vermesini gerektirir (GitHub/GitLab izin verir; kendi sunucunuzda `uploadpack.allowReachableSHA1InWant`).
- **İptal:** çalışan komut durdurulur, kayıt "İptal edildi" ve iptal eden kullanıcıyla saklanır. Sunucuda o ana kadar yapılanlar (çekilen kod, build edilen image) geri alınmaz. Uygulama kapanırken süren deployment "Kesildi" olarak işaretlenir; açılışta yarım kalan kayıtlar da "Kesildi"ye çekilir.
- **Audit:** `project.create/update/delete`, `deployment.start`, `deployment.complete` (sonuç ve commit ile), `deployment.cancel`.

Güvenlik notları:

- Erişim anahtarı komut satırına, loglara veya sunucuda diske yazılmaz; stdin ile verilir ve yalnızca o komut süresince geçici bir credential helper ile kullanılır. Depo yapılandırmasına kaydedilmez.
- `.env` stdin ile `umask 077` altında yazılır (yalnızca SSH kullanıcısı okuyabilir) ve yalnızca projede ortam değişkeni tanımlıysa yazılır; kayıtlı değişkenler kaldırılırsa sunucudaki mevcut dosyaya dokunulmaz.
- Hiçbir adım dosya silmez (`git clean` / `rm -rf` yok). Dolu ve git deposu olmayan bir klasör reddedilir; mevcut bir git deposunda çalışma ağacı çekilen commit'e zorla alınır, izlenmeyen dosyalar kalır.
- Submodule'ler çekilmez. Git komutları SSH kullanıcısıyla, Docker komutları sunucunun sudo ayarıyla çalışır.
- Proje silme kaydı listeden gizler (soft delete); sunucudaki dosyalara, container'lara ve deployment geçmişine dokunulmaz.
- Bağlantılı projelerde bağlantıyı sağlayan eklenti devre dışıysa proje detayında "(eklenti etkin değil)" görünür, depo kontrolü ve deployment açık bir hatayla durur; proje kaydı korunur.

| Anahtar (`Deployment:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `GitTimeoutSeconds` | `300` | Depo kontrolü, fetch ve checkout zaman aşımı |
| `BuildTimeoutMinutes` | `30` | Build adımının zaman aşımı |
| `DeployTimeoutMinutes` | `10` | Deploy adımının zaman aşımı |
| `MaxStoredLogKilobytes` | `1024` | Deployment kaydında saklanan log (son kısım; çalışırken 10 saniyede bir kaydedilir) |

### Alarmlar ve izleme

Menüde **Alarmlar** (açık ve geçmiş alarmlar, kurallar, bildirim kanalları), **Uptime** ve **SSL Sertifikaları** sayfaları bulunur. Üst çubuktaki zil açık alarm sayısını gösterir.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `alert.view` | Alarmlar, uptime kontrolleri ve SSL sertifikalarını görüntüleme; zil | SuperAdmin, Admin, Operator, Developer, Viewer |
| `alert.acknowledge` | Alarmı üstlenme (görüldü olarak işaretleme) | SuperAdmin, Admin, Operator |
| `alert.manage` | Kurallar, bildirim kanalları, uptime ve SSL kontrollerini ekleme, düzenleme, silme; kanal testi, "Şimdi kontrol et" | SuperAdmin, Admin |

**Kurallar.** Her kuralın türü, önem derecesi (Uyarı / Kritik), eşiği, süresi, isteğe bağlı sunucu kapsamı, bağlı kanalları, "düzelince bildir" seçeneği ve tekrar aralığı vardır. Türler: CPU, RAM, disk (en dolu bölüm), sunucu erişilemiyor, uptime kontrolü başarısız, SSL sertifikası süresi, deployment başarısız ve yedekleme başarısız. Metrik kuralları süre boyunca her ölçümde eşiğin aşılmasını bekler; tek bir anlık sıçrama alarm açmaz. `AlertEvaluationWorker` kuralları `EvaluationIntervalSeconds` aralığıyla (en az 15 sn) değerlendirir.

Kurallar kurulumda otomatik eklenmez; panelden oluşturulur. Kurala kanal bağlanmazsa alarm yalnızca panelde (zil ve Alarmlar sayfası) görünür.

**Alarm akışı.**

- Koşul oluşunca alarm açılır ve kurala bağlı kanallara **Uyarı** veya **Kritik** bildirimi gider. Aynı kural ve hedef için aynı anda tek açık alarm olur.
- Tekrar aralığı tanımlıysa alarm açık kaldıkça bu aralıkla hatırlatma gönderilir; `0` ise tek bildirim gider.
- Koşul ortadan kalkınca alarm kapanır ve "düzelince bildir" açıksa **[Düzeldi]** bildirimi gönderilir. Hedef silinirse (sunucu, uptime kontrolü, SSL izleme) veya kural kapatılırsa açık alarm da kapanır.
- SSL kuralları kalan güne göre 30, 15, 7, 3, 1 ve 0. günlerde bir kez daha bildirir; aynı adımda tekrar göndermez.
- Bakımdaki sunucular için sunucu kuralları alarm açmaz.
- Üstlenme bildirimi durdurmaz; alarmı kimin gördüğünü kaydeder. Alarm ve teslimat kayıtları geçmişte kalır, silinmez.

**Zil.** 60 saniyede bir özet okunur; yeni alarmlar bildirim olarak gösterilir (kritikler hata rengiyle, en fazla 3 tane). Bu istekler `X-Background-Poll` başlığıyla gider ve oturum süresini uzatmaz; oturum düşerse sorgu durur.

#### Bildirim kanalları

Kanallar eklenti olarak gelir (`Notifications.Email`, `Notifications.Telegram`, `Notifications.Discord`); eklenti devre dışıysa kanal kaydı korunur, gönderim "eklenti etkin değil" hatasıyla başarısız olarak kaydedilir. Her kanalın etkin/pasif durumu ve en düşük önem derecesi vardır (ör. yalnızca kritikler); düzeldi bildirimi alarmın önemine göre aynı kanallara gider. Kanal ayarlarındaki gizli değerler (SMTP parolası, bot anahtarı, webhook adresi) `Security:MasterKey` ile şifrelenir, arayüzde geri gösterilmez ve düzenlemede boş bırakılırsa korunur. **Test gönder** düğmesi kanala örnek bir bildirim yollar. Her gönderim sonucu (başarılı/başarısız, hata metni) kanal sayfasında listelenir.

| Kanal | Ayarlar | Notlar |
| --- | --- | --- |
| E-posta | SMTP sunucusu, port, güvenlik (Yok / STARTTLS / SSL), kullanıcı adı, parola, gönderen, alıcılar | MailKit ile gönderilir; adresler alan adı içermelidir |
| Telegram | Bot anahtarı, sohbet kimliği | HTML biçimiyle gider; mesaj metni kaçışlanır |
| Discord | Webhook adresi | Renkli embed (uyarı turuncu, kritik kırmızı, düzeldi yeşil); `@everyone` gibi bahsetmeler kapalıdır. Adres yalnızca `Discord:AllowedHosts` listesindeki sunuculara ve https ile gidebilir |

Bildirim metni Türkçedir: önem, kural adı, hedef, değer/eşik ve açılış zamanı. `Alerting:PublicBaseUrl` doluysa alarma giden bağlantı eklenir.

#### Uptime ve SSL

- **Uptime:** HTTP(S) kontrolü kabul edilen durum kodlarına (ör. `200-299,301`) göre, TCP kontrolü porta bağlanabilmeye göre karar verir. Aralık en az `UptimeMinimumIntervalSeconds`, aynı anda en fazla `UptimeMaxConcurrency` kontrol çalışır. Detay sayfası 24 saat / 7 gün / 30 gün erişilebilirlik yüzdesini, ortalama yanıt süresini ve son 50 sonucu gösterir, 30 saniyede bir yenilenir.
- **SSL:** sertifika `SslCheckIntervalHours` aralığıyla okunur; konu, veren, geçerlilik tarihleri ve kalan gün saklanır. Durumlar: Geçerli, Yakında doluyor (`SslExpiringDays`), Süresi dolmuş, Geçersiz (zincir veya ad uyuşmazlığı; neden Türkçe açıklanır), Erişilemiyor.
- **Ağ koruması:** hedef adres çözüldükten sonra link-local (bulut metadata adresi `169.254.169.254` dahil), multicast ve belirsiz adresler reddedilir; bağlantı çözülen adrese yapılır, böylece DNS yeniden bağlama ile atlatılamaz. Loopback ve özel ağ adresleri (iç servisler için) izinlidir.
- Silme kayıtları gizler (soft delete); geçmiş sonuçlar ve alarmlar korunur.

**Saklama.** Bakım işi uptime sonuçlarını `UptimeResultRetentionDays`, bildirim teslimat kayıtlarını `DeliveryRetentionDays` gün sonra siler. Her uptime kontrolünün son 100 sonucu ve her kanalın son 50 teslimatı süre dolsa bile korunur. Silme, İzleme bölümünde anlatılan izin listesindeki tablolarla sınırlıdır; kurallar, kanallar, alarmlar, kontroller ve sertifika kayıtları silinmez.

**Audit:** `alert_rule.create/update/delete`, `alert.acknowledge`, `notification_channel.create/update/delete/test`, `uptime_check.create/update/delete`, `ssl_monitor.create/update/delete`.

| Anahtar (`Alerting:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `Enabled` | `true` | Değerlendirme, uptime ve SSL servislerini açar/kapatır |
| `EvaluationIntervalSeconds` | `60` | Kural değerlendirme aralığı (en az 15 sn) |
| `PublicBaseUrl` | boş | Bildirimlerdeki panel bağlantısı için dışarıdan görünen adres; boşsa bağlantı eklenmez |
| `NotificationTimeoutSeconds` | `15` | Tek bir bildirim gönderiminin zaman aşımı |
| `UptimeMinimumIntervalSeconds` | `30` | Uptime kontrolleri için izin verilen en kısa aralık |
| `UptimeMaxConcurrency` | `8` | Aynı anda çalışan en fazla uptime kontrolü |
| `UptimeResultRetentionDays` | `30` | Uptime sonuçlarının saklama süresi |
| `SslCheckIntervalHours` | `6` | SSL sertifikalarının okunma aralığı |
| `SslExpiringDays` | `30` | "Yakında doluyor" durumunun eşiği |
| `DeliveryRetentionDays` | `90` | Bildirim teslimat kayıtlarının saklama süresi |

| Anahtar | Varsayılan | Açıklama |
| --- | --- | --- |
| `Telegram:ApiUrl` | `https://api.telegram.org` | Telegram Bot API adresi |
| `Discord:AllowedHosts` | `discord.com`, `discordapp.com`, `ptb.discord.com`, `canary.discord.com` | Webhook adresinin gidebileceği sunucular (`host` veya `host:port`); yapılandırmadaki değerler bu listeye eklenir |
| `Discord:AllowHttp` | `false` | Yalnızca yerel test sunucusu için; üretimde açmayın |

Yerel testte gerçek Telegram/Discord/SMTP yerine yerel sahte sunucular kullanın (`Telegram__ApiUrl`, `Discord__AllowedHosts__0`, `Discord__AllowHttp=true` ortam değişkenleri ve yerel bir SMTP portu); canlı sohbetlere veya kanallara test bildirimi göndermeyin.

### Yedekleme

Menüde **Yedekleme** altında üç sekme bulunur: **İşler** (yedekleme işleri ve "Şimdi yedekle"), **Geçmiş** (tüm yedek ve geri yükleme çalışmaları, filtreli) ve **Depolama** (yedeklerin yazıldığı hedefler). Yedekler SSH üzerinden akış olarak alınır; sunucuda geçici dosya oluşmaz, veri panelden doğrudan depolamaya aktarılır.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `backup.view` | İşleri, geçmişi ve çalışma ayrıntılarını (log dahil) görüntüleme | SuperAdmin, Admin, Operator, Developer, Viewer |
| `backup.execute` | "Şimdi yedekle", süren işlemi iptal etme | SuperAdmin, Admin, Operator |
| `backup.manage` | İş ve depolama hedefi ekleme, düzenleme, silme; depolama testi; yedek dosyasını elle silme | SuperAdmin, Admin |
| `backup.restore` | Geri yükleme ve yedek dosyasını indirme | SuperAdmin, Admin |

#### Yedek türleri

| Tür | Kaynak | Sunucuda çalışan | Gereken yetki |
| --- | --- | --- | --- |
| Dosya | Bir veya daha çok mutlak yol, isteğe bağlı hariç tutma desenleri (`*.log`, `cache/`) | `tar -czf -` (GNU veya BusyBox tar) | Okunacak dosyalar için genelde tam sudo veya root |
| Docker volume | Volume adı | `alpine:3` yardımcı container'ı volume'u salt okunur bağlar ve `tar` ile arşivler (`--network none`, log kapalı) | docker grubu **veya** yalnızca `docker` için sudo |
| Veritabanı (container) | Container adı, PostgreSQL veya MySQL/MariaDB, veritabanı, kullanıcı, parola | Tek bir `docker exec -i` içinde `pg_dump` / `mariadb-dump` (yoksa `mysqldump`) ve container içindeki `gzip` | docker grubu **veya** yalnızca `docker` için sudo |
| Veritabanı (sunucu) | Host, port, veritabanı, kullanıcı, parola | Sunucudaki istemci araçları ve `gzip` | İstemci araçları kurulu olmalı |

- Veritabanı parolası komut satırına yazılmaz: stdin'den okunur ve `PGPASSWORD` / `MYSQL_PWD` ile yalnızca döküm aracına verilir. Parola `Security:MasterKey` ile şifreli saklanır ve arayüzde geri gösterilmez.
- Volume yedeğinden önce volume'un varlığı kontrol edilir; yanlış adla boş bir volume oluşturulup boş yedek alınmaz ("Volume bulunamadı").
- PostgreSQL dökümü `--clean --if-exists --no-owner --no-privileges`, MySQL dökümü `--single-transaction --routines --triggers` ile alınır.
- Volume yedeği çalışan container'ı durdurmaz. Sürekli yazılan veriler (ör. veritabanı dosyaları) için tutarlı yedek gerekiyorsa veritabanı yedeği kullanın veya yazan container'ı yedek sırasında durdurun.
- Yardımcı imaj sunucuda yoksa Docker Hub'dan çekilir; sunucunun Docker Hub'a erişimi yoksa `alpine:3` imajını önceden yükleyin. Rootless Docker'da volume'lar kullanıcının kendi Docker daemon'ındadır; iş o kullanıcıyla bağlanan sunucu kaydıyla tanımlanmalıdır.
- Sudo yetkisi yetersizse hata Türkçe açıklanır (dosya ve sunucudaki veritabanı yedekleri tam sudo, volume ve container veritabanı yedekleri yalnızca docker yetkisi ister).

#### Zamanlama, saklama, eşzamanlılık

- **Zamanlama:** Elle, saatlik (her N saatte bir, gün başına hizalı), günlük (belirli saat) veya haftalık (gün + saat). Saatler `Backup:TimeZone` saat dilimine göre yorumlanır; yaz saati geçişlerinde yerel saat korunur, olmayan saat (ileri alınan saat) bir sonraki geçerli ana kayar. Uygulama kapalıyken kaçırılan çalışmalar açılışta **bir kez** telafi edilir.
- **Eşzamanlılık:** Aynı iş için aynı anda tek işlem (yedek veya geri yükleme) çalışır; ikincisi reddedilir. Toplam eşzamanlı işlem sayısı `Backup:MaxConcurrency` ile sınırlıdır.
- **İptal ve kesinti:** İptal SSH komutunu ve depolamaya yazmayı durdurur, yarım dosya bırakılmaz (S3'te yarım multipart yükleme iptal edilir). Uygulama kapanırken süren işlemler durdurulur; açılışta yarım kalan kayıtlar "Kesildi" olarak işaretlenir.
- **Saklama:** Her işte "son N yedeği tut" (1–365) ve isteğe bağlı "N günden eskileri sil" (0 = kapalı). Saklama yalnızca yeni yedek başarıyla kaydedildikten sonra çalışır ve yalnızca o işin **başarılı yedek dosyalarını** depolamadan siler; en yeni yedek hiçbir koşulda silinmez. Çalışma kayıtları, logları ve audit geçmişi silinmez; kayıtta dosyanın "Saklama politikası" tarafından silindiği görünür.
- İş veya depolama hedefi silindiğinde (soft delete) alınmış yedekler ve geçmiş korunur; geçmiş sayfasından geri yüklenebilir.

#### Depolama hedefleri

| Tür | Ayarlar | Notlar |
| --- | --- | --- |
| Yerel disk | Klasör adı | Panel sunucusunda `Backup:LocalRootPath` altında bir klasör; kök dışına çıkılamaz. Yazma önce `.partial` dosyasına yapılır, tamamlanınca yeniden adlandırılır |
| S3 uyumlu (`Storage.S3` eklentisi) | Endpoint (AWS için boş), bölge, bucket, önek, erişim anahtarı, gizli anahtar, path-style | Amazon S3, Cloudflare R2, MinIO, Backblaze B2, SeaweedFS. 16 MiB parçalarla multipart yükleme; küçük yedekler tek istekle gider. Gizli anahtar `Security:MasterKey` ile şifrelenir ve geri gösterilmez |
| Azure Blob (`Storage.AzureBlob` eklentisi) | Hesap adı, hesap anahtarı, isteğe bağlı hizmet adresi, kapsayıcı, önek | Azure Blob Storage. Adres boşsa `https://<hesap>.blob.core.windows.net` kullanılır. Azurite için adres `http://127.0.0.1:10000/devstoreaccount1` olur. Başarısız yüklemede yarım blob silinir. Anahtar şifrelenir ve geri gösterilmez |

- **Test et** düğmesi hedefe küçük bir dosya yazar, okur ve siler.
- Nesne adı `{önek}{işKimliği}/{yyyyMMdd-HHmmss}-{kısaKimlik}.tar.gz` (veritabanında `.sql.gz`), şifreliyse sonuna `.smbk` eklenir.
- S3 bucket'ında yarım kalmış multipart yüklemeleri birkaç gün sonra temizleyen bir yaşam döngüsü kuralı (abort incomplete multipart upload) önerilir; panel hata durumunda yüklemeyi zaten iptal eder, kural yalnızca bağlantı tamamen koptuğunda kalan parçalar içindir.
- R2 için endpoint `https://<hesap>.r2.cloudflarestorage.com`, bölge `auto`; MinIO/SeaweedFS için path-style açık olmalıdır.

#### Şifreleme ve sıkıştırma

Tüm yedekler gzip ile sıkıştırılır. Şifreleme iş başına açılır (varsayılan açık) ve işe girilen **şifreleme parolasıyla** yapılır:

- Biçim (`SMBK`, sürüm 1): 36 baytlık başlık (`SMBK` | sürüm | bayrak | 2 boş bayt | PBKDF2 tur sayısı (uint32, big-endian) | 16 bayt salt | 8 bayt nonce öneki), ardından 1 MiB'lık AES-256-GCM parçaları (son-parça bayrağı | uzunluk | şifreli veri | 16 bayt etiket).
- Anahtar PBKDF2-HMAC-SHA256 (varsayılan 600.000 tur, `Backup:KeyDerivationIterations`) ile türetilir. Her parçanın nonce'u önek + parça sırasıdır; başlık, parça sırası ve son-parça bayrağı ek doğrulama verisidir. Bu sayede parçaların yer değiştirmesi, kesilme ve sona veri ekleme tespit edilir; çözme ilk bozuk parçada durur ve işlem başarısız sayılır.
- Parola işte `Security:MasterKey` ile şifreli saklanır; her çalışma kendi parolasının kopyasını tutar. İşin parolası sonradan değiştirilse bile eski yedekler kendi parolasıyla açılır.
- Çalışma ayrıntısındaki **İndir** şifreli dosyayı olduğu gibi, **Çözülmüş indir** ise panelde çözerek `.tar.gz` / `.sql.gz` olarak verir. İndirmelerde SHA-256 özeti çalışma kaydındakiyle karşılaştırılabilir.

#### Anahtar kaybı ve kurtarma planı

Yedekler panelden bağımsız açılabilir; tek gereken şifreleme parolasıdır.

1. **Şifreleme parolalarını panel dışında saklayın** (parola yöneticisi, kasa). Parola kaybolursa ve panel de yoksa şifreli yedek açılamaz; bu tasarım gereğidir.
2. **`Security:MasterKey` kaybolursa** panel kayıtlı parolaları çözemez: indirme ve geri yükleme "Yedek şifreleme parolası çözülemedi (master key değişmiş olabilir)" hatası verir; şifreli **İndir** yine çalışır. Yedek dosyaları sağlamdır; dosyayı depolamadan (S3 konsolu, `aws s3 cp`, yerel klasör) alıp aşağıdaki araçla açın. Ardından yeni MasterKey ile işin parolasını yeniden girin.
3. **Panel tamamen kaybolursa** aynı yol geçerlidir: yedek dosyası + parola yeterlidir.

```bash
python3 -m pip install cryptography
SM_BACKUP_PASSPHRASE='...' python3 tools/backup-decrypt.py yedek.tar.gz.smbk yedek.tar.gz   # parola verilmezse sorulur
tar -xzf yedek.tar.gz -C /geri/yukleme/klasoru        # dosya ve volume yedekleri
gunzip -c yedek.sql.gz | psql -d hedef_db              # PostgreSQL (MySQL: | mysql hedef_db)
```

Araç bozuk/eksik dosyada ve yanlış parolada Türkçe hata verir, yarım çıktı bırakmaz; `-` çıktısı stdout'a yazar. Şifrelenmemiş yedekler doğrudan `tar` / `gunzip` ile açılır.

#### Geri yükleme

Geçmişteki başarılı bir yedekten **Geri yükle** formu açılır; hedef sunucu yedeğin alındığı sunucudan farklı olabilir. Hedefteki verinin üzerine yazılacağı onaylanmadan işlem başlamaz.

| Tür | Hedef | Davranış |
| --- | --- | --- |
| Dosya | Mutlak klasör (varsayılan `/`; `/proc`, `/sys`, `/dev` ve `..` yasak) | Arşiv klasöre açılır, aynı adlı dosyaların üzerine yazılır; arşivde olmayan dosyalar silinmez |
| Docker volume | Volume adı (varsayılan kaynak volume) | Volume yoksa oluşturulur; içerik üzerine yazılır, fazlalık silinmez |
| Veritabanı | Container/host ve veritabanı adı | Veritabanı önceden var olmalıdır. PostgreSQL dökümü tabloları silip yeniden oluşturur; hata olursa ilk hatada durur (`ON_ERROR_STOP`) |

Hiçbir yedek veya geri yükleme komutu sunucuda dosya silmez. Geri yükleme de bir çalışma olarak geçmişe ve audit log'a yazılır. Şifreli yedekte parola yanlışsa geri yükleme hedefe veri yazmadan durur; dosya ortasında bozulma varsa bozuk parçada durur ve çalışma başarısız olur (o ana kadar açılan kısım hedefte kalır). Ayrıca depolamadan okunan dosyanın SHA-256 özeti yedek alınırken kaydedilenle karşılaştırılır.

**Alarm:** "Yedekleme başarısız" kuralı (varsayılan Kritik, kanal atanmamış) bir işin son yedeği başarısız olunca alarm açar; aynı iş başarılı yedek alınca kapanır.

**Audit:** `backup_storage.create/update/delete/test`, `backup_job.create/update/delete`, `backup.start/complete/cancel/restore/download/artifact_delete`. Gizli değerler (parolalar, anahtarlar) log'a, audit'e ve çalışma loguna yazılmaz.

| Anahtar (`Backup:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `Enabled` | `true` | Zamanlayıcıyı açar/kapatır (elle yedek yine çalışır) |
| `SchedulerIntervalSeconds` | `30` | Zamanı gelen işlerin kontrol aralığı |
| `MaxConcurrency` | `2` | Aynı anda çalışan en fazla yedek/geri yükleme |
| `TimeZone` | `Europe/Istanbul` | Günlük/haftalık zamanlamanın saat dilimi (IANA veya Windows kimliği; geçersizse UTC) |
| `BackupTimeoutMinutes` | `240` | Tek yedeğin en uzun süresi |
| `RestoreTimeoutMinutes` | `240` | Tek geri yüklemenin en uzun süresi |
| `LocalRootPath` | `App_Data/backups` | Yerel depolama klasörlerinin kökü (uygulama dizinine göre veya mutlak) |
| `KeyDerivationIterations` | `600000` | Yeni yedeklerde PBKDF2 tur sayısı (eski yedekler başlıktaki değerle açılır) |
| `MaxStoredLogKilobytes` | `256` | Çalışma başına saklanan en fazla log |

Yerel testte gerçek bucket yerine yerel bir S3 sunucusu (ör. SeaweedFS veya MinIO container'ı) ve test SSH sunucusu kullanın; canlı sunuculardan veya canlı bucket'lardan test yedeği almayın.

### Güvenlik merkezi

Menüde **Güvenlik** sayfası tüm sunucuların son tarama puanını, kritik bulgu ve uyarı sayılarını gösterir; sunucu detayındaki **Güvenlik** sekmesi tek sunucunun raporunu, bulgularını, açık portlarını ve tarama geçmişini listeler. Tarama agentless'tır: SSH ile tek bir salt okunur betik çalıştırılır, sunucuda hiçbir ayar değiştirilmez. Her bulgu için önerilen düzeltme komutu metin olarak gösterilir ve kopyalanabilir; panel bu komutları **çalıştırmaz**.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `security.view` | Güvenlik merkezi ve sunucu güvenlik raporlarını görüntüleme | SuperAdmin, Admin, Operator |
| `security.scan` | Elle tarama başlatma (dakikada en fazla 10) | SuperAdmin, Admin, Operator |

Denetlenenler:

| Kategori | Kontrol | Kaynak |
| --- | --- | --- |
| SSH | Root girişi, parola ile giriş, boş parola, `MaxAuthTries`, X11 yönlendirme, başarısız giriş sayısı ve fail2ban | `sshd -T` (yoksa `sshd_config`), `journalctl` / `auth.log` / `secure` |
| Ağ ve portlar | Dinleyen portlar, adres türü (yalnızca yerel, özel ağ, tüm arayüzler, genel), riskli servisler (veritabanı, Redis, Docker API, Telnet, FTP vb.) | `ss` veya `netstat` |
| Güvenlik duvarı | ufw, firewalld, nftables, iptables durumu ve kural sayısı | İlgili araçların durum komutları |
| Docker | TCP üzerinden açık Docker API (TLS'siz ise kritik), soket erişimi, dışarı yayınlanan container portları | `docker info`, `daemon.json`, `docker ps` |
| Güncellemeler | Bekleyen (güvenlik) güncellemeleri, yeniden başlatma gereksinimi, otomatik güncelleme | apt, dnf/yum, apk |
| Kullanıcılar | UID 0 hesapları, giriş yapabilen hesaplar, boş parolalı hesaplar, parolasız sudo kuralları | `/etc/passwd`, `/etc/group`, `/etc/shadow` (yetki varsa), sudoers |
| Disk | Şifreli disk (LUKS) varlığı | `lsblk` |

Puan 100'den başlar; her kritik bulgu 25, her uyarı 10 puan düşürür (en az 0). Özel ağ adresinde dinleyen riskli portlar bir seviye hafif değerlendirilir. Güvenlik duvarı kapalıyken dışarı açık riskli port varsa bulgu kritiktir.

Yetki: sunucuda **sudo kullan** açıksa betik önce `sudo -n` ile çalıştırılır; sudo yalnızca belirli komutlara izin veriyorsa (ör. sadece `docker`) tarama otomatik olarak yetkisiz kullanıcıyla tekrarlanır. Yetkisiz taramada `/etc/shadow`, güvenlik duvarı kuralları gibi bazı bilgiler okunamaz; rapor bu durumda kısıtlı yetkiyle tarandığını belirtir ve okunamayan kontrolleri "Bilinmiyor" olarak işaretler (puanı düşürmez).

Zamanlanmış tarama: `SecurityScanWorker` host key'i doğrulanmış sunucuları `ScanIntervalHours` aralığıyla tarar. Aynı sunucuda aynı anda tek tarama çalışır; uygulama yeniden başlarken yarım kalan taramalar başarısız olarak işaretlenir. **Kritik güvenlik bulgusu** alarm kuralı, son taramada kritik bulgu olan sunucular için alarm açar (bkz. [Alarmlar ve izleme](#alarmlar-ve-izleme)).

`appsettings.json` → `SecurityScan`:

| Ayar | Varsayılan | Açıklama |
| --- | --- | --- |
| `ScanIntervalHours` | `24` | Otomatik tarama aralığı; `0` otomatik taramayı kapatır |
| `RetentionDays` | `180` | Bu süreden eski tarama kayıtları silinir |
| `KeepLatestPerServer` | `20` | Yaşından bağımsız olarak her sunucu için korunan son tarama sayısı |

Yerel testte yalnızca test SSH container'larını tarayın; canlı sunucularda denemeler için yalnızca kendi sunucularınızı kullanın.

### İki adımlı doğrulama (2FA)

Her kullanıcı sağ üstteki menüden **Hesabım** sayfasında parolasını değiştirebilir ve authenticator uygulamasıyla (Google Authenticator, Microsoft Authenticator, 1Password vb.) TOTP tabanlı iki adımlı doğrulamayı açabilir:

1. **Kurulumu başlat** QR kod ve elle girilebilecek anahtarı gösterir.
2. Uygulamadaki 6 haneli kod girilince 2FA açılır ve **10 kurtarma kodu** bir kez gösterilir. Her kurtarma kodu tek kullanımlıktır.
3. Sonraki girişlerde parola sonrası kod istenir. "Bu tarayıcıda 30 gün boyunca sorma" seçilirse o tarayıcıda 2FA adımı 30 gün atlanır; Hesabım sayfasındaki **Bu tarayıcıyı unut** bu izni kaldırır.

2FA'yı kapatmak ve kurtarma kodlarını yenilemek parola onayı ister. Telefonunu kaybeden kullanıcı için yönetici **Kullanıcılar** sayfasından 2FA'yı sıfırlayabilir. Giriş, 2FA, kurtarma kodu kullanımı, açma/kapatma ve sıfırlama işlemleri audit log'a yazılır; kod doğrulama uç noktaları giriş rate limit'ine tabidir.

`appsettings.json` → `TwoFactor:Required` `true` yapılırsa 2FA'sı kapalı kullanıcılar giriş yaptıktan sonra yalnızca Hesabım sayfasını kullanabilir; kurulumu tamamlayınca panel açılır. Varsayılan `false`'tur (mevcut kullanıcılar kilitlenmez).

### Audit log

**Audit Log** sayfası son 24 saatin özetini (kayıt, başarısız işlem, başarısız giriş, aktif kullanıcı, en sık işlemler) ve filtrelenebilir kayıt listesini gösterir. Filtreler: serbest arama, işlem, sonuç, tarih aralığı, kullanıcı adı, IP (başı yeterli) ve hedef türü. Her kaydın ayrıntı sayfasında tarayıcı bilgisi, kullanıcı kimliği ve zincir imzası görünür. Audit kayıtları düzenlenemez ve hiçbir temizlik işiyle silinmez.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `audit.view` | Audit log listesi, özet ve ayrıntılar | SuperAdmin, Admin |
| `audit.export` | CSV dışa aktarma ve bütünlük doğrulaması | SuperAdmin, Admin |

- **CSV dışa aktarma:** Seçili filtreye uyan en yeni 50.000 kayıt Excel uyumlu (UTF-8 BOM, `;` ayraçlı) CSV olarak indirilir. `=`, `+`, `-`, `@` ile başlayan hücreler formül olarak çalışmasın diye başına `'` eklenir. Dışa aktarma işleminin kendisi de filtresiyle birlikte audit log'a yazılır.
- **Bütünlük zinciri:** Her yeni kayıt, kendi alanları ve bir önceki kaydın imzası üzerinden HMAC-SHA256 ile imzalanır (`ChainHash`). Anahtar, `Security:MasterKey`'den HKDF ile türetilir; ayrı bir gizli değer gerekmez. Bu özellikten önce yazılmış kayıtlar imzasız kalır ve zincirin başında kabul edilir.
- **Bütünlüğü doğrula:** Tüm kayıtlar Id sırasıyla kontrol edilir. Bir kayıt değiştirilmiş, silinmiş, araya kayıt eklenmiş ya da imzası kaldırılmışsa zincirin kırıldığı ilk kayıt numarası gösterilir. Doğrulama sonucu da audit log'a yazılır.

> Not: `Security:MasterKey` değiştirilirse eski kayıtların imzaları yeni anahtarla doğrulanamaz. Anahtar rotasyonundan önce doğrulamayı çalıştırıp sonucu ve CSV dökümünü saklayın.

### Sunucu sistem sekmeleri

Sunucu sayfasındaki **Services**, **Processes**, **Logs**, **Network** ve **Storage** sekmeleri bilgileri SSH ile anlık okur; ajan veya ek paket gerekmez. `UseSudo` açıksa önce `sudo -n` ile denenir, yetki yoksa normal kullanıcıyla okunur.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `system.view` | Servis, process, log, ağ ve disk bilgilerini görüntüleme | SuperAdmin, Admin, Operator, Developer |
| `system.manage` | Servis başlatma/durdurma/yeniden başlatma, process sonlandırma | SuperAdmin, Admin, Operator |

- **Services:** systemd (`systemctl`) veya OpenRC (`rc-status`) otomatik algılanır. Çalışma durumu ve açılışta başlama bilgisi gösterilir; durum ve ada göre süzülebilir. Durdurma işlemi servis adının yazılmasını ister. Servis yöneticisi olmayan sunucularda (ör. container) açıklama gösterilir.
- **Processes:** procps `ps` varsa CPU'ya göre, BusyBox'ta belleğe göre sıralı en fazla 500 process listelenir. Sonlandırma varsayılan olarak SIGTERM gönderir; onay penceresindeki seçenekle SIGKILL gönderilebilir. PID 1 sonlandırılamaz.
- **Logs:** journald varsa `journalctl` (birim ve önem filtresiyle), yoksa `/var/log` altındaki dosyalar okunur. Yalnızca `/var/log/` altındaki, `..` içermeyen yollar kabul edilir. Satır sayısı 10–2000 arasıdır; metin filtresi sunucudan dönen satırlara uygulanır.
- **Network:** arayüzler (durum, MAC, MTU, adresler, alınan/gönderilen bayt), rotalar, DNS sunucuları ve dinlenen portlar (`ss` veya `netstat`).
- **Storage:** dosya sistemlerinin doluluk ve inode kullanımı (sanal dosya sistemleri gizlenir) ile `lsblk` varsa diskler ve bölümler.

Servis kontrolü ve process sonlandırma audit log'a yazılır (`system.service_control`, `system.process_signal`). Bu işlemler kullanıcı başına dakikada 20 istekle sınırlıdır.

**Backups**, **Alerts** ve **Activity** sekmeleri ilgili modülün listesini yalnızca bu sunucuya süzerek gösterir: sunucunun yedekleme işleri ve yedek geçmişi (`backup.view`), sunucuya ait alarmlar (`alert.view`) ve sunucu üzerinde yapılan işlemlerin audit kayıtları (`audit.view`). Sekmeler yalnızca ilgili izni olan kullanıcılara görünür.

### Sunucu grupları, maliyet ve sunucu seçici

**Gruplar** sayfası (`/ServerGroups`) sunucuları proje, müşteri veya rol bazında toplar. Bir sunucu en fazla bir gruptadır; grup silinirse sunucular silinmez, yalnızca gruptan çıkarılır (grup kaydı da yumuşak silinir). Görüntüleme `server.view`, ekleme/düzenleme/silme `server.edit` izni ister ve her değişiklik audit log'a yazılır (`server_group.create/update/delete`).

- Sunucu formunda grup, **aylık maliyet** (0–1.000.000) ve para birimi (USD, EUR, TRY, GBP) seçilir. Maliyet girilip para birimi seçilmezse USD kabul edilir.
- Sunucu listesi gruba göre süzülebilir; grup kartlarında sunucu sayısı, erişilebilir sunucu sayısı ve para birimine göre toplam aylık maliyet görünür.
- Menüdeki **Docker, İmajlar, Volume'lar, Ağlar, Terminal, Dosyalar, Servisler, Process'ler, Loglar** ve **Metrikler** bağlantıları önce sunucu seçiciyi açar; seçilen sunucunun ilgili sekmesine gider. Seçici, kullanıcının o özellik için izni yoksa menüde görünmez.

### Toplu komut ve şablonlar

**Toplu komut** sayfası (`/CommandRuns`) bir komutu veya çok satırlı betiği seçilen sunucularda aynı anda çalıştırır. Komut her sunucuda `sh -c '<betik>' 2>&1` olarak, en fazla 5 sunucuda paralel çalışır; stdout ve stderr yazıldığı sırayla birleştirilir.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `command.view` | Komut geçmişini ve çıktıları görüntüleme, şablon listesini görme | SuperAdmin, Admin, Operator |
| `command.run` | Sunucularda toplu komut çalıştırma | SuperAdmin, Admin |
| `template.manage` | Betik ve cloud-init şablonu ekleme/düzenleme/silme | SuperAdmin, Admin |

- Hedefler tek tek veya grup düğmesiyle seçilir (en fazla 100 sunucu). Zaman aşımı 5–900 saniye, komut en fazla 8000 karakterdir.
- Çalıştırmadan önce onay penceresi komutu ve sunucuları gösterir; 5 veya daha fazla sunucuda sunucu sayısının yazılması istenir.
- **sudo ile çalıştır** yalnızca sudo kullanımı açık sunuculara uygulanır; diğer sunucularda komut yetkisiz çalıştırılmaz, "başarısız" olarak işaretlenir.
- Her sunucunun durumu (başarılı, başarısız, zaman aşımı, kesildi), çıkış kodu, süresi ve çıktısının son 32.000 karakteri saklanır. Ayrıntı sayfası çalışma sürerken kendiliğinden yenilenir.
- Komut HTTP isteğinden bağımsız arka planda çalışır. Uygulama kapanırsa süren komutlar "kesildi" olarak kapanır; açılışta önceki çalışmadan yarım kalan kayıtlar da "kesildi" olarak işaretlenir. Komut geçmişi silinmez.
- Her çalıştırma audit log'a `command.run` olarak (sunucular, sudo bilgisi ve komut metniyle) yazılır.

**Şablonlar** (`/ServerTemplates`) iki türdür:

- **Kabuk betiği:** Toplu komut formunda seçildiğinde içeriği komut alanına kopyalanır ("sudo gerektirir" işaretliyse sudo kutusu da işaretlenir); çalıştırmadan önce düzenlenebilir. Şablon listesindeki çalıştır düğmesi formu bu şablonla açar.
- **cloud-init:** Bulut sağlayıcıda yeni sunucu oluşturulurken user-data olarak verilir; içerik `#cloud-config` veya `#!` ile başlamalıdır.

Şablon içeriği şifrelenmeden saklanır; parola veya API anahtarı yazılmamalıdır. Silinen şablon yumuşak silinir; o şablonla çalıştırılmış komutların geçmişi korunur.

### Bulut sağlayıcıları

**Sağlayıcılar** sayfası (`/CloudAccounts`) Hetzner Cloud, DigitalOcean, Vultr, Linode ve Scaleway hesaplarını bağlar. Her sağlayıcı ayrı bir eklentidir (`Cloud.Hetzner`, `Cloud.DigitalOcean`, `Cloud.Vultr`, `Cloud.Linode`, `Cloud.Scaleway`, grup "Bulut"); kullanmadan önce **Eklentiler** sayfasından kurulup etkinleştirilmelidir. Eklentisi kapatılan hesap listede görünür, ancak eşitleme ve sunucu oluşturma çalışmaz.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `cloud.view` | Hesapları ve sağlayıcıdaki sunucu listesini görüntüleme | SuperAdmin, Admin, Operator |
| `cloud.manage` | Hesap ekleme, düzenleme, silme ve eşitleme | SuperAdmin, Admin |
| `cloud.provision` | Sağlayıcıda yeni sunucu oluşturma | SuperAdmin, Admin |

- **Hesap ekleme:** API anahtarı kaydedilmeden önce sağlayıcıda doğrulanır ve AES-256-GCM ile şifrelenerek saklanır; bir daha gösterilmez. Düzenlemede anahtar alanı boş bırakılırsa mevcut anahtar korunur. Listeleme için okuma, sunucu oluşturma için yazma yetkili anahtar gerekir.
- **Sunucu listesi** sağlayıcıdan anlık okunur. Panele bağlı olmayan sunucularda **Panele ekle** sunucu formunu ad, IP, konum ve aylık fiyatla doldurur (kullanıcı adı `root`); SSH bilgileri girilip kaydedildiğinde sunucu hesaba bağlanır (`server.create` izni gerekir, `cloud.import` olarak audit log'a yazılır).
- **Eşitleme** (elle veya `Cloud:SyncIntervalHours` aralığında otomatik, varsayılan 6 saat, `0` kapatır):
  - Hiçbir hesaba bağlı olmayan ve IP adresi sağlayıcıdaki bir sunucuyla **tam olarak bir** kayıtta eşleşen panel sunucusu hesaba bağlanır. Aynı IP birden fazla panel sunucusunda varsa bağlama yapılmaz.
  - Bağlı sunucuların aylık maliyeti ve para birimi sağlayıcı fiyatından güncellenir. Hetzner fiyatı sunucunun konumundaki **KDV hariç (net)** aylık ücrettir (EUR). DigitalOcean, Vultr ve Linode aylık ücrettir (USD). Scaleway ürünü aylık EUR yayınlamazsa saatlik ücret 730 ile çarpılır. Fiyat dönmeyen sunucunun elle girilmiş maliyeti değiştirilmez.
  - Son eşitleme zamanı ve hatası hesap kartında görünür. Elle eşitleme her zaman, otomatik eşitleme yalnızca değişiklik olduğunda audit log'a `cloud.sync` olarak yazılır.
- **Sunucu oluşturma** (`/CloudAccounts/Provision`): bölge, sunucu tipi (aylık fiyatıyla, seçilen bölgede satılanlar) ve işletim sistemi imajı sağlayıcıdan yüklenir. İsteğe bağlı cloud-init şablonu user-data olarak verilir (en fazla 32 KB). SSH genel anahtarı girilirse root kullanıcısına eklenir: şablonsuzsa `#cloud-config` + `ssh_authorized_keys`, cloud-config şablonda mevcut listeye eklenerek, `#!` betikte shebang'den hemen sonra kurulum komutlarıyla. Onay penceresinde sunucu adının yazılması istenir.
- Anahtar verilmezse Hetzner **tek seferlik root parolası** döndürür; Vultr `default_password` alanını, Linode ise panelin ürettiği root parolasını aynı pencerede bir kez gösterir. Bu parolalar veritabanına ve audit log'a yazılmaz. DigitalOcean parolayı hesabın e-postasına gönderir. Scaleway parola döndürmez. Linode adı harfle başlamalı, 3–64 karakter olmalı ve nokta içermemelidir. Oluşturma (başarılı veya başarısız) `cloud.provision` olarak audit log'a yazılır.
- Hesap silindiğinde kayıt yumuşak silinir ve bağlı panel sunucularının hesap bağlantısı kaldırılır; sağlayıcıdaki sunuculara ve panel sunucu kayıtlarına dokunulmaz.

Sağlayıcı API adresleri `Cloud:Hetzner:ApiUrl` (varsayılan `https://api.hetzner.cloud/v1/`), `Cloud:DigitalOcean:ApiUrl` (`https://api.digitalocean.com/v2/`), `Cloud:Vultr:ApiUrl` (`https://api.vultr.com/v2/`), `Cloud:Linode:ApiUrl` (`https://api.linode.com/v4/`) ve `Cloud:Scaleway:ApiUrl` (`https://api.scaleway.com/`) ile değiştirilebilir; yerel testlerde sahte bir API'ye yönlendirmek için kullanılır. İstekler yönlendirme izlemeyen ayrı HTTP istemcileriyle yapılır; 401/403/429 yanıtları Türkçe hata mesajına çevrilir. Scaleway gizli anahtarı Bearer yerine `X-Auth-Token` başlığında gider.

### Maliyet raporu

**Maliyet** sayfası (`/Costs`, `server.view`) sunucuların aylık maliyetini para birimine göre toplar ve yıllık tahmini gösterir; kur çevrimi yapılmaz. Rapor gruplara ve kaynağa (bulut hesabı veya "Elle girilen") göre kırılır, maliyeti girilmemiş sunucuları ayrıca işaretler ve sunucu düzenleme ekranına bağlantı verir.

### Agent

Panelin SSH ile ulaşamadığı sunucular (NAT arkası, kapalı SSH) için sunucuda çalışan küçük bir agent metrik gönderir. Kurulum ve durum, sunucunun **Metrikler** sekmesinin altındaki panelden yönetilir (`server.view` görür, `server.edit` token üretir ve iptal eder).

- **Token** (`sma_` + 43 karakter) yalnızca oluşturulduğu anda, kurulum komutunun içinde bir kez gösterilir. Veritabanında SHA-256 özeti saklanır; düz metin loglanmaz. Yeni token eskisini geçersiz kılar. İptal `agent.token_revoke`, oluşturma `agent.token_create` olarak audit log'a yazılır.
- Kurulum komutu `GET /api/agent/install.sh` betiğini indirir. Betik token içermez; `SM_URL` ve `SM_TOKEN` ortam değişkenleriyle root olarak çalıştırılır. systemd varsa dakikada bir zamanlayıcı, yoksa cron kurulur. Kaldırma: aynı betik `uninstall` argümanıyla.
- Agent, SSH toplayıcısıyla aynı ölçüm betiğini yerelde çalıştırıp çıktıyı `POST /api/agent/report` ile gönderir (Bearer token, `X-Agent-Version`). Rapor metrik, anlık görüntü ve sağlık kaydı olarak SSH ile toplanmış gibi yazılır. İki rapor arasında en az 20 saniye olmalıdır; izleme kapalıysa rapor reddedilir.
- Son rapor 3 dakika içindeyse o sunucudan SSH ile ayrıca metrik toplanmaz. Host key doğrulanmamış ve agent'ı 5 dakikadır sessiz olan sunucu için başarısız sağlık kaydı yazılır; ardışık başarısızlık eşiği dolunca sunucu çevrimdışı olur.
- Rapor uç noktası oturumsuzdur ve IP başına dakikada 120 istekle sınırlıdır. Gövde en fazla 256 KB olabilir. Panel HTTP üzerinden açıldıysa metrik sayfası token'ın ağda şifresiz gideceğini uyarır.

### Ayarlar

**Ayarlar** sayfası (`/Settings`, `settings.view`, SuperAdmin ve Admin) sürüm, çalışma ortamı, süreç süresi ve veritabanı şema sürümünü gösterir. `settings.manage` (SuperAdmin ve Admin) izleme, alarm, yedekleme, güvenlik taraması ve bulut aralıklarını kaydeder. Kayıt `PanelSettings` tablosuna yazılır ve çalışan sürecin seçenek nesnesine uygulanır; toplayıcı, alarm, yedek zamanlayıcı, uptime ve SSL bir sonraki turda yeni değeri kullanır. Agent aralığı kurulum betiğine gömülüdür, bu sayfadan değişmez. Bağlantı dizesi, ana anahtar ve API anahtarları gösterilmez ve kaydedilmez. Değişiklik `settings.update` olarak audit log'a yazılır.

### Eklentiler

Sistem nopCommerce'teki plugin mantığıyla genişler: Dokploy, Dokku ve diğer DevOps araçları host'tan bağımsız birer eklentidir. Her eklenti `Plugins/{SystemName}/` klasöründe `plugin.json` tanımı, derlenmiş assembly'si (controller, derlenmiş Razor view'ları, servisler, migration'lar) ve `Content/` klasörüyle (CSS/JS) durur.

**Yönetim → Eklentiler** sayfası (`plugin.manage`, yalnızca SuperAdmin) bulunan eklentileri grup halinde listeler:

| İşlem | Davranış |
| --- | --- |
| Kur | Eklentinin migration'ları çalışır, izinleri varsayılan rollere eklenir, kayıt `InstalledPlugins` tablosuna yazılır ve eklenti etkinleşir. |
| Devre dışı bırak | Controller'ları 404 döner, SignalR hub'ları bağlantıyı reddeder, sunucu sekmesi gizlenir, arka plan işleri durur. **Tablolar ve veriler silinmez.** |
| Etkinleştir | Eklenti kaldığı yerden çalışır. |

- Kurma, etkinleştirme ve devre dışı bırakma yeniden başlatma gerektirmez; yeni bir eklenti klasörü eklemek veya eklenti dll'ini güncellemek yeniden başlatma gerektirir.
- Kaldırma (uninstall) yoktur: eklenti verisi kullanıcı verisidir ve silinmez. Bir eklentiyi tamamen kapatmak için devre dışı bırakın.
- Açılışta kurulu her eklentinin yeni migration'ları ve eksik izinleri uygulanır; `plugin.json` sürümü değiştiyse kayıttaki sürüm güncellenir.
- Yüklenemeyen eklenti (bozuk `plugin.json`, eksik dll, aynı `SystemName`) uygulamayı durdurmaz; sayfada hatasıyla listelenir ve loglanır.
- İşlemler audit log'a `plugin.install`, `plugin.enable`, `plugin.disable` olarak yazılır.

| Anahtar (`Plugins:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `Directory` | `Plugins` | Eklenti klasörlerinin bulunduğu dizin (uygulama kök dizinine göre) |
| `InstallOnStartup` | `[]` | Daha önce hiç kurulmamışsa açılışta otomatik kurulan eklentiler; varsayılan boştur, sonradan devre dışı bırakılan eklenti yeniden etkinleştirilmez |

Tablo gerektirmeyen eklentilerde (bildirim kanalları gibi) migration adımı atlanır.

| Eklenti | Grup | Durum |
| --- | --- | --- |
| `DevOps.Dokploy` | DevOps | Hazır (bkz. [Dokploy](#dokploy)) |
| `Git.GitHub` | Git | Hazır (bkz. [GitHub App](#github-app)) |
| `Notifications.Email` | Bildirim | Hazır (bkz. [Bildirim kanalları](#bildirim-kanalları)) |
| `Notifications.Telegram` | Bildirim | Hazır (bkz. [Bildirim kanalları](#bildirim-kanalları)) |
| `Notifications.Discord` | Bildirim | Hazır (bkz. [Bildirim kanalları](#bildirim-kanalları)) |
| `Storage.S3` | Yedekleme | Hazır (bkz. [Yedekleme](#yedekleme)) |
| `Storage.AzureBlob` | Yedekleme | Hazır (bkz. [Yedekleme](#yedekleme)) |
| `DevOps.Dokku` | DevOps | Hazır (bkz. [Dokku](#dokku)) |
| `Git.GitLab` | Git | Planlandı |

### GitHub App

> Eklenti: `Git.GitHub` (`src/Plugins/ServerManager.Plugin.Git.GitHub`). Açılışta otomatik kurulmaz. Devre dışı bırakılırsa menü ve sayfa kapanır, bağlantılı projelerin deployment'ı durur; kayıtlar silinmez.

GitHub hesaplarındaki veya kurumlarındaki depoları projelere erişim anahtarı girmeden tanıtır. Menüde **Deployment → GitHub** sayfası bulunur (`github.manage`, varsayılan SuperAdmin ve Admin).

Bağlantı kurma:

1. **GitHub'da oluştur (manifest):** uygulama adı ve isteğe bağlı kurum adı girilir; panel GitHub'a hazır bir App tanımı gönderir. GitHub'da "Create GitHub App" onaylanınca uygulama oluşur, kimlik bilgileri panele döner ve şifrelenerek saklanır; ardından kurulum sayfası açılır.
2. **Elle ekle:** GitHub'da oluşturulmuş bir App'in **App ID**'si ve **private key** (`.pem`, en az 2048 bit RSA) girilir; kaydetmeden önce GitHub'da doğrulanır.
3. **Hesaba kur:** uygulama bir veya daha fazla hesaba/kuruma kurulur ve depo erişimi seçilir (tümü veya seçili depolar). Kurulumdan sonra GitHub panele geri yönlendirir ve liste yenilenir.

Sayfa her uygulamanın kurulumlarını (hesap, tür, depo erişimi, askıya alınmış mı) ve onu kullanan projeleri gösterir. Proje formundaki **Kaynak** listesinde her kurulum "hesap · uygulama" olarak çıkar.

- **İzinler:** manifestle oluşturulan uygulama yalnızca `contents: read` ve `metadata: read` ister; webhook tanımlanmaz. Elle eklenen uygulamaya da bundan fazlası gerekmez.
- **Erişim anahtarları:** App private key ile 10 dakikadan kısa ömürlü bir JWT imzalanır, buradan kurulum anahtarı alınır. Deployment ve depo kontrolü için anahtar yalnızca o depoya ve okuma iznine daraltılır, önbelleğe alınmaz, kullanıcı adı `x-access-token` ile stdin üzerinden git'e verilir; projede, logda ve sunucuda diske yazılmaz. Depo/kurulum listeleri `ListCacheSeconds` süresince önbellekte tutulur; **Yenile** düğmesi ve kurulum dönüşü önbelleği temizler.
- **Gizli bilgiler:** private key, client secret ve webhook secret `Security:MasterKey` ile şifrelenir, arayüzde gösterilmez.
- **Kaldırma:** bir uygulamayı kullanan proje varsa kaldırılamaz. Kaldırma kaydı gizler (soft delete); GitHub'daki uygulamayı ve kurulumları silmez, bunlar GitHub ayarlarından kaldırılır.
- **Geri dönüş adresi:** GitHub'a bildirilen dönüş adresleri `GitHub:PublicBaseUrl`'den (boşsa isteğin adresinden) üretilir. Panel ters vekil arkasındaysa veya GitHub'ın kullanıcıyı döndüreceği adres farklıysa bu ayarı doldurun.
- **Oturum çerezi:** giriş çerezi `SameSite=Strict` olduğundan GitHub'dan gelen yönlendirmede gönderilmez. Bu yüzden `GitHub/Callback` ve `GitHub/Setup` girişsiz bir ara sayfa döner; sayfa aynı siteden `CompleteManifest`/`Installed` adreslerine geçer ve işlem orada oturum ve `github.manage` iznine göre yapılır. Manifest isteği tek kullanımlıktır, 1 saat geçerlidir ve başlatan kullanıcıya bağlıdır.
- **CSP:** manifest formunun GitHub'a gönderilebilmesi için yalnızca GitHub sayfasında `form-action` politikasına `GitHub:WebUrl` eklenir (`HttpContext.AllowFormAction`).
- **Audit:** `github.app_create`, `github.app_delete`. Aksiyonlar kullanıcı başına dakikada 20 istekle sınırlandırılır.

| Anahtar (`GitHub:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `ApiUrl` | `https://api.github.com` | GitHub REST API adresi (GitHub Enterprise için değiştirin) |
| `WebUrl` | `https://github.com` | Manifest ve kurulum sayfalarının adresi |
| `PublicBaseUrl` | boş | Panelin dışarıdan görünen adresi; boşsa isteğin adresi kullanılır |
| `HttpTimeoutSeconds` | `15` | GitHub API isteklerinin zaman aşımı |
| `ListCacheSeconds` | `60` | Kurulum ve depo listelerinin önbellek süresi |

Yerel test için gerçek GitHub yerine sahte bir API kullanın (`GitHub__ApiUrl` ve `GitHub__WebUrl` ortam değişkenleriyle); canlı hesaplarda test uygulaması oluşturmayın.

### Eklenti geliştirme

Aşağıdaki adımlar örnek bir **Dokku** eklentisi üzerinden anlatılır; yeni bir DevOps aracı için host kodunda değişiklik gerekmez.

**1. Proje** — `src/Plugins/ServerManager.Plugin.DevOps.Dokku/ServerManager.Plugin.DevOps.Dokku.csproj`. Proje adı `ServerManager.Plugin.{SystemName}` olmalıdır; çıktı klasörü bu addan türetilir. Projeyi `ServerManager.slnx` içindeki `/src/Plugins/` klasörüne ekleyin; Web projesi `src/Plugins/*/*.csproj` projelerini otomatik olarak önce derler (assembly referansı vermeden).

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\ServerManager.Infrastructure\ServerManager.Infrastructure.csproj" Private="false" />
    <ProjectReference Include="..\..\ServerManager.Web.Framework\ServerManager.Web.Framework.csproj" Private="false" />
  </ItemGroup>
</Project>
```

`src/Plugins/Directory.Build.props` ve `Directory.Build.targets` ortak ayarları getirir: çıktı `src/ServerManager.Web/Plugins/{SystemName}/` klasörüne yazılır, `plugin.json` ve `Content/**` kopyalanır, host'ta zaten bulunan assembly ve paket dosyaları çıktıya alınmaz. Eklentiye özel bir NuGet paketi gerekiyorsa projede `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` açılır; paket dll'i eklenti klasöründen yüklenir (host'ta bulunan paketler her zaman host'tan yüklenir).

**2. `plugin.json`**

```json
{
  "SystemName": "DevOps.Dokku",
  "FriendlyName": "Dokku",
  "Group": "DevOps",
  "Version": "1.0.0",
  "Author": "Server Manager",
  "Description": "Dokku kurulumu, uygulama listesi ve deploy durumu.",
  "DisplayOrder": 2,
  "AssemblyFileName": "ServerManager.Plugin.DevOps.Dokku.dll"
}
```

`SystemName` yalnızca harf, rakam ve nokta içerir, sonradan değiştirilmez (kurulum kaydı, statik dosya adresi `/plugins/devops.dokku/` ve audit kayıtları buna bağlıdır). İsteğe bağlı `Logo` alanı `Content` içindeki dosya adıdır (ör. `logo.svg`); Eklentiler sayfasında adın yanında gösterilir. Yol, `..` veya başka bir klasör kabul edilmez.

**3. Kimlik sabitleri ve sağlayıcılar** — izin ve audit adları kalıcıdır, değiştirilmez:

```csharp
public static class DokkuPermissions
{
    public const string View = "dokku.view";
    public const string Manage = "dokku.manage";
}

public sealed class DokkuPermissionProvider : IPermissionProvider
{
    public IReadOnlyList<PermissionDefinition> GetPermissions() =>
    [
        new(DokkuPermissions.View, "Dokku uygulamalarını görüntüleme"),
        new(DokkuPermissions.Manage, "Dokku uygulamalarını yönetme")
    ];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetDefaultRolePermissions() => new Dictionary<string, IReadOnlyList<string>>
    {
        [Roles.Admin] = [DokkuPermissions.View, DokkuPermissions.Manage],
        [Roles.Operator] = [DokkuPermissions.View]
    };
}
```

SuperAdmin eklentinin tüm izinlerini otomatik alır. Audit etiketleri `IAuditActionProvider` ile (`["dokku.app_restart"] = "Dokku uygulaması yeniden başlatıldı"`), sunucu detay sekmesi `IServerTabProvider` ile tanımlanır:

```csharp
public IEnumerable<ServerTab> GetTabs() =>
[
    new ServerTab("dokku", "Dokku", "Dokku uygulamaları", "Sunucudaki Dokku uygulamalarını görün ve yönetin.",
        DokkuPermissions.View, Controller: "Dokku", Order: 20)
];
```

Sunucuya bağlı olmayan sayfalar için sol menüye bağlantı `IMenuItemProvider` ile eklenir; bağlantı yalnızca eklenti etkinse ve kullanıcının izni varsa görünür:

```csharp
public IEnumerable<MenuItem> GetItems() =>
[
    new(MenuGroups.Deployment, "Dokku", "Dokku uygulamaları", "server", DokkuPermissions.View, "Dokku")
];
```

Projelere depo tanıtan Git sağlayıcıları (GitLab, Gitea vb.) `ServerManager.Application.Interfaces.Deployments.IGitIntegration` arayüzünü uygulayıp scoped olarak kaydeder; proje formu, dal listesi ve deployment bu arayüz üzerinden çalışır (örnek: `GitHubGitIntegration`).

Her tip kendi dosyasındadır (bir dosyada tek tip kuralı eklentiler için de geçerlidir).

**4. Giriş noktası** — `IPluginStartup` uygulayan sınıf açılışta bulunur ve çağrılır:

```csharp
public sealed class DokkuStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DokkuOptions>(configuration.GetSection("Dokku"));
        services.AddValidatorsFromAssembly(typeof(DokkuStartup).Assembly, includeInternalTypes: true);

        services.AddSingleton<IPermissionProvider, DokkuPermissionProvider>();
        services.AddSingleton<IAuditActionProvider, DokkuAuditActionProvider>();
        services.AddSingleton<IServerTabProvider, DokkuServerTabProvider>();

        services.AddSingleton<IDokkuProvider, SshDokkuProvider>();
        services.AddScoped<IDokkuRepository, DokkuRepository>();
        services.AddScoped<IDokkuService, DokkuService>();
        services.Configure<RateLimiterOptions>(o => o.AddPerUserPolicy("dokku-action", 20));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapHub<DokkuHub>("/hubs/dokku");   // gerekiyorsa
}
```

`ConfigureServices` eklenti devre dışıyken de çalışır. Controller, hub ve sekmeler otomatik olarak engellenir; `BackgroundService` yazıyorsanız her turda `IPluginCatalog.IsEnabled(SystemName)` ile kontrol edin.

**5. Veri** — entity'ler eklentidedir; `IEntityTypeConfiguration<T>` sınıfları `ApplicationDbContext`'e otomatik eklenir, repository `_context.Set<DokkuApp>()` kullanır. Şema yalnızca FluentMigrator ile değişir: migration sınıfları eklenti assembly'sinde durur ve kurulumda/açılışta çalışır. Sürüm numarası tüm uygulamada benzersiz bir zaman damgası olmalıdır (`[Migration(202611150001, "Dokku tabloları")]`); host tablolarına (ör. `Servers`) yalnızca foreign key ile bağlanın, host tablolarını değiştirmeyin. Şema değişiklikleri yalnızca eklemelidir.

**6. Web** — controller'lar normal MVC controller'larıdır (`[HasPermission(DokkuPermissions.View)]`, JSON yanıtları `ApiResponse` ile). Sunucu sayfası için `ServerPageBuilder` ve `_ServerHeader` partial'ı kullanılır. View'lar `Views/Dokku/*.cshtml` altında derlenir; `Views/_ViewImports.cshtml` eklentinin ve `ServerManager.Web.Framework.*` namespace'lerini içe aktarır. Sayfa view'ında:

```cshtml
@{
    ViewData["Page"] = "dokku";
    ViewData[PluginContent.PagePluginKey] = DokkuPlugin.SystemName;
}
```

**7. Stil ve JavaScript**

| Yol | Açıklama |
| --- | --- |
| `Styles/pages/dokku.scss` | Sayfa stili; `npm run build:css` bunu host temasıyla derleyip `Content/css/pages/dokku.css` dosyasına yazar (derlenmiş CSS repoya dahildir) |
| `Content/js/pages/dokku.js` | Sayfanın giriş modülü |
| `Content/js/features/*.js` | Eklentinin kendi modülleri (göreli import: `../features/app-list.js`) |

Host modülleri `@app/` önekiyle içe aktarılır; import map bunu sürüm parametreli host adresine çevirir:

```js
import { qs, on } from '@app/core/dom.js';
import { postForm, failureMessage } from '@app/core/http.js';
import { notify } from '@app/core/notify.js';
import { confirmAction } from '@app/core/dialog.js';
```

Frontend kuralları host ile aynıdır: yalnızca ES module, inline script/style yok, tüm işlemler AJAX, onay SweetAlert2, bildirim toastr, her buton/bağlantıda `title` tooltip'i, sayfa başında kısa açıklama.

**8. Test** — `tests/ServerManager.Plugin.DevOps.Dokku.Tests` projesi oluşturun (Dokploy test projesini örnek alın) ve `ServerManager.slnx`'e ekleyin. İzin/audit adlarının ve `plugin.json`'ın assembly ile eşleştiğini doğrulayan sözleşme testi önerilir (`DokployPluginContractTests`).

**9. Derleme ve yayın** — `dotnet build` eklentiyi `src/ServerManager.Web/Plugins/DevOps.Dokku/` klasörüne yazar (git'e eklenmez). `dotnet publish src/ServerManager.Web` tüm eklenti klasörlerini yayın çıktısındaki `Plugins/` dizinine kopyalar. Hazır bir eklentiyi çalışan bir kuruluma eklemek için klasörünü `Plugins/` altına kopyalayıp uygulamayı yeniden başlatmak ve **Eklentiler** sayfasından kurmak yeterlidir.

### Güvenlik notları

- Tüm sayfalar varsayılan olarak giriş gerektirir; her controller/action izin kontrolüyle korunur.
- Tüm POST istekleri antiforgery token ile doğrulanır; AJAX istekleri `RequestVerificationToken` başlığını gönderir.
- Giriş, bağlantı testi ve anlık metrik toplama uç noktaları rate limit ile sınırlandırılır (dakikada 10 istek).
- İki adımlı doğrulama (TOTP) desteklenir; `TwoFactor:Required` ile zorunlu hale getirilebilir. Audit kayıtları HMAC zinciriyle imzalanır ve panelden doğrulanabilir.
- 5 başarısız girişte hesap 15 dakika kilitlenir. Pasifleştirilen veya kilitlenen kullanıcının açık oturumu en geç 1 dakika içinde sonlanır.
- SSH host key fingerprint ilk başarılı bağlantıda kaydedilir; sonraki bağlantılarda farklı bir anahtar gelirse bağlantı reddedilir. Sunucu adresi veya portu değiştirildiğinde fingerprint sıfırlanır.
- Sunucu silme işlemi sunucu adının birebir yazılmasıyla onaylanır ve soft delete olarak yapılır; audit kayıtları korunur.
- Gizli değerler loglara, audit kayıtlarına ve HTML çıktısına yazılmaz.

### Faydalı komutlar

```bash
docker compose logs -f db                         # MSSQL logları
docker compose down                               # veritabanını durdur (veri volume'de kalır)
dotnet user-secrets list --project src/ServerManager.Web
```
