using ServerManager.Application.DTOs.Security;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class ServerSecurityViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public required SecurityServerReportDto Report { get; init; }
}
