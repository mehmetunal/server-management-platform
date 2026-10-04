namespace ServerManager.Application.DTOs.Cloud;

public sealed record CloudSyncSummary(int ProviderServerCount, int LinkedCount, int NewlyLinkedCount, int CostUpdatedCount);
