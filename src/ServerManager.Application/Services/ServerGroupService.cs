using FluentValidation;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.ServerGroups;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class ServerGroupService : IServerGroupService
{
    private const string NotFoundMessage = "Sunucu grubu bulunamadı.";

    private readonly IServerGroupRepository _repository;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<ServerGroupFormDto> _validator;
    private readonly TimeProvider _timeProvider;

    public ServerGroupService(
        IServerGroupRepository repository,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<ServerGroupFormDto> validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ServerGroupListItemDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _repository.GetAllWithServersAsync(cancellationToken);
        return groups.Select(g =>
        {
            var costs = g.Servers
                .Where(s => s.MonthlyCost.HasValue)
                .GroupBy(s => s.CostCurrency ?? CostCurrencies.Default)
                .ToDictionary(c => c.Key, c => c.Sum(s => s.MonthlyCost!.Value));
            return new ServerGroupListItemDto(
                g.Id,
                g.Name,
                g.Description,
                g.Color,
                g.Servers.Count,
                g.Servers.Count(s => s.Status is not (ServerStatus.Offline or ServerStatus.Unknown)),
                costs,
                g.Servers.Select(s => s.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList());
        }).ToList();
    }

    public async Task<IReadOnlyList<ServerGroupOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _repository.GetAllAsync(cancellationToken);
        return groups.Select(g => new ServerGroupOptionDto(g.Id, g.Name, g.Color)).ToList();
    }

    public async Task<ServiceResult<ServerGroupFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await _repository.GetAsync(id, cancellationToken);
        if (group is null)
            return ServiceResult<ServerGroupFormDto>.NotFound(NotFoundMessage);

        var servers = await _repository.GetGroupServersAsync(id, cancellationToken);
        return ServiceResult<ServerGroupFormDto>.Success(new ServerGroupFormDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            Color = group.Color,
            ServerIds = servers.Select(s => s.Id).ToList()
        });
    }

    public async Task<ServiceResult<Guid>> CreateAsync(ServerGroupFormDto dto, CancellationToken cancellationToken = default)
    {
        Normalize(dto);
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        if (await _repository.NameExistsAsync(dto.Name, null, cancellationToken))
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Name), "Bu isimde bir grup zaten var.");

        var group = new ServerGroup
        {
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
            CreatedAt = UtcNow,
            CreatedBy = _currentUser.UserName
        };
        await _repository.AddAsync(group, cancellationToken);

        var assigned = await AssignServersAsync(group.Id, dto.ServerIds, new HashSet<Guid>(), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.ServerGroupCreate, group, assigned.Count == 0 ? null : $"Sunucular: {string.Join(", ", assigned)}", cancellationToken);
        return ServiceResult<Guid>.Success(group.Id, "Grup oluşturuldu.");
    }

    public async Task<ServiceResult> UpdateAsync(ServerGroupFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var group = await _repository.GetAsync(id, cancellationToken);
        if (group is null)
            return ServiceResult.NotFound(NotFoundMessage);

        Normalize(dto);
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        if (await _repository.NameExistsAsync(dto.Name, id, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), "Bu isimde bir grup zaten var.");

        var changes = new List<string>();
        if (group.Name != dto.Name) changes.Add($"Ad: {group.Name} -> {dto.Name}");
        if (group.Description != dto.Description) changes.Add("Açıklama güncellendi");
        if (group.Color != dto.Color) changes.Add($"Renk: {group.Color} -> {dto.Color}");

        group.Name = dto.Name;
        group.Description = dto.Description;
        group.Color = dto.Color;
        group.UpdatedAt = UtcNow;
        group.UpdatedBy = _currentUser.UserName;

        var current = await _repository.GetGroupServersAsync(id, cancellationToken);
        var removed = current.Where(s => !dto.ServerIds.Contains(s.Id)).ToList();
        foreach (var server in removed)
            server.GroupId = null;

        var added = await AssignServersAsync(id, dto.ServerIds, current.Select(s => s.Id).ToHashSet(), cancellationToken);
        if (added.Count > 0) changes.Add($"Eklenen: {string.Join(", ", added)}");
        if (removed.Count > 0) changes.Add($"Çıkarılan: {string.Join(", ", removed.Select(s => s.Name))}");

        await _repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(AuditActions.ServerGroupUpdate, group, changes.Count == 0 ? "Değişiklik yok" : string.Join(" | ", changes), cancellationToken);
        return ServiceResult.Success("Grup güncellendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await _repository.GetAsync(id, cancellationToken);
        if (group is null)
            return ServiceResult.NotFound(NotFoundMessage);

        var servers = await _repository.GetGroupServersAsync(id, cancellationToken);
        foreach (var server in servers)
            server.GroupId = null;

        group.IsDeleted = true;
        group.DeletedAt = UtcNow;
        group.DeletedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.ServerGroupDelete, group, servers.Count == 0 ? null : $"Gruptan çıkarılan sunucu sayısı: {servers.Count}", cancellationToken);
        return ServiceResult.Success("Grup silindi. Sunucular silinmedi; yalnızca gruptan çıkarıldı.");
    }

    private async Task<List<string>> AssignServersAsync(Guid groupId, IReadOnlyCollection<Guid> serverIds, IReadOnlySet<Guid> alreadyAssigned, CancellationToken cancellationToken)
    {
        var toAssign = serverIds.Where(sid => !alreadyAssigned.Contains(sid)).Distinct().ToList();
        if (toAssign.Count == 0)
            return [];

        var servers = await _repository.GetServersAsync(toAssign, cancellationToken);
        foreach (var server in servers)
            server.GroupId = groupId;

        return servers.Select(s => s.Name).ToList();
    }

    private static void Normalize(ServerGroupFormDto dto)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.Description = TextHelper.NullIfEmpty(dto.Description?.Trim());
        dto.Color = string.IsNullOrWhiteSpace(dto.Color) ? ServerGroupColors.Default : dto.Color.Trim();
        dto.ServerIds = (dto.ServerIds ?? []).Distinct().ToList();
    }

    private Task AuditAsync(string action, ServerGroup group, string? details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.ServerGroup, group.Id.ToString(), group.Name, TextHelper.Truncate(details, 2000)), cancellationToken);
}
