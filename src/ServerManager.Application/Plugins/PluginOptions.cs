namespace ServerManager.Application.Plugins;

public sealed class PluginOptions
{
    public const string SectionName = "Plugins";

    /// <summary>Eklenti klasörlerinin bulunduğu dizin; göreli yol uygulama kök dizinine göredir.</summary>
    public string Directory { get; set; } = "Plugins";

    /// <summary>Daha önce hiç kurulmamışsa açılışta otomatik kurulan eklentiler (SystemName).</summary>
    public List<string> InstallOnStartup { get; set; } = [];
}
