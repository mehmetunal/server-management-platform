using System.Globalization;
using System.Text;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.ManagedServices;

/// <summary>
/// Servis container'ı için sunucuda çalışan komutlar. Değerler Application katmanında doğrulanır ve burada ayrıca
/// <see cref="ShellQuote"/> ile kaçışlanır. Gizli değerler hiçbir komut metnine girmez: ortam dosyası stdin ile yazılır
/// (<see cref="WriteEnvironmentFile"/>) ve container'a <c>--env-file</c> ile verilir.
/// </summary>
internal static class ManagedServiceCommands
{
    public const int DockerMissingExitCode = 3;
    public const int PortBusyExitCode = 4;
    public const int NetworkMissingExitCode = 5;
    public const int NotManagedExitCode = 6;

    public const string PortBusyMarker = "SM_PORT_BUSY=";
    public const string NetworkMissingMarker = "SM_NETWORK_MISSING=";

    /// <summary>Panelin oluşturduğu sunucu klasörünün yolu servis durum klasöründe bu dosyaya yazılır; veri silme buna bakar.</summary>
    public const string DataPathRecordFile = "data-path";

    private const string InteractiveShell = "if command -v bash >/dev/null 2>&1; then exec bash; else exec sh; fi";

    /// <summary>Docker, mimari ve Dokploy/Dokku varlığını <c>SM_*</c> satırlarıyla yazar; her durumda 0 ile çıkar.</summary>
    public static string Probe() =>
        Shell(
            "echo \"SM_ARCH=$(uname -m 2>/dev/null || echo unknown)\"\n" +
            "if command -v docker >/dev/null 2>&1; then\n" +
            "  echo SM_DOCKER=installed\n" +
            "  if v=$(docker version --format '{{.Server.Version}}' 2>/dev/null); then echo \"SM_DOCKER_VERSION=$v\"; else echo SM_DOCKER_DOWN=1; fi\n" +
            "  if docker service ls --format '{{.Name}}' 2>/dev/null | grep -qx dokploy || docker ps -a --format '{{.Names}}' 2>/dev/null | grep -q '^dokploy'; then echo SM_DOKPLOY=1; fi\n" +
            "else\n" +
            "  echo SM_DOCKER=missing\n" +
            "fi\n" +
            "if command -v dokku >/dev/null 2>&1; then echo SM_DOKKU=1; fi\n");

    /// <summary>Docker kurulu ve çalışıyorsa sürüm ve mimariyi yazar; değilse <see cref="DockerMissingExitCode"/> ile çıkar.</summary>
    public static string CheckDocker() =>
        Shell(
            "echo \"SM_ARCH=$(uname -m 2>/dev/null || echo unknown)\"\n" +
            $"command -v docker >/dev/null 2>&1 || {{ echo 'docker: not found' >&2; exit {DockerMissingExitCode}; }}\n" +
            "v=$(docker version --format '{{.Server.Version}}') || exit 1\n" +
            "echo \"SM_DOCKER_VERSION=$v\"\n");

    /// <summary>
    /// Sunucu portlarının boş olduğunu denetler (<c>ss -ltnH</c> ve çalışan container'ların yayınladığı portlar). Servisin kendi
    /// container'ının tuttuğu portlar (yeniden oluşturmada) sayılmaz. Doluysa <c>SM_PORT_BUSY=</c> satırı ve
    /// <see cref="PortBusyExitCode"/> ile çıkar.
    /// </summary>
    public static string CheckPorts(string containerName, IEnumerable<int> ports)
    {
        var list = string.Join(' ', ports.Distinct().Select(p => p.ToString(CultureInfo.InvariantCulture)));
        return Shell(
            "set -u\n" +
            $"name={ShellQuote.Quote(containerName)}\n" +
            "own=$(docker port \"$name\" 2>/dev/null | sed -n 's/.*:\\([0-9][0-9]*\\)$/\\1/p' | sort -u)\n" +
            "busy=''\n" +
            $"for p in {list}; do\n" +
            "  if printf '%s\\n' \"$own\" | grep -qx \"$p\"; then continue; fi\n" +
            "  if command -v ss >/dev/null 2>&1 && ss -ltnH 2>/dev/null | awk '{print $4}' | grep -Eq \"[:.]$p\\$\"; then busy=\"$busy $p\"; continue; fi\n" +
            "  if docker ps --format '{{.Names}} {{.Ports}}' 2>/dev/null | awk -v n=\"$name\" '$1 != n' | grep -Eq \":$p->\"; then busy=\"$busy $p\"; fi\n" +
            "done\n" +
            $"if [ -n \"$busy\" ]; then echo \"{PortBusyMarker}$busy\"; exit {PortBusyExitCode}; fi\n");
    }

    public static string Pull(string imageReference) => "docker pull " + ShellQuote.Quote(imageReference);

    /// <summary>
    /// Panel ağlarını (yoksa) oluşturur; kullanıcının seçtiği ağlar var olmalıdır, yoksa <c>SM_NETWORK_MISSING=</c> ve
    /// <see cref="NetworkMissingExitCode"/> ile çıkılır.
    /// </summary>
    public static string EnsureNetworks(IEnumerable<string> managedNetworks, IEnumerable<string> requiredNetworks)
    {
        var builder = new StringBuilder("set -eu\n");
        foreach (var network in managedNetworks)
        {
            var quoted = ShellQuote.Quote(network);
            builder.Append($"docker network inspect {quoted} >/dev/null 2>&1 || docker network create --label sm.managed=true {quoted} >/dev/null\n");
        }

        foreach (var network in requiredNetworks)
        {
            var quoted = ShellQuote.Quote(network);
            builder.Append($"docker network inspect {quoted} >/dev/null 2>&1 || {{ echo \"{NetworkMissingMarker}\"{quoted} >&2; exit {NetworkMissingExitCode}; }}\n");
        }

        return Shell(builder.ToString());
    }

    /// <summary>Servisin Docker volume'unu (yoksa) oluşturur.</summary>
    public static string EnsureVolume(string slug) =>
        Shell(
            "set -eu\n" +
            $"v={ShellQuote.Quote(ManagedServiceNames.VolumeName(slug))}\n" +
            $"docker volume inspect \"$v\" >/dev/null 2>&1 || docker volume create --label sm.managed=true --label sm.service={ShellQuote.Quote(slug)} \"$v\" >/dev/null\n");

    /// <summary>
    /// Sunucu klasörü modu: klasörü oluşturur, gerekirse sahipliğini ayarlar. Klasör yoksa veya boşsa panel oluşturmuş sayılır
    /// ve yolu servis durum klasörüne kaydedilir; kaldırmada veri yalnızca bu kayıt varsa silinir. Sembolik bağlantı kabul edilmez.
    /// </summary>
    public static string EnsureHostDirectory(string slug, string hostPath, string? owner)
    {
        var builder = new StringBuilder(
            "set -eu\n" +
            "umask 022\n" +
            $"P={ShellQuote.Quote(hostPath)}\n" +
            $"S={ShellQuote.Quote(ManagedServiceNames.StateDirectory(slug))}\n" +
            "case \"$P\" in /*/*) ;; *) echo 'geçersiz klasör' >&2; exit 2 ;; esac\n" +
            $"if [ -L \"$P\" ]; then echo 'klasör sembolik bağlantı' >&2; exit {NotManagedExitCode}; fi\n" +
            "mkdir -p -- \"$S\"\n" +
            "chmod 700 \"$S\"\n" +
            "if [ ! -e \"$P\" ] || [ -z \"$(ls -A -- \"$P\" 2>/dev/null)\" ]; then\n" +
            "  mkdir -p -- \"$P\"\n" +
            $"  if [ ! -f \"$S/{DataPathRecordFile}\" ]; then printf '%s\\n' \"$P\" > \"$S/{DataPathRecordFile}\"; fi\n" +
            "fi\n");
        if (!string.IsNullOrEmpty(owner))
            builder.Append($"chown {ShellQuote.Quote(owner)} \"$P\"\n");

        return Shell(builder.ToString());
    }

    /// <summary>Ortam dosyasını stdin'den 0600 izniyle yazar; değerler komut satırında yer almaz.</summary>
    public static string WriteEnvironmentFile(string slug) =>
        Shell(
            "set -eu\n" +
            "umask 077\n" +
            $"mkdir -p -- {ShellQuote.Quote(ManagedServiceNames.StateDirectory(slug))}\n" +
            $"chmod 700 {ShellQuote.Quote(ManagedServiceNames.StateDirectory(slug))}\n" +
            $"cat > {ShellQuote.Quote(ManagedServiceNames.EnvironmentFile(slug))}\n" +
            $"chmod 600 {ShellQuote.Quote(ManagedServiceNames.EnvironmentFile(slug))}\n");

    public static string RemoveContainer(string containerName) => "docker rm -f " + ShellQuote.Quote(containerName);

    /// <summary>
    /// Container'ı oluşturur (başlatmaz). İlk ağ <c>--network</c> ile verilir, diğerleri <see cref="ConnectNetwork"/> ile
    /// bağlanır. Komut metni gizli değer içermez.
    /// </summary>
    public static string Create(ManagedServicePlan plan)
    {
        var builder = new StringBuilder("docker create");
        Append(builder, "--name", plan.ContainerName);
        Append(builder, "--hostname", plan.ContainerName);
        Append(builder, "--restart", "unless-stopped");
        Append(builder, "--label", "sm.managed=true");
        Append(builder, "--label", "sm.service=" + plan.Slug);
        Append(builder, "--label", "sm.template=" + plan.TemplateKey);
        Append(builder, "--network", plan.Networks.Count > 0 ? plan.Networks[0] : ManagedServiceNames.ServicesNetwork);

        foreach (var port in plan.Ports)
        {
            Append(builder, "-p", string.Create(CultureInfo.InvariantCulture, $"{port.BindAddress}:{port.HostPort}:{port.ContainerPort}/tcp"));
        }

        if (plan.DataPath is not null)
        {
            var source = plan.VolumeMode == ManagedServiceVolumeMode.HostPath && plan.HostDataPath is not null ? plan.HostDataPath : plan.VolumeName;
            Append(builder, "-v", $"{source}:{plan.DataPath}");
        }

        Append(builder, "--env-file", ManagedServiceNames.EnvironmentFile(plan.Slug));

        if (plan.MemoryLimitMb is { } memory)
            Append(builder, "--memory", string.Create(CultureInfo.InvariantCulture, $"{memory}m"));
        if (plan.CpuLimit is { } cpu)
            Append(builder, "--cpus", cpu.ToString("0.##", CultureInfo.InvariantCulture));

        if (plan.HealthCommand is not null)
        {
            Append(builder, "--health-cmd", plan.HealthCommand);
            Append(builder, "--health-interval", "10s");
            Append(builder, "--health-timeout", "5s");
            Append(builder, "--health-retries", "6");
            Append(builder, "--health-start-period", "30s");
        }

        builder.Append(' ').Append(ShellQuote.Quote(plan.ImageReference));
        foreach (var argument in plan.Command)
            builder.Append(' ').Append(ShellQuote.Quote(argument));

        return builder.ToString();
    }

    public static string ConnectNetwork(string network, string containerName) =>
        $"docker network connect {ShellQuote.Quote(network)} {ShellQuote.Quote(containerName)}";

    public static string Start(string containerName) => "docker start " + ShellQuote.Quote(containerName);

    /// <summary>Tek satır: <c>durum|sağlık|yeniden başlama sayısı|başlangıç|imaj|ağ1,ağ2,</c>.</summary>
    public static string Inspect(string containerName) =>
        "docker inspect -f " +
        ShellQuote.Quote("{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}|{{.RestartCount}}|{{.State.StartedAt}}|{{.Config.Image}}|{{range $k, $v := .NetworkSettings.Networks}}{{$k}},{{end}}") +
        " " + ShellQuote.Quote(containerName);

    /// <summary>Şablonun hazırlık/bağlantı testini container içinde çalıştırır; gizli değerler container ortamından okunur.</summary>
    public static string Exec(string containerName, string script) =>
        $"docker exec {ShellQuote.Quote(containerName)} sh -c {ShellQuote.Quote(script)}";

    public static string Logs(string containerName, int tail) =>
        $"docker logs --tail {tail.ToString(CultureInfo.InvariantCulture)} {ShellQuote.Quote(containerName)}";

    /// <summary>
    /// Konsol: şablonun istemci komutu (psql, redis-cli …) veya bash/sh. Komut container içinde <c>sh -c</c> ile çalışır;
    /// parolalar container'ın kendi ortam değişkenlerinden okunur, sunucudaki komut satırına yazılmaz.
    /// </summary>
    public static string Console(string containerName, string? consoleCommand) =>
        $"docker exec -it -e TERM=xterm-256color {ShellQuote.Quote(containerName)} sh -c {ShellQuote.Quote(consoleCommand ?? InteractiveShell)}";

    public static string ListNetworks() => "docker network ls --format '{{.Name}}'";

    /// <summary>
    /// Servisi kaldırır: container, (istenirse) volume veya panelin oluşturduğu sunucu klasörü ve servis durum klasörü.
    /// Sunucu klasörü yalnızca kurulumda panel tarafından oluşturulduysa (yol kaydı eşleşiyorsa) silinir.
    /// </summary>
    public static string RemoveData(ManagedServiceRemovalPlan plan)
    {
        var builder = new StringBuilder("set -eu\n");
        builder.Append($"S={ShellQuote.Quote(ManagedServiceNames.StateDirectory(plan.Slug))}\n");
        if (plan.RemoveData)
        {
            if (plan.VolumeMode == ManagedServiceVolumeMode.HostPath && plan.HostDataPath is not null)
            {
                builder.Append($"P={ShellQuote.Quote(plan.HostDataPath)}\n");
                builder.Append("case \"$P\" in /*/*) ;; *) echo 'klasör silinemez' >&2; exit 2 ;; esac\n");
                builder.Append("if [ -e \"$P\" ]; then\n");
                builder.Append($"  if [ -L \"$P\" ] || [ ! -f \"$S/{DataPathRecordFile}\" ] || [ \"$(cat \"$S/{DataPathRecordFile}\")\" != \"$P\" ]; then\n");
                builder.Append($"    echo 'klasör panel tarafından oluşturulmamış; silinmedi' >&2; exit {NotManagedExitCode}\n");
                builder.Append("  fi\n");
                builder.Append("  rm -rf -- \"$P\"\n");
                builder.Append("fi\n");
            }
            else
            {
                builder.Append($"v={ShellQuote.Quote(plan.VolumeName)}\n");
                builder.Append("if docker volume inspect \"$v\" >/dev/null 2>&1; then docker volume rm \"$v\" >/dev/null; fi\n");
            }
        }

        builder.Append("rm -rf -- \"$S\"\n");
        return Shell(builder.ToString());
    }

    private static void Append(StringBuilder builder, string option, string value) =>
        builder.Append(' ').Append(option).Append(' ').Append(ShellQuote.Quote(value));

    private static string Shell(string script) => "sh -c " + ShellQuote.Quote(script);
}
