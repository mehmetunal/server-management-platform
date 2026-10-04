namespace ServerManager.Application.ServerSystem;

public sealed record NetworkInterfaceEntry(
    string Name,
    string? State,
    string? MacAddress,
    int? Mtu,
    IReadOnlyList<string> Addresses,
    long? ReceivedBytes,
    long? TransmittedBytes);
