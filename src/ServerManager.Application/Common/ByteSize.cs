using System.Globalization;

namespace ServerManager.Application.Common;

/// <summary>Audit ve mesaj metinleri için okunur boyut (1024 tabanlı, Türkçe ondalık).</summary>
public static class ByteSize
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (Math.Abs(value) >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(unit == 0 ? "0" : "0.#", Turkish) + " " + Units[unit];
    }
}
