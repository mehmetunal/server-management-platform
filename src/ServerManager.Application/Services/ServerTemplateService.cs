using FluentValidation;
using ServerManager.Application.Auditing;
using ServerManager.Application.Commands;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Templates;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class ServerTemplateService : IServerTemplateService
{
    private const string NotFoundMessage = "Şablon bulunamadı.";
    private const string DuplicateNameMessage = "Bu isimde bir şablon zaten var.";

    private readonly IServerTemplateRepository _repository;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<ServerTemplateFormDto> _validator;
    private readonly TimeProvider _timeProvider;

    public ServerTemplateService(
        IServerTemplateRepository repository,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<ServerTemplateFormDto> validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ServerTemplateListItemDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _repository.GetAllAsync(cancellationToken);
        return templates.Select(t => new ServerTemplateListItemDto(
            t.Id,
            t.Name,
            t.Description,
            t.Kind,
            t.RequiresSudo,
            CountLines(t.Content),
            t.CreatedAt,
            t.UpdatedAt,
            t.UpdatedBy ?? t.CreatedBy)).ToList();
    }

    public async Task<IReadOnlyList<ServerTemplateOptionDto>> GetOptionsAsync(ServerTemplateKind? kind = null, CancellationToken cancellationToken = default)
    {
        var templates = await _repository.GetAllAsync(cancellationToken);
        return templates
            .Where(t => kind is null || t.Kind == kind)
            .Select(t => new ServerTemplateOptionDto(t.Id, t.Name, t.Kind, t.Content, t.RequiresSudo))
            .ToList();
    }

    public async Task<ServiceResult<ServerTemplateFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetAsync(id, cancellationToken);
        if (template is null)
            return ServiceResult<ServerTemplateFormDto>.NotFound(NotFoundMessage);

        return ServiceResult<ServerTemplateFormDto>.Success(new ServerTemplateFormDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Kind = template.Kind,
            Content = template.Content,
            RequiresSudo = template.RequiresSudo
        });
    }

    public async Task<ServiceResult<Guid>> CreateAsync(ServerTemplateFormDto dto, CancellationToken cancellationToken = default)
    {
        Normalize(dto);
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        if (await _repository.NameExistsAsync(dto.Name, null, cancellationToken))
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Name), DuplicateNameMessage);

        var template = new ServerTemplate
        {
            Name = dto.Name,
            Description = dto.Description,
            Kind = dto.Kind,
            Content = dto.Content,
            RequiresSudo = dto.RequiresSudo,
            CreatedAt = UtcNow,
            CreatedBy = _currentUser.UserName
        };
        await _repository.AddAsync(template, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.TemplateCreate, template, $"Tür: {ServerTemplateKinds.DisplayName(template.Kind)}", cancellationToken);
        return ServiceResult<Guid>.Success(template.Id, "Şablon oluşturuldu.");
    }

    public async Task<ServiceResult> UpdateAsync(ServerTemplateFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var template = await _repository.GetAsync(id, cancellationToken);
        if (template is null)
            return ServiceResult.NotFound(NotFoundMessage);

        Normalize(dto);
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        if (await _repository.NameExistsAsync(dto.Name, id, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), DuplicateNameMessage);

        var changes = new List<string>();
        if (template.Name != dto.Name) changes.Add($"Ad: {template.Name} -> {dto.Name}");
        if (template.Description != dto.Description) changes.Add("Açıklama güncellendi");
        if (template.Kind != dto.Kind) changes.Add($"Tür: {ServerTemplateKinds.DisplayName(template.Kind)} -> {ServerTemplateKinds.DisplayName(dto.Kind)}");
        if (template.Content != dto.Content) changes.Add("İçerik güncellendi");
        if (template.RequiresSudo != dto.RequiresSudo) changes.Add($"Sudo: {(dto.RequiresSudo ? "açık" : "kapalı")}");

        template.Name = dto.Name;
        template.Description = dto.Description;
        template.Kind = dto.Kind;
        template.Content = dto.Content;
        template.RequiresSudo = dto.RequiresSudo;
        template.UpdatedAt = UtcNow;
        template.UpdatedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.TemplateUpdate, template, changes.Count == 0 ? "Değişiklik yok" : string.Join(" | ", changes), cancellationToken);
        return ServiceResult.Success("Şablon güncellendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetAsync(id, cancellationToken);
        if (template is null)
            return ServiceResult.NotFound(NotFoundMessage);

        template.IsDeleted = true;
        template.DeletedAt = UtcNow;
        template.DeletedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.TemplateDelete, template, null, cancellationToken);
        return ServiceResult.Success("Şablon silindi. Geçmiş komut kayıtları korunur.");
    }

    private static int CountLines(string content) =>
        string.IsNullOrEmpty(content) ? 0 : content.Count(c => c == '\n') + (content.EndsWith('\n') ? 0 : 1);

    private static void Normalize(ServerTemplateFormDto dto)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.Description = TextHelper.NullIfEmpty(dto.Description?.Trim());
        dto.Content = (dto.Content ?? string.Empty).Replace("\r\n", "\n");
    }

    private Task AuditAsync(string action, ServerTemplate template, string? details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.ServerTemplate, template.Id.ToString(), template.Name, TextHelper.Truncate(details, 2000)), cancellationToken);
}
