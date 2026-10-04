using System.Globalization;
using System.Text;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Auditing;

/// <summary>
/// Audit kaydının imzalanan biçimi. Alanlar uzunluk önekiyle yazılır (boş ve null ayrışır, ayraç enjeksiyonu olmaz).
/// Biçim değişirse eski kayıtlar doğrulanamaz; yeni sürüm yeni önekle eklenmelidir.
/// </summary>
public static class AuditChainFormat
{
    public const string VersionPrefix = "sm-audit-v1";

    /// <summary>Veritabanı yuvarlamasından etkilenmemek için zaman milisaniyeye indirilir.</summary>
    public static DateTime NormalizeTimestamp(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);

    public static string Canonicalize(string? previousHash, AuditLog log)
    {
        var builder = new StringBuilder(256).Append(VersionPrefix);
        Append(builder, previousHash);
        Append(builder, NormalizeTimestamp(log.CreatedAt).ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
        Append(builder, log.UserId);
        Append(builder, log.UserName);
        Append(builder, log.Action);
        Append(builder, log.EntityType);
        Append(builder, log.EntityId);
        Append(builder, log.TargetName);
        Append(builder, log.Details);
        Append(builder, log.IpAddress);
        Append(builder, log.UserAgent);
        Append(builder, log.IsSuccess ? "1" : "0");
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string? value)
    {
        builder.Append('|');
        if (value is null)
        {
            builder.Append('-');
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }
}
