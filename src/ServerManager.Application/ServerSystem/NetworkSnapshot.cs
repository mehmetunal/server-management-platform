using ServerManager.Application.Security;

namespace ServerManager.Application.ServerSystem;

public sealed record NetworkSnapshot(
    string? Hostname,
    IReadOnlyList<NetworkInterfaceEntry> Interfaces,
    IReadOnlyList<string> Routes,
    IReadOnlyList<string> DnsServers,
    bool PortsAvailable,
    IReadOnlyList<ListeningPort> Ports);
