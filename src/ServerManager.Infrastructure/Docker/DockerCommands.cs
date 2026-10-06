using System.Text;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Docker;

/// <summary>
/// Docker komut metinleri. Kullanıcıdan gelen her değer <see cref="ShellQuote"/> ile kaçışlanır;
/// değerler ayrıca Application katmanında <see cref="DockerNames"/> ile doğrulanmış olmalıdır.
/// </summary>
internal static class DockerCommands
{
    private const string JsonFormat = "--format '{{json .}}'";

    public const string Info = "docker info " + JsonFormat;
    public const string ListContainers = "docker ps -a --no-trunc " + JsonFormat;
    public const string ListVolumeNames = "docker volume ls -q";
    public const string ListNetworkIds = "docker network ls -q";
    public const string DiskUsage = "docker system df " + JsonFormat;
    public const string DiskUsageVerbose = "docker system df -v " + JsonFormat;
    public const string ListImages = "docker image ls --no-trunc --digests " + JsonFormat;
    public const string ListVolumes = "docker volume ls " + JsonFormat;
    public const string ListNetworks = "docker network ls --no-trunc " + JsonFormat;

    /// <summary>Hiçbir container'ın bağlı olmadığı ağlar; Docker'ın önceden tanımlı ağları (bridge, host, none) bu filtrede gelmez.</summary>
    public const string ListUnusedNetworks = "docker network ls --no-trunc --filter dangling=true " + JsonFormat;

    /// <summary>Kullanılmayan build cache'i siler (kullanımdaki katmanlara dokunmaz); onay sorulmaz.</summary>
    public const string PruneBuildCache = "docker builder prune -f";

    private const string InteractiveShell = "if command -v bash >/dev/null 2>&1; then exec bash; else exec sh; fi";

    public static string InspectContainers(IEnumerable<string> containers) =>
        "docker container inspect " + JoinQuoted(containers);

    public static string InspectNetworks(IEnumerable<string> networks) =>
        "docker network inspect " + JoinQuoted(networks);

    public static string Stats(string? container) =>
        "docker stats --no-stream --no-trunc " + JsonFormat + (container is null ? string.Empty : " " + ShellQuote.Quote(container));

    public static string Logs(string container, int tail, string? since)
    {
        var builder = new StringBuilder("docker logs --timestamps --tail ").Append(tail);
        if (since is not null)
            builder.Append(" --since ").Append(ShellQuote.Quote(since));

        return builder.Append(' ').Append(ShellQuote.Quote(container)).ToString();
    }

    public static string ContainerAction(string container, DockerContainerAction action, bool force)
    {
        var verb = action switch
        {
            DockerContainerAction.Start => "start",
            DockerContainerAction.Stop => "stop",
            DockerContainerAction.Restart => "restart",
            DockerContainerAction.Pause => "pause",
            DockerContainerAction.Unpause => "unpause",
            DockerContainerAction.Kill => "kill",
            DockerContainerAction.Remove => force ? "rm -f" : "rm",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        return $"docker {verb} {ShellQuote.Quote(container)}";
    }

    public static string Rename(string container, string newName) =>
        $"docker rename {ShellQuote.Quote(container)} {ShellQuote.Quote(newName)}";

    public static string Pull(string reference) =>
        "docker pull -q " + ShellQuote.Quote(reference);

    public static string RemoveImage(string reference, bool force) =>
        "docker image rm " + (force ? "-f " : string.Empty) + ShellQuote.Quote(reference);

    public static string PruneImages(bool all) =>
        "docker image prune -f" + (all ? " -a" : string.Empty);

    public static string CreateVolume(string name) =>
        "docker volume create " + ShellQuote.Quote(name);

    public static string RemoveVolume(string name) =>
        "docker volume rm " + ShellQuote.Quote(name);

    public static string CreateNetwork(CreateNetworkDto dto)
    {
        var builder = new StringBuilder("docker network create --driver ").Append(ShellQuote.Quote(dto.Driver));
        if (!string.IsNullOrWhiteSpace(dto.Subnet))
            builder.Append(" --subnet ").Append(ShellQuote.Quote(dto.Subnet));
        if (!string.IsNullOrWhiteSpace(dto.Gateway))
            builder.Append(" --gateway ").Append(ShellQuote.Quote(dto.Gateway));
        if (dto.Internal)
            builder.Append(" --internal");

        return builder.Append(' ').Append(ShellQuote.Quote(dto.Name)).ToString();
    }

    public static string RemoveNetwork(string network) =>
        "docker network rm " + ShellQuote.Quote(network);

    public static string ExecShell(string container) =>
        $"docker exec -it -e TERM=xterm-256color {ShellQuote.Quote(container)} sh -c {ShellQuote.Quote(InteractiveShell)}";

    private static string JoinQuoted(IEnumerable<string> values) =>
        string.Join(' ', values.Select(ShellQuote.Quote));
}
