namespace ServerManager.Application.ManagedServices;

/// <summary>
/// Servisler modülüne şablon ekleyen eklenti sözleşmesi. Eklentinin <c>IPluginStartup.ConfigureServices</c> metodunda
/// <c>services.AddSingleton&lt;IServiceTemplateProvider, MyTemplates&gt;()</c> ile kaydedilir. Şablonlar uygulama açıldıktan
/// sonra ilk kullanımda bir kez okunur ve doğrulanır; eklenti devre dışıyken listede görünmez.
/// </summary>
/// <remarks>
/// Kurallar: şablon anahtarı <c>&lt;systemname küçük harf&gt;.&lt;ad&gt;</c> biçiminde olmalı (ör. <c>services.extra.keycloak</c>);
/// geçersiz veya çakışan şablonlar atlanır ve Eklentiler sayfasında uyarı olarak gösterilir. Kod gerektirmeyen şablonlar için
/// eklenti klasörüne <c>templates/*.json</c> dosyaları da eklenebilir (bkz. docs/plugin-gelistirme.md).
/// </remarks>
public interface IServiceTemplateProvider
{
    /// <summary>Eklentinin sunduğu şablonlar.</summary>
    IReadOnlyList<ServiceTemplate> GetTemplates();

    /// <summary>Eklentinin tanımladığı ek şablon grupları (isteğe bağlı).</summary>
    IReadOnlyList<ServiceTemplateCategory> GetCategories() => [];
}
