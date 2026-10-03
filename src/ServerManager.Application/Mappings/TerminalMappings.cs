using ServerManager.Application.DTOs.Terminal;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Mappings;

public static class TerminalMappings
{
    public static TerminalSessionDto ToDto(this TerminalSessionLog session) => new()
    {
        Id = session.Id,
        ServerId = session.ServerId,
        UserName = session.UserName,
        IpAddress = session.IpAddress,
        Kind = session.Kind,
        Container = session.Container,
        StartedAt = session.StartedAt,
        EndedAt = session.EndedAt,
        CloseReason = session.CloseReason,
        CommandCount = session.CommandCount
    };

    public static TerminalCommandDto ToDto(this TerminalCommandLog command) => new()
    {
        ExecutedAt = command.ExecutedAt,
        CommandText = command.CommandText,
        IsApproximate = command.IsApproximate,
        Status = command.Status,
        MatchedRule = command.MatchedRule
    };
}
