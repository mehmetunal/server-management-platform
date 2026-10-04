using ServerManager.Domain.Enums;

namespace ServerManager.Application.Commands;

public static class ServerTemplateKinds
{
    public static string DisplayName(ServerTemplateKind kind) => kind switch
    {
        ServerTemplateKind.Script => "Kabuk betiği",
        ServerTemplateKind.CloudInit => "cloud-init",
        _ => kind.ToString()
    };
}
