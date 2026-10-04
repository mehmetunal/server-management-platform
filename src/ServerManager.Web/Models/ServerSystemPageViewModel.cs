using ServerManager.Application.ServerSystem;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class ServerSystemPageViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public required string PanelUrl { get; init; }

    public required string LoadingText { get; init; }

    /// <summary>Yalnızca Logs sekmesinde: formun başlangıç değerleri.</summary>
    public LogRequest? LogRequest { get; init; }
}
