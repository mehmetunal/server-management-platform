using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Auditing;
using ServerManager.Application.Commands;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Commands;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Commands;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class CommandRunService : ICommandRunService
{
    private const string NotFoundMessage = "Komut kaydı bulunamadı.";

    private readonly ICommandRunRepository _repository;
    private readonly IServerGroupRepository _serverLookup;
    private readonly IServerTemplateRepository _templateRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CommandRunRequestDto> _validator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public CommandRunService(
        ICommandRunRepository repository,
        IServerGroupRepository serverLookup,
        IServerTemplateRepository templateRepository,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<CommandRunRequestDto> validator,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _serverLookup = serverLookup;
        _templateRepository = templateRepository;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<CommandRunListItemDto>> SearchAsync(CommandRunFilterDto filter, CancellationToken cancellationToken = default)
    {
        var runs = await _repository.SearchAsync(filter, cancellationToken);
        return runs.Map(ToListItem);
    }

    public async Task<ServiceResult<CommandRunDetailsDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetWithTargetsAsync(id, cancellationToken);
        if (run is null)
            return ServiceResult<CommandRunDetailsDto>.NotFound(NotFoundMessage);

        var targets = run.Targets
            .OrderBy(t => t.ServerName, StringComparer.OrdinalIgnoreCase)
            .Select(t => new CommandRunTargetDto(
                t.Id, t.ServerId, t.ServerName, t.Status, t.ExitCode, t.Output, t.OutputTruncated,
                t.ErrorMessage, t.StartedAt, t.CompletedAt, t.DurationMs))
            .ToList();

        return ServiceResult<CommandRunDetailsDto>.Success(new CommandRunDetailsDto(ToListItem(run), run.TimeoutSeconds, targets));
    }

    public async Task<ServiceResult<Guid>> BeginAsync(CommandRunRequestDto dto, CancellationToken cancellationToken = default)
    {
        dto.Command = (dto.Command ?? string.Empty).Replace("\r\n", "\n").Trim();
        dto.ServerIds = (dto.ServerIds ?? []).Distinct().ToList();

        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var servers = await _serverLookup.GetServersAsync(dto.ServerIds, cancellationToken);
        if (servers.Count != dto.ServerIds.Count)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.ServerIds), "Seçilen sunuculardan bazıları bulunamadı. Sayfayı yenileyip tekrar seçin.");

        string? templateName = null;
        if (dto.TemplateId is { } templateId)
        {
            var template = await _templateRepository.GetAsync(templateId, cancellationToken);
            if (template is null)
                return ServiceResult<Guid>.ValidationFailure(nameof(dto.TemplateId), "Seçilen şablon bulunamadı.");
            if (template.Kind != ServerTemplateKind.Script)
                return ServiceResult<Guid>.ValidationFailure(nameof(dto.TemplateId), "cloud-init şablonları yalnızca sunucu oluştururken kullanılabilir.");
            templateName = template.Name;
        }

        var run = new CommandRun
        {
            Command = dto.Command,
            TemplateId = dto.TemplateId,
            TemplateName = templateName,
            UseSudo = dto.UseSudo,
            TimeoutSeconds = dto.TimeoutSeconds,
            Status = CommandRunStatus.Running,
            TargetCount = servers.Count,
            StartedAt = UtcNow,
            UserId = _currentUser.UserId,
            UserName = _currentUser.UserName
        };
        foreach (var server in servers.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            run.Targets.Add(new CommandRunTarget { RunId = run.Id, ServerId = server.Id, ServerName = server.Name });

        await _repository.AddAsync(run, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var details = $"Sunucular ({servers.Count}): {string.Join(", ", run.Targets.Select(t => t.ServerName))}"
            + (dto.UseSudo ? " | sudo" : string.Empty)
            + (templateName is null ? string.Empty : $" | Şablon: {templateName}")
            + $" | Komut: {dto.Command}";
        await _auditLogService.LogAsync(
            new AuditEntry(AuditActions.CommandRun, AuditEntityTypes.CommandRun, run.Id.ToString(), TextHelper.Truncate(FirstLine(dto.Command), 200), TextHelper.Truncate(details, 2000)),
            cancellationToken);

        return ServiceResult<Guid>.Success(run.Id, $"Komut {servers.Count} sunucuda çalıştırılıyor.");
    }

    public async Task ExecuteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetWithTargetsAsync(runId, CancellationToken.None);
        if (run is null || run.Status != CommandRunStatus.Running)
            return;

        var timeout = TimeSpan.FromSeconds(run.TimeoutSeconds);
        using var saveLock = new SemaphoreSlim(1, 1);

        async Task SaveAsync(Action apply)
        {
            await saveLock.WaitAsync(CancellationToken.None);
            try
            {
                apply();
                await _repository.SaveChangesAsync(CancellationToken.None);
            }
            finally
            {
                saveLock.Release();
            }
        }

        try
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = CommandRunRules.Parallelism, CancellationToken = cancellationToken };
            await Parallel.ForEachAsync(run.Targets.Where(t => t.Status == CommandTargetStatus.Pending).ToList(), options, async (target, ct) =>
            {
                await SaveAsync(() =>
                {
                    target.Status = CommandTargetStatus.Running;
                    target.StartedAt = UtcNow;
                });

                var stopwatch = Stopwatch.StartNew();
                ScriptExecutionResult result;
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var executor = scope.ServiceProvider.GetRequiredService<IServerScriptExecutor>();
                    result = await executor.ExecuteAsync(target.ServerId, run.Command, run.UseSudo, timeout, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    result = new ScriptExecutionResult(false, null, false, string.Empty, "Komut çalıştırılırken beklenmeyen bir hata oluştu.");
                }

                stopwatch.Stop();
                await SaveAsync(() => ApplyResult(run, target, result, stopwatch.ElapsedMilliseconds));
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await SaveAsync(() => MarkInterrupted(run));
            return;
        }

        await SaveAsync(() =>
        {
            run.Status = CommandRunStatus.Completed;
            run.CompletedAt = UtcNow;
        });
    }

    public async Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default)
    {
        var runs = await _repository.GetRunningAsync(cancellationToken);
        foreach (var run in runs)
            MarkInterrupted(run);

        if (runs.Count > 0)
            await _repository.SaveChangesAsync(cancellationToken);
        return runs.Count;
    }

    private void ApplyResult(CommandRun run, CommandRunTarget target, ScriptExecutionResult result, long elapsedMs)
    {
        var (output, truncated) = CommandRunRules.TruncateOutput(result.Output);
        target.Output = output.Length == 0 ? null : output;
        target.OutputTruncated = truncated;
        target.ExitCode = result.ExitCode;
        target.ErrorMessage = TextHelper.Truncate(result.ErrorMessage, 1000);
        target.CompletedAt = UtcNow;
        target.DurationMs = elapsedMs;
        target.Status = result switch
        {
            { IsSuccess: true } => CommandTargetStatus.Succeeded,
            { TimedOut: true } => CommandTargetStatus.TimedOut,
            _ => CommandTargetStatus.Failed
        };

        if (target.Status == CommandTargetStatus.Succeeded)
            run.SucceededCount++;
        else
            run.FailedCount++;
    }

    private void MarkInterrupted(CommandRun run)
    {
        foreach (var target in run.Targets.Where(t => t.Status is CommandTargetStatus.Pending or CommandTargetStatus.Running))
        {
            target.Status = CommandTargetStatus.Interrupted;
            target.CompletedAt = UtcNow;
            target.ErrorMessage ??= "Uygulama durdurulduğu için işlem yarıda kaldı; sunucuda tamamlanıp tamamlanmadığı bilinmiyor.";
        }

        run.Status = CommandRunStatus.Interrupted;
        run.CompletedAt = UtcNow;
    }

    private static string FirstLine(string command)
    {
        var index = command.IndexOf('\n');
        return index < 0 ? command : command[..index] + " …";
    }

    private static CommandRunListItemDto ToListItem(CommandRun run) => new(
        run.Id,
        run.Command,
        run.TemplateName,
        run.Status,
        run.TargetCount,
        run.SucceededCount,
        run.FailedCount,
        run.UseSudo,
        run.StartedAt,
        run.CompletedAt,
        run.UserName);
}
