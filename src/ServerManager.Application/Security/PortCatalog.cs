using ServerManager.Domain.Enums;

namespace ServerManager.Application.Security;

public static class PortCatalog
{
    private static readonly IReadOnlyDictionary<int, KnownPort> Ports = new KnownPort[]
    {
        new(21, "FTP", SecurityCheckStatus.Warning, "Parolalar şifresiz gider; SFTP kullanın."),
        new(22, "SSH"),
        new(23, "Telnet", SecurityCheckStatus.Critical, "Şifresiz uzak erişim; kapatın ve SSH kullanın."),
        new(25, "SMTP"),
        new(53, "DNS"),
        new(80, "HTTP"),
        new(111, "rpcbind", SecurityCheckStatus.Warning, "NFS kullanılmıyorsa kapatın; DDoS yansıtmada kullanılabilir."),
        new(143, "IMAP"),
        new(443, "HTTPS"),
        new(465, "SMTPS"),
        new(587, "SMTP (gönderim)"),
        new(993, "IMAPS"),
        new(995, "POP3S"),
        new(1433, "SQL Server", SecurityCheckStatus.Critical, "Veritabanı internete açık olmamalı."),
        new(2375, "Docker API (şifresiz)", SecurityCheckStatus.Critical, "Bu porta erişen herkes sunucuda root yetkisi alır."),
        new(2376, "Docker API (TLS)", SecurityCheckStatus.Warning, "İstemci sertifikası doğrulaması açık olmalı; mümkünse yalnızca yerel/VPN erişimi verin."),
        new(3000, "Uygulama"),
        new(3306, "MySQL / MariaDB", SecurityCheckStatus.Critical, "Veritabanı internete açık olmamalı."),
        new(3389, "RDP", SecurityCheckStatus.Warning, "Uzak masaüstü yalnızca VPN üzerinden açılmalı."),
        new(5432, "PostgreSQL", SecurityCheckStatus.Critical, "Veritabanı internete açık olmamalı."),
        new(5900, "VNC", SecurityCheckStatus.Warning, "Uzak ekran yalnızca VPN/SSH tüneli üzerinden açılmalı."),
        new(5984, "CouchDB", SecurityCheckStatus.Critical, "Veritabanı internete açık olmamalı."),
        new(6379, "Redis", SecurityCheckStatus.Critical, "Redis'e erişen biri sunucuya dosya yazabilir; yalnızca 127.0.0.1'e bağlayın."),
        new(8080, "HTTP (alternatif)"),
        new(8086, "InfluxDB", SecurityCheckStatus.Warning, "Yalnızca yerel ağdan erişilebilir olmalı."),
        new(9000, "Uygulama / Portainer"),
        new(9042, "Cassandra", SecurityCheckStatus.Critical, "Veritabanı internete açık olmamalı."),
        new(9200, "Elasticsearch", SecurityCheckStatus.Critical, "Kimlik doğrulamasız Elasticsearch veri sızıntısının en yaygın nedenidir."),
        new(10250, "Kubelet API", SecurityCheckStatus.Warning, "Kubelet yalnızca küme içinden erişilebilir olmalı."),
        new(11211, "Memcached", SecurityCheckStatus.Critical, "Kimlik doğrulaması yoktur; DDoS yansıtmada kullanılır."),
        new(27017, "MongoDB", SecurityCheckStatus.Critical, "Veritabanı internete açık olmamalı.")
    }.ToDictionary(p => p.Port);

    public static KnownPort? Find(int port) => Ports.GetValueOrDefault(port);

    public static string ServiceName(int port, string? process) =>
        Find(port)?.Service ?? (string.IsNullOrEmpty(process) ? "—" : process);
}
