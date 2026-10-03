using System.Text;

namespace ServerManager.Application.Files;

/// <summary>Editörde açılacak dosyaları UTF-8 metin olarak çözer; kaydederken BOM ve satır sonu korunur.</summary>
public static class TextFileCodec
{
    public const string Lf = "LF";
    public const string Crlf = "CRLF";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <returns>İkili veya UTF-8 olmayan içerikte null.</returns>
    public static TextFileContent? Decode(ReadOnlySpan<byte> bytes)
    {
        var hasBom = bytes.StartsWith(Utf8Bom);
        if (hasBom)
            bytes = bytes[Utf8Bom.Length..];

        if (bytes.Contains((byte)0))
            return null;

        string content;
        try
        {
            content = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        return new TextFileContent(content, hasBom, content.Contains("\r\n", StringComparison.Ordinal) ? Crlf : Lf);
    }

    public static byte[] Encode(string content, bool hasBom, string? lineEnding)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (lineEnding == Crlf)
            normalized = normalized.Replace("\n", "\r\n", StringComparison.Ordinal);

        var body = StrictUtf8.GetBytes(normalized);
        return hasBom ? [.. Utf8Bom, .. body] : body;
    }
}
