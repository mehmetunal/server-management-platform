using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Docker;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class ProjectRuntimeService : IProjectRuntimeService
{
    private const string NotFoundMessage = "Proje bulunamadı.";

    private readonly IDeploymentRepository _repository;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IDockerClient _dockerClient;
    private readonly DockerOptions _options;

    public ProjectRuntimeService(
        IDeploymentRepository repository,
        IServerConnectionProvider connectionProvider,
        IDockerClient dockerClient,
        IOptions<DockerOptions> options)
    {
        _repository = repository;
        _connectionProvider = connectionProvider;
        _dockerClient = dockerClient;
        _options = options.Value;
    }

    public async Task<ServiceResult<IReadOnlyList<ProjectContainerDto>>> GetContainersAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<IReadOnlyList<ProjectContainerDto>>.NotFound(NotFoundMessage);

        if (project.BuildType == DeploymentBuildType.Commands)
            return ServiceResult<IReadOnlyList<ProjectContainerDto>>.Failure("Komutla dağıtılan projenin container'ı panel tarafından bilinmez; Docker sekmesinden bakın.");

        var connection = await _connectionProvider.GetAsync(project.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<IReadOnlyList<ProjectContainerDto>>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await ListAsync(project, connection.Data!.Context, cancellationToken);
    }

    public async Task<ServiceResult<ProjectLogsDto>> GetLogsAsync(Guid projectId, ProjectLogQuery query, CancellationToken cancellationToken = default)
    {
        if (!DockerNames.IsValidContainerReference(query.Container))
            return ServiceResult<ProjectLogsDto>.Failure("Geçersiz container adı.", ServiceErrorType.Validation);

        if (!string.IsNullOrWhiteSpace(query.Since) && !DockerNames.IsValidLogSince(query.Since))
            return ServiceResult<ProjectLogsDto>.Failure("Geçersiz zaman damgası.", ServiceErrorType.Validation);

        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<ProjectLogsDto>.NotFound(NotFoundMessage);

        var connection = await _connectionProvider.GetAsync(project.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<ProjectLogsDto>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var context = connection.Data!.Context;
        var containers = await ListAsync(project, context, cancellationToken);
        if (!containers.IsSuccess)
            return ServiceResult<ProjectLogsDto>.Failure(containers.Message ?? "Container'lar okunamadı.", containers.ErrorType);

        if (!containers.Data!.Any(c => string.Equals(c.Name, query.Container, StringComparison.Ordinal)))
            return ServiceResult<ProjectLogsDto>.NotFound("Container bu projeye ait değil veya artık yok.");

        var tail = Math.Clamp(query.Tail ?? _options.DefaultLogTail, 1, Math.Max(1, _options.MaxLogTail));
        var since = string.IsNullOrWhiteSpace(query.Since) ? null : query.Since;
        var logs = await _dockerClient.GetContainerLogsAsync(context, query.Container, tail, since, cancellationToken);
        if (!logs.IsSuccess)
            return ServiceResult<ProjectLogsDto>.Failure(logs.Message ?? "Loglar alınamadı.", logs.ErrorType);

        var all = logs.Data!.Lines;
        var lines = all
            .Select(line => new ProjectLogLineDto
            {
                Timestamp = line.Timestamp,
                RawTimestamp = line.RawTimestamp,
                Text = line.Text,
                IsError = line.IsError,
                IsProblem = RuntimeLogFilter.IsProblem(line.Text)
            })
            .Where(line => !query.ProblemsOnly || line.IsProblem)
            .ToList();

        return ServiceResult<ProjectLogsDto>.Success(new ProjectLogsDto
        {
            Container = logs.Data.Container,
            Lines = lines,
            Truncated = logs.Data.Truncated,
            TotalLines = all.Count,
            LastRawTimestamp = all.LastOrDefault(line => line.RawTimestamp is not null)?.RawTimestamp
        });
    }

    private async Task<ServiceResult<IReadOnlyList<ProjectContainerDto>>> ListAsync(DeploymentProject project, DTOs.Ssh.RemoteExecutionContext context, CancellationToken cancellationToken)
    {
        var containers = await _dockerClient.GetContainersAsync(context, cancellationToken);
        if (!containers.IsSuccess)
            return ServiceResult<IReadOnlyList<ProjectContainerDto>>.Failure(containers.Message ?? "Container'lar okunamadı.", containers.ErrorType);

        return ServiceResult<IReadOnlyList<ProjectContainerDto>>.Success(ProjectContainers.Select(project.Slug, project.BuildType, containers.Data!));
    }
}
