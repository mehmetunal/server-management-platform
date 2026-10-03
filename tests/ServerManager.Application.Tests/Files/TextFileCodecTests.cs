using System.Text;
using ServerManager.Application.Files;

namespace ServerManager.Application.Tests.Files;

public class TextFileCodecTests
{
    [Fact]
    public void Decodes_utf8_without_bom_as_lf()
    {
        var content = TextFileCodec.Decode(Encoding.UTF8.GetBytes("ad: Şükrü\nşehir: İzmir\n"));

        Assert.NotNull(content);
        Assert.Equal("ad: Şükrü\nşehir: İzmir\n", content.Content);
        Assert.False(content.HasBom);
        Assert.Equal(TextFileCodec.Lf, content.LineEnding);
    }

    [Fact]
    public void Detects_bom_and_crlf()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("a\r\nb\r\n")];

        var content = TextFileCodec.Decode(bytes);

        Assert.NotNull(content);
        Assert.Equal("a\r\nb\r\n", content.Content);
        Assert.True(content.HasBom);
        Assert.Equal(TextFileCodec.Crlf, content.LineEnding);
    }

    [Fact]
    public void Rejects_binary_and_invalid_utf8()
    {
        Assert.Null(TextFileCodec.Decode([0x7F, 0x45, 0x4C, 0x46, 0x00, 0x01]));
        Assert.Null(TextFileCodec.Decode([0x61, 0xC3, 0x28]));
    }

    [Fact]
    public void Encode_preserves_bom_and_converts_line_endings()
    {
        var bytes = TextFileCodec.Encode("a\nb\r\nc", hasBom: true, TextFileCodec.Crlf);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("a\r\nb\r\nc", Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public void Encode_normalizes_to_lf_without_bom()
    {
        var bytes = TextFileCodec.Encode("a\r\nb\n", hasBom: false, TextFileCodec.Lf);

        Assert.Equal("a\nb\n"u8.ToArray(), bytes);
    }

    [Fact]
    public void Encode_rejects_lone_surrogates()
    {
        Assert.Throws<EncoderFallbackException>(() => TextFileCodec.Encode("a\uD800b", hasBom: false, TextFileCodec.Lf));
    }
}
