namespace ServerManager.Application.DTOs.Files;

public sealed record FileSaveResultDto(string Version, long Size, DateTime LastWriteTime);
