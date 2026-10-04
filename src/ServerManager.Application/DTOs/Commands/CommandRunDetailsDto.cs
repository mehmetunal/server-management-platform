namespace ServerManager.Application.DTOs.Commands;

public sealed record CommandRunDetailsDto(CommandRunListItemDto Run, int TimeoutSeconds, IReadOnlyList<CommandRunTargetDto> Targets);
