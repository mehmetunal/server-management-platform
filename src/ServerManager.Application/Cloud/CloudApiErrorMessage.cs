using System.Net;
using System.Text.Json;

namespace ServerManager.Application.Cloud;

/// <summary>Bulut sağlayıcı API'lerinin hata yanıtlarını kullanıcıya gösterilecek Türkçe mesaja çevirir.</summary>
public static class CloudApiErrorMessage
{
    /// <param name="status">Yanıtın HTTP durum kodu.</param>
    /// <param name="body">Yanıt gövdesi; JSON değilse ayrıntı okunmaz.</param>
    /// <param name="providerName">Mesajlarda geçen sağlayıcı adı (ör. "Hetzner").</param>
    /// <param name="forbiddenMessage">403 yanıtında gösterilecek, sağlayıcıya özgü yetki mesajı.</param>
    /// <param name="readDetail">Kök JSON öğesinden sağlayıcının hata ayrıntısını okur; yoksa null döner.</param>
    public static string Create(
        HttpStatusCode status,
        string body,
        string providerName,
        string forbiddenMessage,
        Func<JsonElement, string?> readDetail)
    {
        string? detail = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            detail = readDetail(document.RootElement);
        }
        catch (JsonException)
        {
            // Gövde JSON değilse yalnızca durum koduna göre genel mesaj verilir.
        }

        return status switch
        {
            HttpStatusCode.Unauthorized => "API anahtarı geçersiz veya iptal edilmiş.",
            HttpStatusCode.Forbidden => forbiddenMessage,
            HttpStatusCode.TooManyRequests => $"{providerName} istek sınırına ulaşıldı; biraz sonra tekrar deneyin.",
            _ => detail is null ? $"{providerName} isteği başarısız oldu (HTTP {(int)status})." : $"{providerName}: {detail}"
        };
    }
}
