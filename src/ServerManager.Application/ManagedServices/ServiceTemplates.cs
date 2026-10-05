using System.Globalization;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.ManagedServices;

/// <summary>
/// Tek tıkla kurulabilen servislerin sabit kataloğu. Komutlar container içinde çalışır ve gizli değerleri container'ın
/// ortam değişkenlerinden (<c>$POSTGRES_PASSWORD</c> …) okur; panel parolayı hiçbir komut satırına yazmaz.
/// </summary>
public static class ServiceTemplates
{
    public const string Postgres = "postgres";
    public const string MySql = "mysql";
    public const string MariaDb = "mariadb";
    public const string Redis = "redis";
    public const string MongoDb = "mongodb";
    public const string SqlServer = "mssql";
    public const string MinIo = "minio";
    public const string RabbitMq = "rabbitmq";
    public const string Adminer = "adminer";
    public const string PgAdmin = "pgadmin";
    public const string UptimeKuma = "uptime-kuma";
    public const string N8n = "n8n";

    private const string SqlCmd = "/opt/mssql-tools18/bin/sqlcmd";

    public static readonly IReadOnlyList<ServiceTemplate> All =
    [
        new ServiceTemplate
        {
            Key = Postgres,
            DisplayName = "PostgreSQL",
            Category = ManagedServiceCategory.Database,
            Description = "Güçlü, açık kaynak ilişkisel veritabanı.",
            LogoText = "PG",
            Color = "#336791",
            Image = "postgres",
            Tags = ["18", "17", "16", "15", "14"],
            Ports = [new ServicePortDefinition("db", 5432, ServicePortRole.Primary, "PostgreSQL")],
            Credentials = new ServiceCredentialSpec
            {
                UsernameKind = ServiceUsernameKind.Name,
                DefaultUsername = "app",
                HasDatabase = true,
                DefaultDatabase = "app"
            },
            // 18 ve sonrası imajlarda PGDATA /var/lib/postgresql/<sürüm>/docker; volume üst klasöre bağlanmalıdır.
            DataPath = tag => MajorVersion(tag) is { } major && major < 18 ? "/var/lib/postgresql/data" : "/var/lib/postgresql",
            Environment = c =>
            [
                new("POSTGRES_USER", c.Username ?? string.Empty),
                new("POSTGRES_PASSWORD", c.Password ?? string.Empty, true),
                new("POSTGRES_DB", c.Database ?? string.Empty)
            ],
            HealthCommand = "pg_isready -q -h 127.0.0.1 -U \"$POSTGRES_USER\" -d \"$POSTGRES_DB\"",
            ReadinessCommand = "PGPASSWORD=\"$POSTGRES_PASSWORD\" psql -h 127.0.0.1 -U \"$POSTGRES_USER\" -d \"$POSTGRES_DB\" -tAc 'SELECT 1' >/dev/null",
            ConsoleCommand = "export PGPASSWORD=\"$POSTGRES_PASSWORD\"; exec psql -h 127.0.0.1 -U \"$POSTGRES_USER\" -d \"$POSTGRES_DB\"",
            ConsoleLabel = "psql",
            ConnectionString = ServiceConnectionStrings.Postgres,
            SuggestedEnvironment = (e, c) => new Dictionary<string, string>
            {
                ["DATABASE_URL"] = ServiceConnectionStrings.Postgres(e, c),
                ["ConnectionStrings__Default"] = ServiceConnectionStrings.Npgsql(e, c),
                ["PGHOST"] = e.Host,
                ["PGPORT"] = Port(e),
                ["PGUSER"] = c.Username ?? string.Empty,
                ["PGPASSWORD"] = c.Password ?? string.Empty,
                ["PGDATABASE"] = c.Database ?? string.Empty
            },
            MinMemoryMb = 256,
            MemoryHint = "Küçük projeler için 256 MB yeterlidir; üretimde en az 1 GB önerilir.",
            WarnOnMajorUpgrade = true,
            Notes = "Ana sürüm yükseltmesi (ör. 16 → 17) veri klasörünü otomatik dönüştürmez; önce pg_dump ile yedek alın."
        },
        new ServiceTemplate
        {
            Key = MySql,
            DisplayName = "MySQL",
            Category = ManagedServiceCategory.Database,
            Description = "Yaygın kullanılan açık kaynak ilişkisel veritabanı.",
            LogoText = "My",
            Color = "#00758F",
            Image = "mysql",
            Tags = ["8.4", "lts", "innovation", "8.0"],
            Ports = [new ServicePortDefinition("db", 3306, ServicePortRole.Primary, "MySQL")],
            Credentials = new ServiceCredentialSpec
            {
                UsernameKind = ServiceUsernameKind.Name,
                DefaultUsername = "app",
                HasDatabase = true,
                DefaultDatabase = "app",
                ReservedUsernames = ["root"]
            },
            DataPath = _ => "/var/lib/mysql",
            Environment = c =>
            [
                new("MYSQL_ROOT_PASSWORD", c.Password ?? string.Empty, true),
                new("MYSQL_USER", c.Username ?? string.Empty),
                new("MYSQL_PASSWORD", c.Password ?? string.Empty, true),
                new("MYSQL_DATABASE", c.Database ?? string.Empty)
            ],
            HealthCommand = "mysqladmin ping -h 127.0.0.1 --silent",
            ReadinessCommand = MySqlClient("mysql", "\"$MYSQL_USER\"", "$MYSQL_PASSWORD", "-e 'SELECT 1' >/dev/null"),
            ConsoleCommand = MySqlClient("mysql", "root", "$MYSQL_ROOT_PASSWORD", "${MYSQL_DATABASE:+\"$MYSQL_DATABASE\"}"),
            ConsoleLabel = "mysql",
            ConnectionString = ServiceConnectionStrings.MySql,
            SuggestedEnvironment = (e, c) => new Dictionary<string, string>
            {
                ["DATABASE_URL"] = ServiceConnectionStrings.MySql(e, c),
                ["ConnectionStrings__Default"] = ServiceConnectionStrings.MySqlAdo(e, c)
            },
            MinMemoryMb = 512,
            MemoryHint = "MySQL 8 boşta ~400 MB kullanır; en az 512 MB, üretimde 1 GB+ önerilir.",
            WarnOnMajorUpgrade = true,
            Notes = "Root parolası, uygulama kullanıcısının parolasıyla aynıdır."
        },
        new ServiceTemplate
        {
            Key = MariaDb,
            DisplayName = "MariaDB",
            Category = ManagedServiceCategory.Database,
            Description = "MySQL uyumlu, topluluk tarafından geliştirilen veritabanı.",
            LogoText = "Ma",
            Color = "#003545",
            Image = "mariadb",
            Tags = ["11.8", "11.4", "10.11", "lts"],
            Ports = [new ServicePortDefinition("db", 3306, ServicePortRole.Primary, "MariaDB")],
            Credentials = new ServiceCredentialSpec
            {
                UsernameKind = ServiceUsernameKind.Name,
                DefaultUsername = "app",
                HasDatabase = true,
                DefaultDatabase = "app",
                ReservedUsernames = ["root"]
            },
            DataPath = _ => "/var/lib/mysql",
            Environment = c =>
            [
                new("MARIADB_ROOT_PASSWORD", c.Password ?? string.Empty, true),
                new("MARIADB_USER", c.Username ?? string.Empty),
                new("MARIADB_PASSWORD", c.Password ?? string.Empty, true),
                new("MARIADB_DATABASE", c.Database ?? string.Empty)
            ],
            HealthCommand = "healthcheck.sh --connect --innodb_initialized",
            ReadinessCommand = MySqlClient("mariadb", "\"$MARIADB_USER\"", "$MARIADB_PASSWORD", "-e 'SELECT 1' >/dev/null"),
            ConsoleCommand = MySqlClient("mariadb", "root", "$MARIADB_ROOT_PASSWORD", "${MARIADB_DATABASE:+\"$MARIADB_DATABASE\"}"),
            ConsoleLabel = "mariadb",
            ConnectionString = ServiceConnectionStrings.MySql,
            SuggestedEnvironment = (e, c) => new Dictionary<string, string>
            {
                ["DATABASE_URL"] = ServiceConnectionStrings.MySql(e, c),
                ["ConnectionStrings__Default"] = ServiceConnectionStrings.MySqlAdo(e, c)
            },
            MinMemoryMb = 256,
            MemoryHint = "En az 256 MB; üretimde 1 GB+ önerilir.",
            WarnOnMajorUpgrade = true,
            Notes = "Root parolası, uygulama kullanıcısının parolasıyla aynıdır."
        },
        new ServiceTemplate
        {
            Key = Redis,
            DisplayName = "Redis",
            Category = ManagedServiceCategory.Database,
            Description = "Bellek içi anahtar-değer deposu; önbellek, kuyruk ve oturumlar için.",
            LogoText = "Rd",
            Color = "#DC382D",
            Image = "redis",
            Tags = ["8", "7.4", "7.2"],
            Ports = [new ServicePortDefinition("db", 6379, ServicePortRole.Primary, "Redis")],
            Credentials = new ServiceCredentialSpec { UsernameKind = ServiceUsernameKind.None },
            DataPath = _ => "/data",
            Environment = c => [new("REDIS_PASSWORD", c.Password ?? string.Empty, true)],
            // Parola container ortamından okunur; sunucudaki docker komut satırında yer almaz. Giriş betiği redis kullanıcısına geçer.
            Command = ["sh", "-c", "exec docker-entrypoint.sh redis-server --requirepass \"$REDIS_PASSWORD\" --appendonly yes"],
            HealthCommand = "REDISCLI_AUTH=\"$REDIS_PASSWORD\" redis-cli ping | grep -q PONG",
            ReadinessCommand = "REDISCLI_AUTH=\"$REDIS_PASSWORD\" redis-cli ping | grep -q PONG",
            ConsoleCommand = "export REDISCLI_AUTH=\"$REDIS_PASSWORD\"; exec redis-cli",
            ConsoleLabel = "redis-cli",
            ConnectionString = ServiceConnectionStrings.Redis,
            SuggestedEnvironment = (e, c) => new Dictionary<string, string>
            {
                ["REDIS_URL"] = ServiceConnectionStrings.Redis(e, c),
                ["ConnectionStrings__Redis"] = ServiceConnectionStrings.RedisAdo(e, c)
            },
            MinMemoryMb = 64,
            MemoryHint = "Veri belleğe sığmalıdır; bellek sınırını veri boyutunun en az iki katı seçin.",
            WarnOnMajorUpgrade = false
        },
        new ServiceTemplate
        {
            Key = MongoDb,
            DisplayName = "MongoDB",
            Category = ManagedServiceCategory.Database,
            Description = "Doküman tabanlı NoSQL veritabanı.",
            LogoText = "Mg",
            Color = "#47A248",
            Image = "mongo",
            Tags = ["8.0", "7.0", "6.0"],
            Ports = [new ServicePortDefinition("db", 27017, ServicePortRole.Primary, "MongoDB")],
            Credentials = new ServiceCredentialSpec
            {
                UsernameKind = ServiceUsernameKind.Name,
                DefaultUsername = "admin",
                HasDatabase = true,
                DefaultDatabase = "app"
            },
            DataPath = _ => "/data/db",
            Environment = c =>
            [
                new("MONGO_INITDB_ROOT_USERNAME", c.Username ?? string.Empty),
                new("MONGO_INITDB_ROOT_PASSWORD", c.Password ?? string.Empty, true),
                new("MONGO_INITDB_DATABASE", c.Database ?? string.Empty)
            ],
            HealthCommand = "mongosh --quiet --eval 'db.adminCommand(\"ping\").ok' | grep -q 1",
            ReadinessCommand = "mongosh --quiet -u \"$MONGO_INITDB_ROOT_USERNAME\" -p \"$MONGO_INITDB_ROOT_PASSWORD\" --authenticationDatabase admin --eval 'db.adminCommand({ ping: 1 }).ok' | grep -q 1",
            ConsoleCommand = "exec mongosh -u \"$MONGO_INITDB_ROOT_USERNAME\" -p \"$MONGO_INITDB_ROOT_PASSWORD\" --authenticationDatabase admin \"${MONGO_INITDB_DATABASE:-admin}\"",
            ConsoleLabel = "mongosh",
            ConnectionString = ServiceConnectionStrings.Mongo,
            SuggestedEnvironment = (e, c) => new Dictionary<string, string>
            {
                ["MONGODB_URI"] = ServiceConnectionStrings.Mongo(e, c),
                ["ConnectionStrings__Mongo"] = ServiceConnectionStrings.Mongo(e, c)
            },
            MinMemoryMb = 512,
            MemoryHint = "WiredTiger önbelleği belleğin yarısını kullanır; en az 512 MB, üretimde 2 GB+ önerilir.",
            WarnOnMajorUpgrade = true,
            Notes = "Ana sürümler tek tek yükseltilmelidir (6.0 → 7.0 → 8.0)."
        },
        new ServiceTemplate
        {
            Key = SqlServer,
            DisplayName = "SQL Server",
            Category = ManagedServiceCategory.Database,
            Description = "Microsoft SQL Server (Linux container). Varsayılan sürüm Developer'dır.",
            LogoText = "MS",
            Color = "#CC2927",
            Image = "mcr.microsoft.com/mssql/server",
            Tags = ["2022-latest", "2025-latest"],
            Ports = [new ServicePortDefinition("db", 1433, ServicePortRole.Primary, "SQL Server")],
            Credentials = new ServiceCredentialSpec
            {
                UsernameKind = ServiceUsernameKind.Fixed,
                DefaultUsername = "sa",
                PasswordPolicy = ServicePasswordPolicy.MssqlComplex
            },
            DataPath = _ => "/var/opt/mssql",
            DataOwner = "10001:0",
            Environment = c =>
            [
                new("ACCEPT_EULA", "Y"),
                new("MSSQL_SA_PASSWORD", c.Password ?? string.Empty, true)
            ],
            DefaultEnvironment = [new("MSSQL_PID", "Developer")],
            HealthCommand = $"SQLCMDPASSWORD=\"$MSSQL_SA_PASSWORD\" {SqlCmd} -C -S localhost -U sa -b -Q 'SELECT 1' -h -1 >/dev/null",
            ReadinessCommand = $"SQLCMDPASSWORD=\"$MSSQL_SA_PASSWORD\" {SqlCmd} -C -S localhost -U sa -b -Q 'SELECT 1' -h -1 >/dev/null",
            ConsoleCommand = $"export SQLCMDPASSWORD=\"$MSSQL_SA_PASSWORD\"; exec {SqlCmd} -C -S localhost -U sa",
            ConsoleLabel = "sqlcmd",
            ConnectionString = ServiceConnectionStrings.SqlServer,
            SuggestedEnvironment = (e, c) => new Dictionary<string, string>
            {
                ["ConnectionStrings__Default"] = ServiceConnectionStrings.SqlServer(e, c)
            },
            RequiresX86 = true,
            MinMemoryMb = 2048,
            MemoryHint = "SQL Server en az 2 GB bellek ister; daha azında açılmaz.",
            WarnOnMajorUpgrade = true,
            HealthTimeoutSeconds = 300,
            Notes = "MSSQL_PID ek değişkeniyle sürüm seçilebilir (Developer, Express, Standard …). Lisans koşullarını kontrol edin."
        },
        new ServiceTemplate
        {
            Key = MinIo,
            DisplayName = "MinIO",
            Category = ManagedServiceCategory.Application,
            Description = "S3 uyumlu nesne depolama; API ve web konsolu.",
            LogoText = "S3",
            Color = "#C72E49",
            Image = "minio/minio",
            Tags = ["latest", "RELEASE.2025-04-22T22-12-26Z"],
            Ports =
            [
                new ServicePortDefinition("api", 9000, ServicePortRole.Primary, "S3 API"),
                new ServicePortDefinition("console", 9001, ServicePortRole.WebUi, "Web konsolu")
            ],
            Credentials = new ServiceCredentialSpec
            {
                UsernameKind = ServiceUsernameKind.Name,
                DefaultUsername = "admin",
                UsernameLabel = "Root kullanıcı (access key)"
            },
            DataPath = _ => "/data",
            Environment = c =>
            [
                new("MINIO_ROOT_USER", c.Username ?? string.Empty),
                new("MINIO_ROOT_PASSWORD", c.Password ?? string.Empty, true)
            ],
            Command = ["server", "/data", "--console-address", ":9001"],
            HealthCommand = "mc ready local",
            ConnectionString = (e, _) => ServiceConnectionStrings.Http(e),
            SuggestedEnvironment = (e, c) => new Dictionary<string, string>
            {
                ["S3_ENDPOINT"] = ServiceConnectionStrings.Http(e),
                ["S3_ACCESS_KEY"] = c.Username ?? string.Empty,
                ["S3_SECRET_KEY"] = c.Password ?? string.Empty,
                ["AWS_ACCESS_KEY_ID"] = c.Username ?? string.Empty,
                ["AWS_SECRET_ACCESS_KEY"] = c.Password ?? string.Empty
            },
            MinMemoryMb = 256
        },
        new ServiceTemplate
        {
            Key = RabbitMq,
            DisplayName = "RabbitMQ",
            Category = ManagedServiceCategory.Application,
            Description = "Mesaj kuyruğu (AMQP) ve yönetim arayüzü.",
            LogoText = "RQ",
            Color = "#FF6600",
            Image = "rabbitmq",
            Tags = ["4.1-management", "4.0-management", "3.13-management"],
            Ports =
            [
                new ServicePortDefinition("amqp", 5672, ServicePortRole.Primary, "AMQP"),
                new ServicePortDefinition("management", 15672, ServicePortRole.WebUi, "Yönetim arayüzü")
            ],
            Credentials = new ServiceCredentialSpec { UsernameKind = ServiceUsernameKind.Name, DefaultUsername = "admin" },
            DataPath = _ => "/var/lib/rabbitmq",
            DataOwner = "999:999",
            Environment = c =>
            [
                new("RABBITMQ_DEFAULT_USER", c.Username ?? string.Empty),
                new("RABBITMQ_DEFAULT_PASS", c.Password ?? string.Empty, true)
            ],
            HealthCommand = "rabbitmq-diagnostics -q ping",
            ReadinessCommand = "rabbitmq-diagnostics -q check_port_connectivity",
            ConnectionString = ServiceConnectionStrings.Amqp,
            SuggestedEnvironment = (e, c) => new Dictionary<string, string>
            {
                ["RABBITMQ_URL"] = ServiceConnectionStrings.Amqp(e, c),
                ["AMQP_URL"] = ServiceConnectionStrings.Amqp(e, c)
            },
            MinMemoryMb = 256,
            WarnOnMajorUpgrade = true,
            Notes = "Ana sürüm yükseltmesinden önce feature flag'lerin etkin olduğundan emin olun."
        },
        new ServiceTemplate
        {
            Key = Adminer,
            DisplayName = "Adminer",
            Category = ManagedServiceCategory.Application,
            Description = "Tek dosyalık veritabanı yönetim arayüzü (MySQL, PostgreSQL, SQLite …).",
            LogoText = "Ad",
            Color = "#34495E",
            Image = "adminer",
            Tags = ["latest", "5", "4"],
            Ports = [new ServicePortDefinition("web", 8080, ServicePortRole.WebUi, "Web arayüzü")],
            ConnectionString = (e, _) => ServiceConnectionStrings.Http(e),
            MinMemoryMb = 64,
            Notes = "Aynı sunucudaki servislere bağlanmak için sunucu alanına container adını yazın (ör. sm-svc-postgres)."
        },
        new ServiceTemplate
        {
            Key = PgAdmin,
            DisplayName = "pgAdmin",
            Category = ManagedServiceCategory.Application,
            Description = "PostgreSQL için web tabanlı yönetim aracı.",
            LogoText = "pA",
            Color = "#2F6792",
            Image = "dpage/pgadmin4",
            Tags = ["9", "8", "latest"],
            Ports = [new ServicePortDefinition("web", 80, ServicePortRole.WebUi, "Web arayüzü")],
            Credentials = new ServiceCredentialSpec
            {
                UsernameKind = ServiceUsernameKind.Email,
                DefaultUsername = "admin@example.com",
                UsernameLabel = "Giriş e-postası"
            },
            DataPath = _ => "/var/lib/pgadmin",
            DataOwner = "5050:5050",
            Environment = c =>
            [
                new("PGADMIN_DEFAULT_EMAIL", c.Username ?? string.Empty),
                new("PGADMIN_DEFAULT_PASSWORD", c.Password ?? string.Empty, true)
            ],
            ConnectionString = (e, _) => ServiceConnectionStrings.Http(e),
            MinMemoryMb = 256
        },
        new ServiceTemplate
        {
            Key = UptimeKuma,
            DisplayName = "Uptime Kuma",
            Category = ManagedServiceCategory.Application,
            Description = "Kendi sunucunuzda çalışan uptime izleme ve durum sayfası.",
            LogoText = "UK",
            Color = "#5CDD8B",
            Image = "louislam/uptime-kuma",
            Tags = ["2", "1"],
            Ports = [new ServicePortDefinition("web", 3001, ServicePortRole.WebUi, "Web arayüzü")],
            DataPath = _ => "/app/data",
            ConnectionString = (e, _) => ServiceConnectionStrings.Http(e),
            MinMemoryMb = 128,
            WarnOnMajorUpgrade = true,
            Notes = "Yönetici hesabı ilk açılışta web arayüzünden oluşturulur."
        },
        new ServiceTemplate
        {
            Key = N8n,
            DisplayName = "n8n",
            Category = ManagedServiceCategory.Application,
            Description = "Görsel iş akışı otomasyonu.",
            LogoText = "n8",
            Color = "#EA4B71",
            Image = "docker.n8n.io/n8nio/n8n",
            Tags = ["stable", "latest", "next"],
            Ports = [new ServicePortDefinition("web", 5678, ServicePortRole.WebUi, "Web arayüzü")],
            Credentials = new ServiceCredentialSpec
            {
                UsernameKind = ServiceUsernameKind.None,
                PasswordPolicy = ServicePasswordPolicy.None,
                GeneratesEncryptionKey = true
            },
            DataPath = _ => "/home/node/.n8n",
            DataOwner = "1000:1000",
            Environment = c => [new("N8N_ENCRYPTION_KEY", c.EncryptionKey ?? string.Empty, true)],
            DefaultEnvironment =
            [
                new("N8N_SECURE_COOKIE", "false"),
                new("GENERIC_TIMEZONE", "Europe/Istanbul"),
                new("TZ", "Europe/Istanbul")
            ],
            HealthCommand = "wget -q -O /dev/null http://127.0.0.1:5678/healthz",
            ConnectionString = (e, _) => ServiceConnectionStrings.Http(e),
            MinMemoryMb = 512,
            Notes = "N8N_ENCRYPTION_KEY kurulumda üretilir ve saklanır; kaybolursa kayıtlı kimlik bilgileri çözülemez. " +
                    "HTTPS ile yayınlıyorsanız N8N_SECURE_COOKIE=true yapın."
        }
    ];

    private static readonly Dictionary<string, ServiceTemplate> ByKey = All.ToDictionary(t => t.Key, StringComparer.Ordinal);

    public static ServiceTemplate? Find(string? key) => key is not null && ByKey.TryGetValue(key, out var template) ? template : null;

    public static IEnumerable<ServiceTemplate> ByCategory(ManagedServiceCategory category) => All.Where(t => t.Category == category);

    /// <summary>Etiketin ilk sayısal parçası (ör. "16.4" → 16, "2022-latest" → 2022); sayı yoksa null ("latest").</summary>
    public static int? MajorVersion(string? tag)
    {
        if (string.IsNullOrEmpty(tag) || !char.IsAsciiDigit(tag[0]))
            return null;

        var end = 0;
        while (end < tag.Length && char.IsAsciiDigit(tag[end]))
            end++;

        return int.TryParse(tag.AsSpan(0, end), NumberStyles.None, CultureInfo.InvariantCulture, out var major) ? major : null;
    }

    /// <summary>
    /// MySQL/MariaDB istemcisi: parola geçici bir seçenek dosyasına (0600, container içinde) yazılır; komut satırında görünmez.
    /// printf kabuğun yerleşik komutu olduğu için değer süreç listesine de düşmez.
    /// </summary>
    private static string MySqlClient(string client, string user, string passwordVariable, string arguments) =>
        "f=$(mktemp) || exit 1; chmod 600 \"$f\"; " +
        $"printf '[client]\\nuser=%s\\npassword=\"%s\"\\n' {user} \"{passwordVariable}\" > \"$f\"; " +
        $"{client} --defaults-extra-file=\"$f\" -h 127.0.0.1 {arguments}; s=$?; rm -f \"$f\"; exit $s";

    private static string Port(ServiceEndpoint endpoint) => endpoint.Port.ToString(CultureInfo.InvariantCulture);
}
