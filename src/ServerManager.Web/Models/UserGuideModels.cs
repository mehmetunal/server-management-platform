namespace ServerManager.Web.Models;

/// <summary>Kılavuzun işlenmiş hâli: bölüm bölüm HTML ve içindekiler.</summary>
public sealed record GuideDocument(IReadOnlyList<GuideChapter> Chapters)
{
    /// <summary>Belgede geçen tüm başlık kimlikleri (bölüm, h2 ve h3).</summary>
    public IReadOnlySet<string> Anchors { get; } = Chapters
        .SelectMany(chapter => chapter.Sections
            .SelectMany(section => section.Headings.Select(heading => heading.Id).Prepend(section.Id))
            .Prepend(chapter.Id))
        .ToHashSet(StringComparer.Ordinal);
}

/// <summary>Bir Markdown dosyası. <see cref="IntroHtml"/> ilk h2'den önceki metindir.</summary>
public sealed record GuideChapter(string Id, string Title, string IntroHtml, IReadOnlyList<GuideSection> Sections);

/// <summary>Bir h2 başlığı ve altındaki içerik; arama bu birimle süzer.</summary>
public sealed record GuideSection(string Id, string Title, string Html, IReadOnlyList<GuideHeading> Headings);

/// <summary>İçindekiler satırı olarak gösterilen alt başlık (h3).</summary>
public sealed record GuideHeading(string Id, string Title, int Level);
