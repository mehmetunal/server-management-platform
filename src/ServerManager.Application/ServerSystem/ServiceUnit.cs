namespace ServerManager.Application.ServerSystem;

public sealed record ServiceUnit(string Name, string? Description, string ActiveState, string SubState, bool? Enabled)
{
    public bool IsRunning => SubState is "running" or "started" || (ActiveState == "active" && SubState is "running" or "exited" or "listening");

    public bool IsFailed => ActiveState is "failed" || SubState is "failed" or "crashed";
}
