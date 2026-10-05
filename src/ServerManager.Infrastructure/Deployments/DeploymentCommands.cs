using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Deployments;

/// <summary>
/// Hedef sunucuda çalışan komutlar. Erişim anahtarı ve ortam değişkenleri komut satırına yazılmaz; stdin ile verilir
/// (bkz. <see cref="TokenInput"/>). Git komutları SSH kullanıcısıyla, Docker komutları sunucu ayarındaki sudo tercihiyle çalışır.
/// </summary>
public static class DeploymentCommands
{
    public const int WorkspaceNotEmptyExitCode = 3;
    public const string WorkspaceInitializedMarker = "sm:initialized";

    /// <summary>Panelin oluşturduğu proje klasörlerine yazılan işaret dosyası. Kalıcı silme yalnızca bu dosya varsa klasörü siler.</summary>
    public const string ManagedMarkerFile = ".sm-managed";

    public const int NotManagedExitCode = 5;
    public const char CommitFieldSeparator = '\u001f';

    private const string GitExports =
        "export GIT_TERMINAL_PROMPT=0\n" +
        "export GIT_SSH_COMMAND='ssh -o BatchMode=yes -o StrictHostKeyChecking=accept-new'\n";

    private const string ReadToken = "IFS= read -r SM_GIT_TOKEN || true\nexport SM_GIT_TOKEN\n";

    public static string? TokenInput(GitSource source) =>
        string.IsNullOrEmpty(source.AccessToken) ? null : source.AccessToken + "\n";

    public static string ListBranches(GitSource source) =>
        Shell("set -e\n" + Credentials(source) + GitExports + $"{Git(source)} ls-remote --heads {ShellQuote.Quote(source.RepositoryUrl)}\n");

    /// <summary>
    /// Klasör yoksa oluşturulur; boşsa git deposu başlatılır ve <see cref="ManagedMarkerFile"/> yazılır. Boş olmayan ve git deposu
    /// olmayan klasöre dokunulmaz (<see cref="WorkspaceNotEmptyExitCode"/> ile çıkar); mevcut dosyalar hiçbir durumda silinmez.
    /// </summary>
    public static string PrepareWorkspace(string deployPath) =>
        Shell(
            "set -e\n" +
            $"P={ShellQuote.Quote(deployPath)}\n" +
            "mkdir -p -- \"$P\"\n" +
            "if [ -e \"$P/.git\" ]; then exit 0; fi\n" +
            $"if [ -n \"$(ls -A -- \"$P\")\" ]; then echo 'not empty' >&2; exit {WorkspaceNotEmptyExitCode}; fi\n" +
            "git -C \"$P\" init -q\n" +
            $": > \"$P/{ManagedMarkerFile}\"\n" +
            "mkdir -p -- \"$P/.git/info\"\n" +
            $"echo {ManagedMarkerFile} >> \"$P/.git/info/exclude\"\n" +
            $"echo {WorkspaceInitializedMarker}\n");

    /// <summary>Kaynağı sığ (depth 1) çeker ve çalışma ağacını o commit'e alır.</summary>
    public static string FetchSource(DeploymentPlan plan)
    {
        var url = ShellQuote.Quote(plan.Source.RepositoryUrl);
        var reference = plan.Commit ?? "refs/heads/" + plan.Branch;
        return Shell(
            "set -e\n" +
            Credentials(plan.Source) +
            GitExports +
            $"P={ShellQuote.Quote(plan.DeployPath)}\n" +
            $"git -C \"$P\" remote set-url origin {url} 2>/dev/null || git -C \"$P\" remote add origin {url}\n" +
            $"{Git(plan.Source)} -C \"$P\" fetch --depth 1 --no-tags --progress origin {ShellQuote.Quote(reference)}\n" +
            "git -C \"$P\" checkout -q --force --detach FETCH_HEAD\n");
    }

    /// <summary>Çalışma ağacındaki commit'i <c>%H%x1f%an%x1f%s</c> biçiminde yazar.</summary>
    public static string ReadCommit(string deployPath) =>
        Shell($"git -C {ShellQuote.Quote(deployPath)} log -1 --format='%H%x1f%an%x1f%s'");

    public static string WriteEnvironmentFile(string deployPath) =>
        Shell($"umask 077 && cat > {ShellQuote.Quote(DeployPaths.Combine(deployPath, ".env"))}");

    public static string ComposeBuild(DeploymentPlan plan) => Compose(plan) + " build";

    public static string ComposeUp(DeploymentPlan plan, bool noBuild = false) =>
        Compose(plan) + (noBuild ? " up -d --no-build --remove-orphans" : " up -d --remove-orphans");

    public static string DockerBuild(DeploymentPlan plan, string commitSha)
    {
        var image = DeploymentNames.ImageName(plan.Slug);
        return $"docker build --progress=plain -t {image}:{GitRefs.ShortSha(commitSha)} -t {image}:latest " +
               $"-f {ShellQuote.Quote(DeployPaths.Combine(plan.DeployPath, plan.DockerfilePath))} {ShellQuote.Quote(plan.DeployPath)}";
    }

    public static string DockerRemoveContainer(string slug) => $"docker rm -f {DeploymentNames.ContainerName(slug)}";

    public static string DockerRun(DeploymentPlan plan, string commitSha)
    {
        var ports = string.Concat(plan.PortMappings.Select(p => " -p " + ShellQuote.Quote(p)));
        var envFile = plan.Environment is null ? string.Empty : " --env-file " + ShellQuote.Quote(DeployPaths.Combine(plan.DeployPath, ".env"));
        var network = plan.Routes.Count == 0 ? string.Empty : " --network " + DomainNames.ProxyNetwork;
        var labels = string.Concat(plan.Routes.SelectMany(TraefikRoutes.Labels).Distinct().Select(label => " --label " + ShellQuote.Quote(label)));
        return $"docker run -d --name {DeploymentNames.ContainerName(plan.Slug)} --restart unless-stopped --label sm.project={plan.Slug}" +
               $"{network}{labels}{ports}{envFile} {DeploymentNames.ImageName(plan.Slug)}:{GitRefs.ShortSha(commitSha)}";
    }

    /// <summary>Kullanıcı komutu proje klasöründe <c>set -e</c> ile çalışır; satırlardan biri hata verirse adım başarısız olur.</summary>
    public static string RunUserCommand(string deployPath, string command) =>
        Shell($"set -e\ncd -- {ShellQuote.Quote(deployPath)}\n{command.Replace("\r\n", "\n", StringComparison.Ordinal)}\n");

    public static string ImageExists(string slug) =>
        "docker image inspect " + DeploymentNames.ImageName(slug) + ":latest";

    public static string ComposeFileExists(DeploymentPlan plan) =>
        Shell("test -f " + ShellQuote.Quote(DeployPaths.Combine(plan.DeployPath, plan.ComposeFile)));

    public static string WriteOverride(string deployPath) =>
        Shell("umask 077 && cat > " + ShellQuote.Quote(DeployPaths.Combine(deployPath, DomainNames.OverrideFileName)));

    public static string RemoveOverride(string deployPath) =>
        Shell("rm -f -- " + ShellQuote.Quote(DeployPaths.Combine(deployPath, DomainNames.OverrideFileName)));

    /// <summary>
    /// Proje klasörünü, compose projesini, panelin adlandırdığı container ve imajı ve vekil dosyalarını siler.
    /// sm-traefik ve sm-proxy ağına dokunulmaz. Klasör yolu en az iki parçalı mutlak yol olmalıdır. Klasör varsa içinde
    /// <see cref="ManagedMarkerFile"/> bulunmalıdır; yoksa (klasörü panel oluşturmadıysa) hiçbir şey silinmez ve <see cref="NotManagedExitCode"/> ile çıkılır.
    /// </summary>
    public static string RemoveProject(DeploymentPlan plan)
    {
        var path = ShellQuote.Quote(plan.DeployPath);
        var container = ShellQuote.Quote(DeploymentNames.ContainerName(plan.Slug));
        var image = ShellQuote.Quote(DeploymentNames.ImageName(plan.Slug));
        var projectName = ShellQuote.Quote(DeploymentNames.ComposeProjectName(plan.Slug));
        var dynamicDir = ShellQuote.Quote(DomainNames.DynamicDirectory);
        var slug = ShellQuote.Quote(plan.Slug);
        var compose = DeployPaths.IsValidRelativeFile(plan.ComposeFile)
            ? ShellQuote.Quote(DeployPaths.Combine(plan.DeployPath, plan.ComposeFile))
            : "''";
        var overrideFile = ShellQuote.Quote(DeployPaths.Combine(plan.DeployPath, DomainNames.OverrideFileName));

        return Shell(
            "set -eu\n" +
            $"P={path}\n" +
            "case \"$P\" in\n" +
            "  /*/*) ;;\n" +
            "  *) echo 'klasör silinemez' >&2; exit 2 ;;\n" +
            "esac\n" +
            $"if [ -L \"$P\" ]; then echo 'klasör sembolik bağlantı' >&2; exit {NotManagedExitCode}; fi\n" +
            "if [ -e \"$P\" ]; then\n" +
            $"  if [ ! -f \"$P/{ManagedMarkerFile}\" ] || [ -L \"$P/{ManagedMarkerFile}\" ]; then echo 'klasör panel tarafından oluşturulmamış' >&2; exit {NotManagedExitCode}; fi\n" +
            "fi\n" +
            "if command -v docker >/dev/null 2>&1; then\n" +
            $"  compose={compose}\n" +
            $"  override={overrideFile}\n" +
            "  if [ -n \"$compose\" ] && [ -f \"$compose\" ]; then\n" +
            $"    if [ -f \"$override\" ]; then\n" +
            $"      docker compose --project-name {projectName} --project-directory \"$P\" -f \"$compose\" -f \"$override\" down --remove-orphans --rmi local -v\n" +
            "    else\n" +
            $"      docker compose --project-name {projectName} --project-directory \"$P\" -f \"$compose\" down --remove-orphans --rmi local -v\n" +
            "    fi\n" +
            "  fi\n" +
            $"  docker rm -f {container} >/dev/null 2>&1 || true\n" +
            "  docker images --format '{{.Repository}} {{.ID}}' | while read -r repo id; do\n" +
            $"    [ \"$repo\" = {image} ] || continue\n" +
            "    docker rmi -f \"$id\" >/dev/null 2>&1 || true\n" +
            "  done\n" +
            "fi\n" +
            $"base={dynamicDir}\n" +
            $"slug={slug}\n" +
            "for f in \"$base/$slug\"--*; do\n" +
            "  [ -f \"$f\" ] || continue\n" +
            "  rm -f -- \"$f\"\n" +
            "done\n" +
            "rm -rf -- \"$P\"\n");
    }

    /// <summary>
    /// Projenin eski vekil dosyalarını siler ve stdin'deki base64 dosyaları yazar.
    /// Satır biçimi: dosya adı, base64, … ve sonunda END. Sertifika komut metninde yer almaz.
    /// </summary>
    public static string SyncProxyFiles(string slug) =>
        Shell(
            "set -eu\n" +
            "umask 077\n" +
            $"base={ShellQuote.Quote(DomainNames.DynamicDirectory)}\n" +
            $"slug={ShellQuote.Quote(slug)}\n" +
            "mkdir -p -- \"$base\"\n" +
            "for f in \"$base/$slug\"--*; do\n" +
            "  [ -f \"$f\" ] || continue\n" +
            "  rm -f -- \"$f\"\n" +
            "done\n" +
            "while IFS= read -r name; do\n" +
            "  [ \"$name\" = END ] && break\n" +
            "  case \"$name\" in\n" +
            "    \"$slug\"--*) ;;\n" +
            "    *) echo 'geçersiz vekil dosyası' >&2; exit 2 ;;\n" +
            "  esac\n" +
            "  IFS= read -r payload || exit 2\n" +
            "  printf '%s' \"$payload\" | base64 -d > \"$base/$name\"\n" +
            "  chmod 600 \"$base/$name\"\n" +
            "done\n");

    public static string ProxyStatus() =>
        Shell(
            "if ! docker inspect " + ShellQuote.Quote(DomainNames.ProxyContainer) + " >/dev/null 2>&1; then\n" +
            "  echo SM_PROXY=missing\n" +
            "  exit 0\n" +
            "fi\n" +
            "status=$(docker inspect -f '{{.State.Status}}' " + ShellQuote.Quote(DomainNames.ProxyContainer) + " 2>/dev/null || echo unknown)\n" +
            "printf 'SM_PROXY=%s\\n' \"$status\"\n");

    public const int ProxyBusyExitCode = 4;

    public static string InstallProxy(string email)
    {
        var container = ShellQuote.Quote(DomainNames.ProxyContainer);
        var network = ShellQuote.Quote(DomainNames.ProxyNetwork);
        var image = ShellQuote.Quote(DomainNames.TraefikImage);
        var emailQuoted = ShellQuote.Quote(email);
        var dynamicDir = ShellQuote.Quote(DomainNames.DynamicDirectory);
        var acmeDir = ShellQuote.Quote(DomainNames.CertificateDirectory);
        return Shell(
            "set -eu\n" +
            $"if docker inspect {container} >/dev/null 2>&1; then\n" +
            $"  running=$(docker inspect -f '{{{{.State.Running}}}}' {container})\n" +
            "  if [ \"$running\" = true ]; then echo SM_PROXY=running; exit 0; fi\n" +
            $"  docker start {container}\n" +
            "  echo SM_PROXY=started\n" +
            "  exit 0\n" +
            "fi\n" +
            "if command -v ss >/dev/null 2>&1; then\n" +
            "  busy=$(ss -lntH | awk '$4 ~ /:(80|443)$/ {print $4}')\n" +
            "  if [ -n \"$busy\" ]; then\n" +
            "    echo 'SM_PROXY=busy' >&2\n" +
            "    printf '%s\\n' \"$busy\" >&2\n" +
            $"    exit {ProxyBusyExitCode}\n" +
            "  fi\n" +
            "fi\n" +
            $"docker network inspect {network} >/dev/null 2>&1 || docker network create {network}\n" +
            $"mkdir -p -- {acmeDir} {dynamicDir}\n" +
            $"if [ ! -f {acmeDir}/acme.json ]; then touch {acmeDir}/acme.json; fi\n" +
            $"chmod 600 {acmeDir}/acme.json\n" +
            $"docker run -d --name {container} --restart unless-stopped --network {network} " +
            "-p 80:80 -p 443:443 " +
            "-v /var/run/docker.sock:/var/run/docker.sock:ro " +
            $"-v {acmeDir}:/letsencrypt -v {dynamicDir}:/etc/traefik/dynamic " +
            $"{image} " +
            "--providers.docker=true --providers.docker.exposedbydefault=false " +
            $"--providers.docker.network={DomainNames.ProxyNetwork} " +
            "--providers.file.directory=/etc/traefik/dynamic --providers.file.watch=true " +
            "--entrypoints.web.address=:80 --entrypoints.websecure.address=:443 " +
            $"--certificatesresolvers.letsencrypt.acme.email={emailQuoted} " +
            "--certificatesresolvers.letsencrypt.acme.storage=/letsencrypt/acme.json " +
            "--certificatesresolvers.letsencrypt.acme.httpchallenge.entrypoint=web\n" +
            "echo SM_PROXY=installed\n");
    }

    private static string Compose(DeploymentPlan plan)
    {
        var files = "-f " + ShellQuote.Quote(DeployPaths.Combine(plan.DeployPath, plan.ComposeFile));
        if (plan.Routes.Count > 0)
            files += " -f " + ShellQuote.Quote(DeployPaths.Combine(plan.DeployPath, DomainNames.OverrideFileName));

        return $"docker compose --progress plain --project-name {DeploymentNames.ComposeProjectName(plan.Slug)} " +
               $"--project-directory {ShellQuote.Quote(plan.DeployPath)} {files}";
    }

    private static string Credentials(GitSource source) =>
        string.IsNullOrEmpty(source.AccessToken) ? string.Empty : ReadToken;

    /// <summary>
    /// Sistemde tanımlı credential helper'lar sıfırlanır (anahtar başka yere kaydedilmesin). Anahtar varsa yalnızca
    /// ortam değişkeninden okuyan geçici bir helper eklenir; anahtar diske yazılmaz.
    /// </summary>
    private static string Git(GitSource source)
    {
        if (string.IsNullOrEmpty(source.AccessToken))
            return "git -c credential.helper=";

        var username = ShellQuote.Quote(source.Username ?? "git");
        var helper = $"!f() {{ test \"$1\" = get || exit 0; printf 'username=%s\\n' {username}; printf 'password=%s\\n' \"$SM_GIT_TOKEN\"; }}; f";
        return $"git -c credential.helper= -c {ShellQuote.Quote("credential.helper=" + helper)}";
    }

    private static string Shell(string script) => "sh -c " + ShellQuote.Quote(script);
}
