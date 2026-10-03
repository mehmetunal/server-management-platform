# Server Management Platform
## Sunucu Yönetim, Docker, Dokploy ve Terminal Yönetim Sistemi

> Bu proje; birden fazla Linux sunucunun tek bir web panelinden güvenli şekilde tanımlanmasını, izlenmesini ve yönetilmesini amaçlayan profesyonel bir Server Management Platform'dur.

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
    └── ServerManager.Plugin.DevOps.Dokku/      # örnek: ileride eklenecek
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

## Phase 8 — Backup

- Docker volume backup
- Database backup
- File backup
- S3/R2
- Restore

## Phase 9 — Security

- Security center
- SSH audit
- Port audit
- Firewall status
- 2FA
- Advanced audit

## Phase 10 — Advanced

- Agent
- Multi-server command runner
- Cloud provider integration
- Cost monitoring
- Server provisioning
- Server templates

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
| Admin | Dashboard, sunucu görüntüleme/ekleme/düzenleme/silme/bağlantı testi, tüm Docker, terminal, dosya ve deployment izinleri, audit log |
| Operator | Dashboard, sunucu görüntüleme, bağlantı testi, Docker görüntüleme/başlatma/durdurma/yeniden başlatma/terminal, sunucu terminali, dosya görüntüleme/oluşturma/düzenleme/yükleme/indirme, deployment görüntüleme/çalıştırma |
| Developer | Dashboard, sunucu görüntüleme, Docker görüntüleme/yeniden başlatma, dosya görüntüleme/indirme, deployment görüntüleme/çalıştırma |
| Viewer | Dashboard, sunucu görüntüleme, Docker görüntüleme, deployment görüntüleme |

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
- Silme yalnızca geçici metrik tablolarında (`ServerMetrics`, `ServerMetricsHourly`, `ServerHealthChecks`) çalışır; bu liste dışında bir tablo veya sütun istenirse işlem hata verip durur.
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

> Eklenti: `DevOps.Dokploy` (`src/Plugins/ServerManager.Plugin.DevOps.Dokploy`). Varsayılan olarak `Plugins:InstallOnStartup` listesindedir; devre dışı bırakılırsa sekme, sayfalar, hub ve sağlık kontrolü durur, kayıtlar silinmez.

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

### Deployment

Git deposundaki bir uygulamayı kayıtlı bir sunucuya SSH üzerinden (agentless) dağıtır. Menüde **Projeler** ve **Deployment'lar** sayfaları, sunucu detayında **Deployments** sekmesi bulunur. Bu özellik çekirdeğin parçasıdır; Dokploy gibi üçüncü taraf araçlar eklenti olarak ayrı kalır.

| İzin | Kapsam | Varsayılan roller |
| --- | --- | --- |
| `deployment.view` | Projeler, deployment geçmişi, canlı çıktı ve log indirme | SuperAdmin, Admin, Operator, Developer, Viewer |
| `deployment.manage` | Proje ekleme, düzenleme, silme (Git erişim anahtarı, ortam değişkenleri, build/deploy komutları dahil) | SuperAdmin, Admin |
| `deployment.execute` | Deployment başlatma, iptal etme, yeniden dağıtma, depo/dal kontrolü | SuperAdmin, Admin, Operator, Developer |

> `deployment.manage` fiilen sunucuda komut çalıştırma yetkisidir: "Komutlar" build türündeki komutlar ve Dockerfile/compose içeriği hedef sunucuda çalışır. Bu izni yalnızca sunucuya terminal erişimi verilebilecek kişilere verin.

Proje ayarları:

- **Git:** sağlayıcı (GitHub, GitLab, Bitbucket, kendi sunucusu), `https://` veya SSH depo adresi, dal, isteğe bağlı kullanıcı adı ve erişim anahtarı (token). SSH adreslerinde hedef sunucudaki kullanıcının anahtarı depoda yetkili olmalıdır.
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

| Anahtar (`Deployment:`) | Varsayılan | Açıklama |
| --- | --- | --- |
| `GitTimeoutSeconds` | `300` | Depo kontrolü, fetch ve checkout zaman aşımı |
| `BuildTimeoutMinutes` | `30` | Build adımının zaman aşımı |
| `DeployTimeoutMinutes` | `10` | Deploy adımının zaman aşımı |
| `MaxStoredLogKilobytes` | `1024` | Deployment kaydında saklanan log (son kısım; çalışırken 10 saniyede bir kaydedilir) |

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
| `InstallOnStartup` | `["DevOps.Dokploy"]` | Daha önce hiç kurulmamışsa açılışta otomatik kurulan eklentiler; sonradan devre dışı bırakılan eklenti yeniden etkinleştirilmez |

| Eklenti | Grup | Durum |
| --- | --- | --- |
| `DevOps.Dokploy` | DevOps | Hazır (bkz. [Dokploy](#dokploy)) |
| `DevOps.Dokku` | DevOps | Planlandı |

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

`SystemName` yalnızca harf, rakam ve nokta içerir, sonradan değiştirilmez (kurulum kaydı, statik dosya adresi `/plugins/devops.dokku/` ve audit kayıtları buna bağlıdır).

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
