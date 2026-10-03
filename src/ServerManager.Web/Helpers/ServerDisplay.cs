using Microsoft.AspNetCore.Mvc.Rendering;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class ServerDisplay
{
    public static string StatusText(ServerStatus status) => status switch
    {
        ServerStatus.Healthy => "Healthy",
        ServerStatus.Warning => "Warning",
        ServerStatus.Critical => "Critical",
        ServerStatus.Offline => "Offline",
        ServerStatus.Maintenance => "Bakımda",
        _ => "Bilinmiyor"
    };

    public static string StatusBadgeClass(ServerStatus status) => status switch
    {
        ServerStatus.Healthy => "badge-success",
        ServerStatus.Warning => "badge-warning",
        ServerStatus.Critical => "badge-danger",
        ServerStatus.Offline => "badge-dark",
        ServerStatus.Maintenance => "badge-info",
        _ => "badge-neutral"
    };

    public static string EnvironmentText(ServerEnvironment environment) => environment switch
    {
        ServerEnvironment.Production => "Production",
        ServerEnvironment.Staging => "Staging",
        ServerEnvironment.Development => "Development",
        ServerEnvironment.Testing => "Test",
        _ => "Diğer"
    };

    public static string EnvironmentBadgeClass(ServerEnvironment environment) => environment switch
    {
        ServerEnvironment.Production => "badge-danger",
        ServerEnvironment.Staging => "badge-warning",
        ServerEnvironment.Development => "badge-info",
        ServerEnvironment.Testing => "badge-brand",
        _ => "badge-neutral"
    };

    public static string AuthenticationTypeText(AuthenticationType type) => type switch
    {
        AuthenticationType.Password => "SSH Parola",
        AuthenticationType.PrivateKey => "SSH Private Key",
        AuthenticationType.PrivateKeyWithPassphrase => "SSH Key + Passphrase",
        _ => type.ToString()
    };

    public static IEnumerable<SelectListItem> StatusOptions(ServerStatus? selected) =>
        Enum.GetValues<ServerStatus>().Select(s => new SelectListItem(StatusText(s), ((int)s).ToString(), s == selected));

    public static IEnumerable<SelectListItem> EnvironmentOptions(ServerEnvironment? selected) =>
        Enum.GetValues<ServerEnvironment>().Select(e => new SelectListItem(EnvironmentText(e), ((int)e).ToString(), e == selected));

    public static IEnumerable<SelectListItem> AuthenticationTypeOptions(AuthenticationType selected) =>
        Enum.GetValues<AuthenticationType>().Select(a => new SelectListItem(AuthenticationTypeText(a), ((int)a).ToString(), a == selected));
}
