using System.Globalization;
using System.Text;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;

namespace ServerManager.Application.Auditing;

public static class AuditCsv
{
    private static readonly string[] Header =
    [
        "Id", "Zaman", "Kullanıcı", "Kullanıcı Id", "İşlem kodu", "İşlem", "Hedef türü", "Hedef Id", "Hedef",
        "Sonuç", "IP", "Tarayıcı", "Detay", "Zincir imzası"
    ];

    /// <summary>Excel uyumlu (UTF-8 BOM, noktalı virgül) CSV. Formül ile başlayan hücreler metne çevrilir.</summary>
    public static byte[] Write(IEnumerable<AuditLogDto> logs, Func<string, string> actionName)
    {
        var builder = new StringBuilder();
        AppendRow(builder, Header);
        foreach (var log in logs)
        {
            AppendRow(builder,
            [
                log.Id.ToString(CultureInfo.InvariantCulture),
                AppTimeZone.ToLocal(log.CreatedAt).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                log.UserName,
                log.UserId,
                log.Action,
                actionName(log.Action),
                log.EntityType,
                log.EntityId,
                log.TargetName,
                log.IsSuccess ? "Başarılı" : "Başarısız",
                log.IpAddress,
                log.UserAgent,
                log.Details,
                log.ChainHash
            ]);
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        return [.. preamble, .. body];
    }

    internal static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        return value.IndexOfAny([';', '"', '\n', '\r', ',']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string?> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
                builder.Append(';');
            builder.Append(Escape(values[i]));
        }

        builder.Append("\r\n");
    }
}
