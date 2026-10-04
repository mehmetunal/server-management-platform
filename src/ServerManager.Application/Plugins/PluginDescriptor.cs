namespace ServerManager.Application.Plugins;

/// <summary>Eklenti klasöründeki <c>plugin.json</c> dosyasının içeriği.</summary>
public sealed class PluginDescriptor
{
    public const string FileName = "plugin.json";

    public string SystemName { get; init; } = string.Empty;

    public string FriendlyName { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string? Author { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public string AssemblyFileName { get; init; } = string.Empty;

    /// <summary>Content klasöründeki logo dosyası (ör. logo.svg). Yol içeremez.</summary>
    public string? Logo { get; init; }
}
