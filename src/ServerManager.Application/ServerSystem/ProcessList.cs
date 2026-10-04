namespace ServerManager.Application.ServerSystem;

/// <param name="HasCpuUsage">BusyBox ps CPU yüzdesi vermez; bu durumda sütun gizlenir.</param>
public sealed record ProcessList(IReadOnlyList<ProcessEntry> Processes, bool HasCpuUsage, int TotalCount);
