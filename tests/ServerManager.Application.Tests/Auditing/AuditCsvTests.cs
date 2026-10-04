using System.Text;
using ServerManager.Application.Auditing;
using ServerManager.Application.DTOs.AuditLogs;

namespace ServerManager.Application.Tests.Auditing;

public class AuditCsvTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1", "'+1")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("satır\nikinci", "\"satır\nikinci\"")]
    [InlineData("normal", "normal")]
    [InlineData(null, "")]
    public void Escape_neutralizes_formulas_and_quotes_separators(string? value, string expected) =>
        Assert.Equal(expected, AuditCsv.Escape(value));

    [Fact]
    public void Write_produces_bom_header_and_rows()
    {
        var logs = new[]
        {
            new AuditLogDto
            {
                Id = 7,
                UserName = "admin",
                Action = AuditActions.ServerDelete,
                TargetName = "web;1",
                IsSuccess = false,
                CreatedAt = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc)
            }
        };

        var bytes = AuditCsv.Write(logs, action => action == AuditActions.ServerDelete ? "Sunucu silme" : action);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Id;Zaman;Kullanıcı;", lines[0]);
        Assert.StartsWith("7;", lines[1]);
        Assert.Contains("Sunucu silme", lines[1]);
        Assert.Contains("\"web;1\"", lines[1]);
        Assert.Contains("Başarısız", lines[1]);
    }
}
