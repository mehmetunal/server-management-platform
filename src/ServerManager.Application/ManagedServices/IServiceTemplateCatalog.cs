namespace ServerManager.Application.ManagedServices;

/// <summary>
/// Servis şablonu kataloğu: yerleşik şablonlar + etkin eklentilerin şablonları (<see cref="IServiceTemplateProvider"/> ve
/// <c>templates/*.json</c>). Eklenti etkinliği çalışma anında değerlendirilir; yeniden başlatma gerekmez.
/// </summary>
public interface IServiceTemplateCatalog
{
    /// <summary>Yeni kurulumda seçilebilecek şablonlar (yerleşik + etkin eklentiler).</summary>
    IReadOnlyList<ServiceTemplate> GetAvailable();

    /// <summary>En az bir kullanılabilir şablonu olan gruplar, sırasıyla.</summary>
    IReadOnlyList<ServiceTemplateCategory> GetCategories();

    /// <summary>Kullanılabilir şablon; bilinmiyorsa veya eklentisi devre dışıysa null.</summary>
    ServiceTemplate? Find(string? key);

    /// <summary>
    /// Kayıtlı servisin şablonunu durumu ile birlikte döner: eklentisi devre dışı olsa da şablon (gösterim için) döner;
    /// eklenti kaldırıldıysa şablon null'dır.
    /// </summary>
    ServiceTemplateResolution Resolve(string? key);

    /// <summary>Şablona bağlı, etkin eklentilerin kancaları.</summary>
    IReadOnlyList<IServiceTemplateHooks> GetHooks(string templateKey);

    /// <summary>Yükleme sırasında atlanan şablonlar ve uyarılar.</summary>
    IReadOnlyList<ServiceTemplateIssue> Issues { get; }

    /// <summary>Bir eklentinin şablon uyarıları (Eklentiler sayfası).</summary>
    IReadOnlyList<string> GetIssues(string pluginSystemName);
}

public enum ServiceTemplateAvailability
{
    Available = 0,

    /// <summary>Şablon bir eklentiye ait ve eklenti devre dışı (veya kurulu değil).</summary>
    PluginDisabled = 1,

    /// <summary>Şablon hiçbir kaynakta yok (eklenti kaldırılmış veya şablon silinmiş).</summary>
    Missing = 2
}

/// <param name="Template">Şablon; <see cref="ServiceTemplateAvailability.Missing"/> ise null.</param>
/// <param name="PluginSystemName">Şablonun (veya anahtar önekinden tahmin edilen) eklentisi.</param>
/// <param name="PluginName">Eklentinin görünen adı.</param>
public sealed record ServiceTemplateResolution(
    ServiceTemplate? Template,
    ServiceTemplateAvailability Availability,
    string? PluginSystemName = null,
    string? PluginName = null)
{
    public bool IsAvailable => Availability == ServiceTemplateAvailability.Available;

    /// <summary>Kullanılamıyorsa kullanıcıya gösterilecek kısa durum (rozet metni).</summary>
    public string? Badge => Availability switch
    {
        ServiceTemplateAvailability.PluginDisabled => "Şablon eklentisi devre dışı",
        ServiceTemplateAvailability.Missing when PluginSystemName is not null => "Şablon eklentisi bulunamadı",
        ServiceTemplateAvailability.Missing => "Şablon bulunamadı",
        _ => null
    };

    /// <summary>Yeniden oluşturma/yükseltme engellendiğinde gösterilecek açıklama.</summary>
    public string? BlockedMessage => Availability switch
    {
        ServiceTemplateAvailability.PluginDisabled =>
            $"Bu servisin şablonunu sağlayan \"{PluginName ?? PluginSystemName}\" eklentisi devre dışı. Loglar, başlat/durdur ve kaldırma kullanılabilir; " +
            "ayar değiştirme ve sürüm yükseltme için eklentiyi Eklentiler sayfasından etkinleştirin.",
        ServiceTemplateAvailability.Missing when PluginSystemName is not null =>
            $"Bu servisin şablonunu sağlayan eklenti ({PluginName ?? PluginSystemName}) yüklü değil veya şablonu artık sunmuyor. " +
            "Loglar, başlat/durdur ve kaldırma kullanılabilir; ayar değiştirme ve sürüm yükseltme için eklentiyi yeniden yükleyin.",
        ServiceTemplateAvailability.Missing => "Servis şablonu bulunamadı.",
        _ => null
    };
}

/// <param name="PluginSystemName">Sorunun ait olduğu eklenti; çekirdek kaynaklıysa null.</param>
/// <param name="Source">Kaynak (sağlayıcı tipi veya JSON dosyası).</param>
public sealed record ServiceTemplateIssue(string? PluginSystemName, string Source, string Message);
