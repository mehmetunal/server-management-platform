using System.Globalization;
using System.Text;
using ServerManager.Application.ManagedServices;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.ManagedServices;

/// <summary>
/// Docker'ın yayınladığı portlar UFW/INPUT zincirini atlar; kısıtlama DOCKER-USER zincirine yazılır. Her kural servisin
/// etiketini yorum olarak taşır (<c>sm-svc-&lt;slug&gt;</c>); kurallar önce etiketle silinir, sonra yeniden eklenir (idempotent).
/// Eşleşme NAT öncesi hedef porta göre yapılır (<c>--ctstate DNAT --ctorigdstport</c>): yalnızca sunucu portuna dışarıdan gelen
/// bağlantılar etkilenir, Docker ağı içinden container adıyla yapılan bağlantılar etkilenmez.
/// </summary>
internal static class ServiceFirewallCommands
{
    public const int FirewallUnavailableExitCode = 7;
    public const string Chain = "DOCKER-USER";
    public const string SystemdUnit = "sm-services-firewall.service";
    public const string SystemdUnitPath = "/etc/systemd/system/" + SystemdUnit;

    /// <summary>Etiketli kuralları satır numarasıyla siler (büyükten küçüğe); IPv4 ve varsa IPv6.</summary>
    private static string RemoveFunction(string tag) =>
        $"tag={ShellQuote.Quote(tag)}\n" +
        "sm_remove() {\n" +
        "  command -v \"$1\" >/dev/null 2>&1 || return 0\n" +
        $"  \"$1\" -L {Chain} -n >/dev/null 2>&1 || return 0\n" +
        $"  nums=$(\"$1\" -L {Chain} -n --line-numbers 2>/dev/null | awk -v t=\"/* $tag */\" 'index($0, t) {{ print $1 }}' | sort -rn)\n" +
        "  for n in $nums; do\n" +
        $"    \"$1\" -D {Chain} \"$n\"\n" +
        "  done\n" +
        "}\n";

    /// <summary>
    /// Sunucuya yazılan ve çalıştırılan bağımsız betik: etiketli kuralları siler, her port için önce DROP, sonra izinli kaynaklar
    /// için RETURN kuralını en üste ekler (RETURN'ler DROP'un üstünde kalır). IPv4 zinciri yoksa betik
    /// <see cref="FirewallUnavailableExitCode"/> ile çıkar; port açık kalmasın diye kurulum başarısız sayılır.
    /// </summary>
    public static string ApplyScript(ServiceFirewallPlan plan)
    {
        var builder = new StringBuilder();
        builder.Append("#!/bin/sh\n");
        builder.Append("# Mag Server Manager tarafından yazıldı; elle düzenlemeyin. Servis: ").Append(plan.Tag).Append('\n');
        builder.Append("set -u\n");
        builder.Append(RemoveFunction(plan.Tag));
        builder.Append("sm_remove iptables\n");
        builder.Append("sm_remove ip6tables\n");
        if (!plan.HasRules)
            return builder.ToString();

        builder.Append($"if ! command -v iptables >/dev/null 2>&1 || ! iptables -L {Chain} -n >/dev/null 2>&1; then\n");
        builder.Append($"  echo '{Chain} zinciri bulunamadı; Docker iptables kullanmıyor olabilir.' >&2\n");
        builder.Append($"  exit {FirewallUnavailableExitCode.ToString(CultureInfo.InvariantCulture)}\n");
        builder.Append("fi\n");
        builder.Append("set -e\n");

        var v4 = plan.AllowedSources.Where(c => !ServiceValidation.IsIpv6Cidr(c)).ToList();
        var v6 = plan.AllowedSources.Where(ServiceValidation.IsIpv6Cidr).ToList();
        AppendRules(builder, "iptables", plan, v4);

        builder.Append($"if command -v ip6tables >/dev/null 2>&1 && ip6tables -L {Chain} -n >/dev/null 2>&1; then\n");
        AppendRules(builder, "ip6tables", plan, v6, "  ");
        builder.Append("fi\n");
        return builder.ToString();
    }

    private static void AppendRules(StringBuilder builder, string binary, ServiceFirewallPlan plan, IReadOnlyList<string> sources, string indent = "")
    {
        var comment = $"-m comment --comment {ShellQuote.Quote(plan.Tag)}";
        foreach (var port in plan.Ports)
        {
            var match = $"-p tcp -m conntrack --ctstate DNAT --ctorigdstport {port.ToString(CultureInfo.InvariantCulture)} --ctdir ORIGINAL";
            builder.Append(indent).Append($"{binary} -I {Chain} 1 {match} {comment} -j DROP\n");
            foreach (var source in sources)
                builder.Append(indent).Append($"{binary} -I {Chain} 1 -s {ShellQuote.Quote(source)} {match} {comment} -j RETURN\n");
        }
    }

    /// <summary>
    /// Kural betiğini (stdin) servis klasörüne yazar ve çalıştırır. Kurallar yeniden başlatmada silinmesin diye systemd varsa
    /// Docker'dan sonra tüm servis betiklerini çalıştıran bir birim kurulur. Kural yoksa betik silinir, yalnızca eski kurallar kaldırılır.
    /// </summary>
    public static string Apply(ServiceFirewallPlan plan, string slug)
    {
        var directory = ShellQuote.Quote(ManagedServiceNames.StateDirectory(slug));
        var script = ShellQuote.Quote(ManagedServiceNames.FirewallScript(slug));
        if (!plan.HasRules)
        {
            return Shell(
                "set -u\n" +
                "f=$(mktemp)\n" +
                "cat > \"$f\"\n" +
                "sh \"$f\"; s=$?\n" +
                "rm -f \"$f\"\n" +
                $"rm -f -- {script}\n" +
                "exit $s\n");
        }

        return Shell(
            "set -eu\n" +
            "umask 077\n" +
            $"mkdir -p -- {directory}\n" +
            $"cat > {script}\n" +
            $"chmod 700 {script}\n" +
            $"sh {script}\n" +
            "if command -v systemctl >/dev/null 2>&1 && [ -d /etc/systemd/system ]; then\n" +
            $"  if [ ! -f {SystemdUnitPath} ]; then\n" +
            $"    cat > {SystemdUnitPath} <<'UNIT'\n" +
            SystemdUnitText +
            "UNIT\n" +
            $"    chmod 644 {SystemdUnitPath}\n" +
            "    systemctl daemon-reload >/dev/null 2>&1 || true\n" +
            $"    systemctl enable {SystemdUnit} >/dev/null 2>&1 || true\n" +
            "  fi\n" +
            "fi\n");
    }

    /// <summary>Servis kaldırılırken kuralları ve betiği siler.</summary>
    public static string Remove(string slug) =>
        Shell(
            "set -u\n" +
            RemoveFunction(ManagedServiceNames.FirewallTag(slug)) +
            "sm_remove iptables\n" +
            "sm_remove ip6tables\n" +
            $"rm -f -- {ShellQuote.Quote(ManagedServiceNames.FirewallScript(slug))}\n");

    /// <summary>Servise ait kural sayısını <c>SM_FW=&lt;sayı&gt;</c> olarak yazar.</summary>
    public static string Count(string slug) =>
        Shell(
            $"t={ShellQuote.Quote("/* " + ManagedServiceNames.FirewallTag(slug) + " */")}\n" +
            "c=0\n" +
            "for b in iptables ip6tables; do\n" +
            "  command -v \"$b\" >/dev/null 2>&1 || continue\n" +
            $"  n=$(\"$b\" -L {Chain} -n 2>/dev/null | grep -cF -- \"$t\" || true)\n" +
            "  c=$((c + ${n:-0}))\n" +
            "done\n" +
            "echo \"SM_FW=$c\"\n");

    private const string SystemdUnitText =
        "[Unit]\n" +
        "Description=Mag Server Manager servis guvenlik duvari kurallari\n" +
        "After=docker.service\n" +
        "Wants=docker.service\n" +
        "\n" +
        "[Service]\n" +
        "Type=oneshot\n" +
        "RemainAfterExit=yes\n" +
        "ExecStart=/bin/sh -c 'for f in " + ManagedServiceNames.StateRoot + "/*/firewall.sh; do [ -f \"$f\" ] && /bin/sh \"$f\"; done; exit 0'\n" +
        "\n" +
        "[Install]\n" +
        "WantedBy=multi-user.target\n";

    private static string Shell(string script) => "sh -c " + ShellQuote.Quote(script);
}
