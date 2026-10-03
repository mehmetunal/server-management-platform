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
└── ServerManager.Web/
    ├── Controllers/
    ├── Hubs/
    ├── Views/
    ├── Components/
    ├── wwwroot/
    └── Middleware/
```

Repository pattern kullanılmalı; database erişimi repository katmanında tutulmalıdır.

---

# 51. Provider Adapter Architecture

Server işlemleri doğrudan controller'a yazılmamalıdır.

Örneğin:

```text
IServerProvider
   ├── SshServerProvider
   ├── DockerProvider
   ├── DokployProvider
   └── CloudProvider
```

Docker:

```text
IDockerProvider
```

Dokploy:

```text
IDokployProvider
```

gibi abstraction kullanılmalıdır.

Bu yapı ileride yeni provider eklemeyi kolaylaştırır.

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

### Teknoloji

| Katman | Teknoloji |
| --- | --- |
| Web | ASP.NET Core MVC (.NET 10), Tailwind CSS v4 |
| Kimlik | ASP.NET Core Identity (Guid anahtarlı), izin claim'leri |
| Veri | MSSQL, EF Core (yalnızca sorgu), FluentMigrator (tüm şema) |
| Doğrulama | FluentValidation |
| SSH | SSH.NET |
| Canlı veri | ASP.NET Core SignalR, Chart.js |
| Log | Serilog (konsol + `logs/` dosyası) |
| Test | xUnit v3, NSubstitute |

Mimari: `Domain` → `Application` (servisler, DTO, validator, arayüzler) → `Infrastructure` (EF Core, repository, Identity, şifreleme, SSH) → `Web` (controller, view). Mediator yoktur; akış `Controller → Service → Repository` şeklindedir.

### Gereksinimler

- .NET SDK 10.0
- Node.js 20+ (yalnızca Tailwind CSS derlemesi için)
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

### 3. CSS derleme

```bash
cd src/ServerManager.Web
npm install
npm run build:css            # tek seferlik, minify
npm run build:css:watch      # geliştirme sırasında izleme modu
```

Derlenmiş `wwwroot/css/site.css` repoya dahildir; sadece `Styles/input.css` veya view'larda sınıf değişikliği yapıldığında yeniden derlenmelidir.

Chart.js ve SignalR istemcisi CDN yerine `wwwroot/lib` altından sunulur (CSP `script-src 'self'`). Dosyalar repoya dahildir; paket sürümü güncellendiğinde yeniden kopyalanır:

```bash
npm install
npm run build:vendor         # chart.umd.min.js ve signalr.min.js → wwwroot/lib
```

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
| SuperAdmin | Tümü |
| Admin | Dashboard, sunucu görüntüleme/ekleme/düzenleme/silme/bağlantı testi, audit log |
| Operator | Dashboard, sunucu görüntüleme, bağlantı testi |
| Developer | Dashboard, sunucu görüntüleme |
| Viewer | Dashboard, sunucu görüntüleme |

İzinler `AspNetRoleClaims` tablosunda `permission` claim'i olarak tutulur. Seeder yalnızca eksik izinleri ekler; elle eklenmiş izinleri kaldırmaz.

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
