using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Templates;

public sealed record ServerTemplateOptionDto(Guid Id, string Name, ServerTemplateKind Kind, string Content, bool RequiresSudo);
