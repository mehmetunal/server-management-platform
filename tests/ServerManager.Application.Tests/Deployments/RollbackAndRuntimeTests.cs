using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Deployments;

public class RollbackPlannerTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static (DeploymentProject Project, Deployment Deployment) Succeeded()
    {
        var project = new DeploymentProject { Name = "api", Slug = "api" };
        var deployment = new Deployment { ProjectId = project.Id, Status = DeploymentStatus.Succeeded, CommitSha = Sha };
        return (project, deployment);
    }

    [Fact]
    public void Successful_deployment_with_commit_is_a_valid_target()
    {
        var (project, deployment) = Succeeded();

        Assert.Null(RollbackPlanner.Validate(deployment, project));
    }

    [Theory]
    [InlineData(DeploymentStatus.Failed)]
    [InlineData(DeploymentStatus.Cancelled)]
    [InlineData(DeploymentStatus.Started)]
    public void Only_successful_deployments_can_be_targets(DeploymentStatus status)
    {
        var (project, deployment) = Succeeded();
        deployment.Status = status;

        Assert.NotNull(RollbackPlanner.Validate(deployment, project));
    }

    [Fact]
    public void Missing_commit_deleted_project_or_foreign_project_is_rejected()
    {
        var (project, deployment) = Succeeded();

        Assert.NotNull(RollbackPlanner.Validate(deployment, null));
        Assert.NotNull(RollbackPlanner.Validate(new Deployment { ProjectId = Guid.NewGuid(), Status = DeploymentStatus.Succeeded, CommitSha = Sha }, project));
        deployment.CommitSha = null;
        Assert.NotNull(RollbackPlanner.Validate(deployment, project));
    }

    [Theory]
    [InlineData(DeploymentBuildType.Dockerfile, true, RollbackStrategy.RunExistingImage)]
    [InlineData(DeploymentBuildType.Dockerfile, false, RollbackStrategy.RebuildCommit)]
    [InlineData(DeploymentBuildType.DockerCompose, true, RollbackStrategy.RebuildCommit)]
    [InlineData(DeploymentBuildType.Commands, false, RollbackStrategy.RebuildCommit)]
    public void Existing_image_is_reused_only_for_dockerfile_projects(DeploymentBuildType type, bool imageExists, RollbackStrategy expected) =>
        Assert.Equal(expected, RollbackPlanner.Choose(type, imageExists));

    [Fact]
    public void Current_deployment_is_not_offered_as_rollback_target()
    {
        var (_, deployment) = Succeeded();

        Assert.False(RollbackPlanner.CanOfferRollback(deployment, deployment.Id));
        Assert.True(RollbackPlanner.CanOfferRollback(deployment, Guid.NewGuid()));
        deployment.Status = DeploymentStatus.Failed;
        Assert.False(RollbackPlanner.CanOfferRollback(deployment, Guid.NewGuid()));
    }
}

public class RuntimeLogFilterTests
{
    [Theory]
    [InlineData("2026-10-05 ERROR connection refused")]
    [InlineData("fatal: repository not found")]
    [InlineData("panic: runtime error")]
    [InlineData("System.NullReferenceException: Object reference")]
    [InlineData("[WARN] disk is almost full")]
    [InlineData("level=warning msg=slow")]
    [InlineData("Unhandled Exception")]
    public void Problem_lines_match(string line) => Assert.True(RuntimeLogFilter.IsProblem(line));

    [Theory]
    [InlineData("GET /health 200 3ms")]
    [InlineData("info: Application started")]
    [InlineData("")]
    [InlineData(null)]
    public void Normal_lines_do_not_match(string? line) => Assert.False(RuntimeLogFilter.IsProblem(line));
}

public class ProjectContainersTests
{
    private static DockerContainerDto Container(string name, string? project = null, string state = "running") =>
        new() { Id = name, Name = name, ComposeProject = project, State = state, Image = "img" };

    [Fact]
    public void Compose_project_containers_are_selected_by_label()
    {
        var all = new[]
        {
            Container("sm-api-web-1", "sm-api"),
            Container("sm-api-db-1", "sm-api"),
            Container("custom-name", "sm-api"),
            Container("sm-api-web-other-1", "sm-api-web"),
            Container("sm-api")
        };

        var selected = ProjectContainers.Select("api", DeploymentBuildType.DockerCompose, all);

        Assert.Equal(["custom-name", "sm-api-db-1", "sm-api-web-1"], selected.Select(c => c.Name));
        Assert.Equal(["custom-name", "db", "web"], selected.Select(c => c.Service));
    }

    [Fact]
    public void Dockerfile_project_selects_its_single_container()
    {
        var selected = ProjectContainers.Select("api", DeploymentBuildType.Dockerfile, [Container("sm-api"), Container("sm-api-2"), Container("sm-api-web-1", "sm-api")]);

        Assert.Equal("sm-api", Assert.Single(selected).Name);
    }

    [Fact]
    public void Command_projects_have_no_known_containers() =>
        Assert.Empty(ProjectContainers.Select("api", DeploymentBuildType.Commands, [Container("sm-api")]));

    [Theory]
    [InlineData("sm-api-web-1", "web")]
    [InlineData("sm-api-my-worker-12", "my-worker")]
    [InlineData("sm-api-web", "web")]
    [InlineData("other", "other")]
    public void Service_name_is_derived_from_default_container_name(string container, string expected) =>
        Assert.Equal(expected, ProjectContainers.ServiceName("sm-api", container));
}
