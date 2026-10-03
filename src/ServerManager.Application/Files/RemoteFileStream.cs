namespace ServerManager.Application.Files;

/// <summary>İndirme akışı. Akış kapatıldığında arkasındaki SSH bağlantısı da kapanır.</summary>
public sealed class RemoteFileStream
{
    public required Stream Content { get; init; }

    public required string FileName { get; init; }

    public long? Length { get; init; }
}
