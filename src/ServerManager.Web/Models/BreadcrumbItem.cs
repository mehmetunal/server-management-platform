namespace ServerManager.Web.Models;

public sealed record BreadcrumbItem(string Label, string? Url = null, string? Title = null);
