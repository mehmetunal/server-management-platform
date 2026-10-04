using QRCoder;

namespace ServerManager.Web.Services;

public static class QrCodeImage
{
    /// <summary>CSP img-src data: adreslerine izin verdiği için QR kodu satır içi PNG olarak döner.</summary>
    public static string ToPngDataUri(string text, int pixelsPerModule = 6)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
