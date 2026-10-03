using ServerManager.Application.Dokploy;
using ServerManager.Application.DTOs.Dokploy;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Dokploy;

public class DokployStatusEvaluatorTests
{
    private static readonly DokployHttpProbeResult Reachable = new(true, 42, "ok");
    private static readonly DokployHttpProbeResult Unreachable = new(false, null, "Panel bağlantıyı reddetti.");

    private static DokployServiceDto Service(string name, int running = 1, int desired = 1) =>
        new() { Name = name, Image = "dokploy/dokploy:v0.25.3", RunningReplicas = running, DesiredReplicas = desired };

    private static DokployContainerDto Traefik(string state = "running") =>
        new() { Name = "dokploy-traefik", Image = "traefik:v3", State = state, Status = "Up" };

    private static DokployHostStatusDto Host(
        IReadOnlyList<DokployServiceDto>? services = null,
        IReadOnlyList<DokployContainerDto>? containers = null,
        bool healthy = true) => new()
    {
        DockerAvailable = true,
        SwarmActive = true,
        Services = services ?? [Service("dokploy"), Service("dokploy-postgres"), Service("dokploy-redis")],
        Containers = containers ?? [Traefik()],
        LocalHealthy = healthy
    };

    [Fact]
    public void Healthy_installation_is_running()
    {
        var (status, _) = DokployStatusEvaluator.Evaluate(Host(), null, Reachable);

        Assert.Equal(DokployStatus.Running, status);
    }

    [Fact]
    public void Missing_ssh_with_reachable_panel_is_running()
    {
        Assert.Equal(DokployStatus.Running, DokployStatusEvaluator.Evaluate(null, "timeout", Reachable).Status);
        Assert.Equal(DokployStatus.Unreachable, DokployStatusEvaluator.Evaluate(null, "timeout", Unreachable).Status);
    }

    [Fact]
    public void Docker_error_without_panel_is_unknown()
    {
        var host = new DokployHostStatusDto { DockerAvailable = false, DockerError = "permission denied" };

        Assert.Equal(DokployStatus.Unknown, DokployStatusEvaluator.Evaluate(host, null, Unreachable).Status);
    }

    [Fact]
    public void Missing_service_is_not_installed()
    {
        var (status, _) = DokployStatusEvaluator.Evaluate(Host(services: [Service("other")]), null, Unreachable);

        Assert.Equal(DokployStatus.NotInstalled, status);
    }

    [Fact]
    public void Stopped_dokploy_service_is_stopped()
    {
        var (status, message) = DokployStatusEvaluator.Evaluate(Host(services: [Service("dokploy", running: 0)]), null, Unreachable);

        Assert.Equal(DokployStatus.Stopped, status);
        Assert.Contains("0/1", message);
    }

    [Fact]
    public void Failing_local_health_is_degraded()
    {
        Assert.Equal(DokployStatus.Degraded, DokployStatusEvaluator.Evaluate(Host(healthy: false), null, Reachable).Status);
    }

    [Fact]
    public void Stopped_dependency_or_traefik_is_degraded()
    {
        var (dbStatus, dbMessage) = DokployStatusEvaluator.Evaluate(Host(services: [Service("dokploy"), Service("dokploy-postgres", running: 0)]), null, Reachable);
        var (traefikStatus, traefikMessage) = DokployStatusEvaluator.Evaluate(Host(containers: [Traefik("exited")]), null, Reachable);

        Assert.Equal(DokployStatus.Degraded, dbStatus);
        Assert.Contains("dokploy-postgres", dbMessage);
        Assert.Equal(DokployStatus.Degraded, traefikStatus);
        Assert.Contains("dokploy-traefik", traefikMessage);
    }

    [Fact]
    public void Traefik_running_as_service_does_not_need_container()
    {
        var host = Host(services: [Service("dokploy"), Service("dokploy-traefik")], containers: []);

        Assert.Equal(DokployStatus.Running, DokployStatusEvaluator.Evaluate(host, null, Reachable).Status);
    }

    [Fact]
    public void Healthy_host_but_unreachable_panel_is_degraded()
    {
        var (status, message) = DokployStatusEvaluator.Evaluate(Host(), null, Unreachable);

        Assert.Equal(DokployStatus.Degraded, status);
        Assert.Contains("reddetti", message);
    }
}
