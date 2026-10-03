using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Tests.TestData;
using ServerManager.Application.Validators.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Validators;

public class ProjectDtoValidatorTests
{
    private readonly CreateProjectDtoValidator _validator = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<IReadOnlyList<string>> InvalidPropertiesAsync(CreateProjectDto dto)
    {
        var result = await _validator.ValidateAsync(dto, Ct);
        return result.Errors.Select(e => e.PropertyName).Distinct().ToList();
    }

    [Fact]
    public async Task Valid_compose_project_passes()
    {
        var result = await _validator.ValidateAsync(ProjectTestData.ValidCreateDto(Guid.NewGuid()), Ct);

        Assert.True(result.IsValid, string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Required_fields_are_reported()
    {
        var properties = await InvalidPropertiesAsync(new CreateProjectDto { Branch = "", ComposeFile = "" });

        Assert.Contains(nameof(CreateProjectDto.ServerId), properties);
        Assert.Contains(nameof(CreateProjectDto.Name), properties);
        Assert.Contains(nameof(CreateProjectDto.RepositoryUrl), properties);
        Assert.Contains(nameof(CreateProjectDto.Branch), properties);
        Assert.Contains(nameof(CreateProjectDto.DeployPath), properties);
        Assert.Contains(nameof(CreateProjectDto.ComposeFile), properties);
    }

    [Fact]
    public async Task Token_is_only_allowed_for_https_repositories()
    {
        var dto = ProjectTestData.ValidCreateDto(Guid.NewGuid());
        dto.RepositoryUrl = "git@github.com:acme/api.git";
        dto.AccessToken = "ghp_x";

        Assert.Contains(nameof(CreateProjectDto.AccessToken), await InvalidPropertiesAsync(dto));
    }

    [Fact]
    public async Task Token_with_control_characters_is_rejected()
    {
        var dto = ProjectTestData.ValidCreateDto(Guid.NewGuid());
        dto.AccessToken = "ghp_x\nmalicious";

        Assert.Contains(nameof(CreateProjectDto.AccessToken), await InvalidPropertiesAsync(dto));
    }

    [Fact]
    public async Task Dockerfile_project_validates_dockerfile_and_ports()
    {
        var dto = ProjectTestData.ValidCreateDto(Guid.NewGuid());
        dto.BuildType = DeploymentBuildType.Dockerfile;
        dto.ComposeFile = "../ignored";
        dto.DockerfilePath = "../Dockerfile";
        dto.PortMappings = "8080";

        var properties = await InvalidPropertiesAsync(dto);

        Assert.Contains(nameof(CreateProjectDto.DockerfilePath), properties);
        Assert.Contains(nameof(CreateProjectDto.PortMappings), properties);
        Assert.DoesNotContain(nameof(CreateProjectDto.ComposeFile), properties);
    }

    [Fact]
    public async Task Commands_project_requires_deploy_command()
    {
        var dto = ProjectTestData.ValidCreateDto(Guid.NewGuid());
        dto.BuildType = DeploymentBuildType.Commands;

        Assert.Contains(nameof(CreateProjectDto.DeployCommand), await InvalidPropertiesAsync(dto));

        dto.DeployCommand = "systemctl restart api";
        Assert.Empty(await InvalidPropertiesAsync(dto));
    }

    [Theory]
    [InlineData(nameof(CreateProjectDto.Name), "api; rm")]
    [InlineData(nameof(CreateProjectDto.Branch), "--upload-pack=x")]
    [InlineData(nameof(CreateProjectDto.DeployPath), "/etc/api")]
    [InlineData(nameof(CreateProjectDto.GitUsername), "user name")]
    [InlineData(nameof(CreateProjectDto.Environment), "not valid")]
    public async Task Unsafe_values_are_rejected(string property, string value)
    {
        var dto = ProjectTestData.ValidCreateDto(Guid.NewGuid());
        typeof(CreateProjectDto).GetProperty(property)!.SetValue(dto, value);

        Assert.Contains(property, await InvalidPropertiesAsync(dto));
    }

    [Fact]
    public async Task Update_requires_id()
    {
        var dto = new UpdateProjectDto
        {
            ServerId = Guid.NewGuid(),
            Name = "api",
            RepositoryUrl = "https://github.com/acme/api.git",
            DeployPath = "/srv/apps/api"
        };

        var result = await new UpdateProjectDtoValidator().ValidateAsync(dto, Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProjectDto.Id));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" " + ProjectTestData.Sha + " ", true)]
    [InlineData("0123456", false)]
    [InlineData("--all", false)]
    public async Task Start_commit_must_be_full_sha_when_given(string? commit, bool expected)
    {
        var result = await new StartDeploymentDtoValidator().ValidateAsync(new StartDeploymentDto { CommitSha = commit }, Ct);

        Assert.Equal(expected, result.IsValid);
    }
}
