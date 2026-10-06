using System.Reflection;
using System.Text.RegularExpressions;
using ServerManager.Web.Models;

namespace ServerManager.Web.Services;

/// <summary>
/// Derlemeye gömülü kılavuz dosyalarını (docs/kullanim-kilavuzu.md, docs/son-kullanici.md) bir kez işleyip bellekte tutar.
/// Dosyalar derlemenin içinde olduğundan Docker imajında ayrı bir docs klasörü gerekmez.
/// </summary>
public sealed partial class UserGuide
{
    private const string ResourcePrefix = "ServerManager.Web.Guide.";
    private const string ImageResourcePrefix = ResourcePrefix + "images.";

    /// <summary>Kılavuzun bölümleri, sırasıyla. İlk dosyanın başlıkları önek almaz; yardım bağlantıları bunlara gider.</summary>
    public static IReadOnlyList<(string FileName, string ChapterId, string IdPrefix)> Files { get; } =
    [
        ("kullanim-kilavuzu.md", "kullanim-kilavuzu", string.Empty),
        ("son-kullanici.md", "son-kullanici", "sk-")
    ];

    private static readonly Assembly ResourceAssembly = typeof(UserGuide).Assembly;

    private readonly Lazy<GuideDocument> _document = new(
        () => UserGuideRenderer.Render(LoadSources()), LazyThreadSafetyMode.ExecutionAndPublication);

    public GuideDocument Document => _document.Value;

    public static IReadOnlyList<GuideSource> LoadSources() => Files
        .Select(file => new GuideSource(file.FileName, file.ChapterId, file.IdPrefix, ReadResource(ResourcePrefix + file.FileName)))
        .ToList();

    /// <summary>docs/images altındaki gömülü bir resmi açar; ad geçersizse veya yoksa null döner.</summary>
    public static Stream? OpenImage(string fileName) =>
        SafeFileName().IsMatch(fileName) ? ResourceAssembly.GetManifestResourceStream(ImageResourcePrefix + fileName) : null;

    private static string ReadResource(string name)
    {
        using var stream = ResourceAssembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Kılavuz kaynağı derlemede yok: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex SafeFileName();
}
