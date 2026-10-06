using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ServerManager.Web.Models;

namespace ServerManager.Web.Services;

/// <summary>Kılavuzu oluşturan bir Markdown dosyası. <see cref="IdPrefix"/> başlık kimliklerinin çakışmaması içindir.</summary>
public sealed record GuideSource(string FileName, string ChapterId, string IdPrefix, string Markdown);

/// <summary>
/// docs/ altındaki Markdown dosyalarını güvenli ayarlarla HTML'e çevirir ve h2 başlıklarına göre bölümlere ayırır.
/// Ham HTML kapalıdır (metin olarak kaçışlanır); yalnızca http, https, mailto ve belge içi bağlantılar kalır.
/// Dosyalar arası bağlantılar (ör. <c>son-kullanici.md#giris</c>) sayfa içi çapaya çevrilir; uygulamada
/// bulunmayan belgelere giden bağlantılar düz metne dönüşür. Göreli resim adresleri <see cref="ImagePathToken"/> ile
/// işaretlenir; istekte uygulamanın yoluna göre <c>/Guide/Image/</c> adresine çevrilir.
/// </summary>
public static class UserGuideRenderer
{
    public const string ImagePathToken = "/__guide-image__/";

    private static readonly string[] AllowedSchemes = ["http", "https", "mailto"];

    // UseGenericAttributes en sona eklenmelidir; başlıklardaki {#id} kimlikleri otomatik kimliğin önüne geçer.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoIdentifiers(AutoIdentifierOptions.Default)
        .UseGenericAttributes()
        .Build();

    public static GuideDocument Render(IReadOnlyList<GuideSource> sources)
    {
        var known = sources.ToDictionary(source => source.FileName, StringComparer.OrdinalIgnoreCase);
        return new GuideDocument(sources.Select(source => RenderChapter(source, known)).ToList());
    }

    /// <summary>Tek bir Markdown metnini (bölümlere ayırmadan) HTML'e çevirir.</summary>
    public static string RenderHtml(string markdown) => Markdown.ToHtml(markdown, Pipeline);

    private static GuideChapter RenderChapter(GuideSource source, IReadOnlyDictionary<string, GuideSource> known)
    {
        var document = Markdown.Parse(source.Markdown, Pipeline);
        var chapterTitle = source.ChapterId;
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var attributes = heading.GetAttributes();
            if (heading.Level == 1)
            {
                attributes.Id = source.ChapterId;
                chapterTitle = PlainText(heading.Inline);
                continue;
            }

            var id = source.IdPrefix + (string.IsNullOrEmpty(attributes.Id) ? "bolum" : attributes.Id);
            var unique = id;
            for (var index = 2; !used.Add(unique); index++)
                unique = $"{id}-{index.ToString(CultureInfo.InvariantCulture)}";
            attributes.Id = unique;
        }

        foreach (var link in document.Descendants<LinkInline>().ToList())
            RewriteLink(link, source, known);

        var intro = new List<Block>();
        var sections = new List<(HeadingBlock Heading, List<Block> Blocks)>();
        foreach (var block in document)
        {
            if (block is HeadingBlock { Level: 2 } h2)
                sections.Add((h2, [block]));
            else if (sections.Count == 0)
                intro.Add(block);
            else
                sections[^1].Blocks.Add(block);
        }

        return new GuideChapter(
            source.ChapterId,
            chapterTitle,
            RenderBlocks(intro),
            sections.Select(section => new GuideSection(
                section.Heading.GetAttributes().Id!,
                PlainText(section.Heading.Inline),
                RenderBlocks(section.Blocks),
                section.Blocks.OfType<HeadingBlock>()
                    .Where(heading => heading.Level == 3)
                    .Select(heading => new GuideHeading(heading.GetAttributes().Id!, PlainText(heading.Inline), heading.Level))
                    .ToList())).ToList());
    }

    private static void RewriteLink(LinkInline link, GuideSource source, IReadOnlyDictionary<string, GuideSource> known)
    {
        var url = link.Url?.Trim() ?? string.Empty;

        if (link.IsImage)
        {
            if (IsExternal(url, out var allowed))
            {
                if (!allowed) link.Url = string.Empty;
                return;
            }

            var fileName = Path.GetFileName(url.Split('?', '#')[0]);
            link.Url = string.IsNullOrEmpty(fileName) ? string.Empty : ImagePathToken + Uri.EscapeDataString(fileName);
            return;
        }

        if (url.StartsWith('#'))
        {
            link.Url = "#" + source.IdPrefix + url[1..];
            return;
        }

        if (IsExternal(url, out var safe))
        {
            if (!safe)
            {
                Unwrap(link);
                return;
            }

            var attributes = link.GetAttributes();
            attributes.AddPropertyIfNotExist("target", "_blank");
            attributes.AddPropertyIfNotExist("rel", "noopener noreferrer");
            return;
        }

        var hashIndex = url.IndexOf('#', StringComparison.Ordinal);
        var path = hashIndex < 0 ? url : url[..hashIndex];
        var fragment = hashIndex < 0 ? string.Empty : url[(hashIndex + 1)..];
        if (known.TryGetValue(Path.GetFileName(path), out var target))
        {
            link.Url = "#" + (fragment.Length == 0 ? target.ChapterId : target.IdPrefix + fragment);
            return;
        }

        // Uygulamada olmayan belge (ör. teknik doküman) veya göreli dosya: bağlantı yerine metin kalır.
        Unwrap(link);
    }

    private static bool IsExternal(string url, out bool allowed)
    {
        allowed = false;
        if (url.StartsWith("//", StringComparison.Ordinal)) return true;

        var colon = url.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0) return false;
        var stop = url.IndexOfAny(['/', '?', '#']);
        if (stop >= 0 && stop < colon) return false;

        allowed = AllowedSchemes.Contains(url[..colon], StringComparer.OrdinalIgnoreCase);
        return true;
    }

    private static void Unwrap(LinkInline link) => link.ReplaceBy(new LiteralInline(PlainText(link)), copyChildren: false);

    private static string RenderBlocks(IEnumerable<Block> blocks)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        foreach (var block in blocks)
            renderer.Render(block);
        writer.Flush();
        return writer.ToString();
    }

    private static string PlainText(ContainerInline? container)
    {
        if (container is null) return string.Empty;
        var builder = new StringBuilder();
        Append(builder, container);
        return builder.ToString().Trim();
    }

    private static void Append(StringBuilder builder, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                builder.Append(code.Content);
                break;
            case LineBreakInline:
                builder.Append(' ');
                break;
            case ContainerInline container:
                foreach (var child in container)
                    Append(builder, child);
                break;
        }
    }
}
