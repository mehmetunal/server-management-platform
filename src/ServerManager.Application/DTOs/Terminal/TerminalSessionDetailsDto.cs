namespace ServerManager.Application.DTOs.Terminal;

public sealed class TerminalSessionDetailsDto
{
    public required TerminalSessionDto Session { get; init; }

    public IReadOnlyList<TerminalCommandDto> Commands { get; init; } = [];
}
