namespace ServerManager.Application.DTOs.ServerGroups;

public static class ServerGroupColors
{
    public const string Default = "slate";

    public static readonly IReadOnlyList<string> All = ["slate", "blue", "green", "amber", "red", "purple", "cyan", "pink"];

    public static bool IsValid(string? color) => color is not null && All.Contains(color);
}
