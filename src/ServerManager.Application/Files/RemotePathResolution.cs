namespace ServerManager.Application.Files;

/// <summary>Sunucuda sembolik bağlantıları çözülmüş yol. Koruma kontrolleri ve komutlar bu yollarla yapılır.</summary>
/// <param name="IsSymbolicLink">Yolun kendisi bir sembolik bağlantı.</param>
/// <param name="Target">Tüm bağlantılar izlenerek ulaşılan gerçek yol (<c>readlink -f</c>); izni değiştirilecek olan budur.</param>
/// <param name="Entry">Üst klasörün gerçek yolu ve ad; son bileşeni izlemeyen işlemlerin (silme, taşıma) etkilediği yol.</param>
public sealed record RemotePathResolution(bool IsSymbolicLink, string Target, string Entry);
