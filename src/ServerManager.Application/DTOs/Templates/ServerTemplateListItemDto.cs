using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Templates;

public sealed record ServerTemplateListItemDto(
    Guid Id,
    string Name,
    string? Description,
    ServerTemplateKind Kind,
    bool RequiresSudo,
    int LineCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? UpdatedBy);
