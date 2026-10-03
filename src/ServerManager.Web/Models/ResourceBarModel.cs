namespace ServerManager.Web.Models;

public sealed record ResourceBarModel(string Label, string Key, double? Percent, double Warning, double Critical, bool Compact = false);
