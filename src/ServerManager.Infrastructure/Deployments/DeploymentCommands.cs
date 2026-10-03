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
    /// Klasör yoksa oluşturulur; boşsa git deposu başlatılır. Boş olmayan ve git deposu olmayan klasöre dokunulmaz
    /// (<see cref="WorkspaceNotEmptyExitCode"/> ile çıkar); mevcut dosyalar hiçbir durumda silinmez.
    /// </summary>
    public static string PrepareWorkspace(string deployPath) =>
        Shell(
            "set -e\n" +
            $"P={ShellQuote.Quote(deployPath)}\n" +
            "mkdir -p -- \"$P\"\n" +
            "if [ -e \"$P/.git\" ]; then exit 0; fi\n" +
            $"if [ -n \"$(ls -A -- \"$P\")\" ]; then echo 'not empty' >&2; exit {WorkspaceNotEmptyExitCode}; fi\n" +
            "git -C \"$P\" init -q\n" +
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

    public static string ComposeUp(DeploymentPlan plan) => Compose(plan) + " up -d --remove-orphans";

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
        return $"docker run -d --name {DeploymentNames.ContainerName(plan.Slug)} --restart unless-stopped --label sm.project={plan.Slug}" +
               $"{ports}{envFile} {DeploymentNames.ImageName(plan.Slug)}:{GitRefs.ShortSha(commitSha)}";
    }

    /// <summary>Kullanıcı komutu proje klasöründe <c>set -e</c> ile çalışır; satırlardan biri hata verirse adım başarısız olur.</summary>
    public static string RunUserCommand(string deployPath, string command) =>
        Shell($"set -e\ncd -- {ShellQuote.Quote(deployPath)}\n{command.Replace("\r\n", "\n", StringComparison.Ordinal)}\n");

    private static string Compose(DeploymentPlan plan) =>
        $"docker compose --progress plain --project-name {DeploymentNames.ComposeProjectName(plan.Slug)} " +
        $"--project-directory {ShellQuote.Quote(plan.DeployPath)} -f {ShellQuote.Quote(DeployPaths.Combine(plan.DeployPath, plan.ComposeFile))}";

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
