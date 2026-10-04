namespace ServerManager.Web.Models;

/// <param name="RootPassword">Yalnızca bu yanıtta döner; sunucuda saklanmaz.</param>
public sealed record CloudProvisionResponse(string RedirectUrl, string? RootPassword);
