using System.Text.RegularExpressions;
using ServerManager.Web.Helpers;
using ServerManager.Web.Services;

namespace ServerManager.Web.Tests;

/// <summary>Kılavuzun Markdown işlemesi; veritabanı gerektirmez.</summary>
public sealed partial class UserGuideTests
{
    private static GuideSource Source(string markdown, string fileName = "a.md", string chapterId = "a", string prefix = "") =>
        new(fileName, chapterId, prefix, markdown);

    [Fact]
    public void Markdown_renders_headings_tables_and_task_lists()
    {
        const string markdown = """
            # Başlık

            Giriş metni **kalın**.

            ## Bölüm bir {#bir}

            | A | B |
            | --- | --- |
            | 1 | 2 |

            - [x] bitti
            - [ ] sırada

            ### Alt başlık

            Metin.
            """;

        var document = UserGuideRenderer.Render([Source(markdown)]);

        var chapter = Assert.Single(document.Chapters);
        Assert.Equal("Başlık", chapter.Title);
        Assert.Contains("<strong>kalın</strong>", chapter.IntroHtml, StringComparison.Ordinal);
        var section = Assert.Single(chapter.Sections);
        Assert.Equal("bir", section.Id);
        Assert.Equal("Bölüm bir", section.Title);
        Assert.Contains("<h2 id=\"bir\">", section.Html, StringComparison.Ordinal);
        Assert.Contains("<table>", section.Html, StringComparison.Ordinal);
        Assert.Contains("type=\"checkbox\"", section.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{#bir}", section.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Raw_html_is_escaped()
    {
        const string markdown = """
            ## Bölüm {#b}

            <script>alert('x')</script>

            Satır içi <img src=x onerror=alert(1)> metin.
            """;

        var html = UserGuideRenderer.Render([Source(markdown)]).Chapters[0].Sections[0].Html;

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&lt;img", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Unsafe_link_schemes_become_plain_text()
    {
        const string markdown = """
            ## Bölüm {#b}

            [tıkla](javascript:alert(1)) ve [site](https://example.com) ve [veri](data:text/html,x)
            """;

        var html = UserGuideRenderer.Render([Source(markdown)]).Chapters[0].Sections[0].Html;

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:text", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tıkla", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.com\"", html, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Toc_lists_h2_sections_with_their_h3_headings()
    {
        const string markdown = """
            # Kitap

            ## Birinci {#birinci}

            ### Alt bir {#alt-bir}

            ### Alt iki

            ## İkinci

            #### Gösterilmez
            """;

        var chapter = UserGuideRenderer.Render([Source(markdown)]).Chapters[0];

        Assert.Equal(["birinci", chapter.Sections[1].Id], chapter.Sections.Select(s => s.Id));
        Assert.False(string.IsNullOrWhiteSpace(chapter.Sections[1].Id));
        Assert.Equal("İkinci", chapter.Sections[1].Title);
        Assert.Equal(2, chapter.Sections[0].Headings.Count);
        Assert.Equal("alt-bir", chapter.Sections[0].Headings[0].Id);
        Assert.Equal("Alt iki", chapter.Sections[0].Headings[1].Title);
        Assert.Empty(chapter.Sections[1].Headings);
    }

    [Fact]
    public void Cross_document_links_and_ids_are_rewritten_per_chapter()
    {
        var main = Source("# Ana\n\n## Giriş {#giris}\n\nBkz. [son kullanıcı](b.md), [onun girişi](b.md#giris) ve [teknik](teknik.md).", "a.md", "ana");
        var second = Source("# İkinci\n\n## Giriş {#giris}\n\n[yukarı](#giris)", "b.md", "ikinci", "b-");

        var document = UserGuideRenderer.Render([main, second]);

        var mainHtml = document.Chapters[0].Sections[0].Html;
        Assert.Contains("href=\"#ikinci\"", mainHtml, StringComparison.Ordinal);
        Assert.Contains("href=\"#b-giris\"", mainHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("teknik.md", mainHtml, StringComparison.Ordinal);
        Assert.Contains("teknik", mainHtml, StringComparison.Ordinal);
        Assert.Equal("b-giris", document.Chapters[1].Sections[0].Id);
        Assert.Contains("href=\"#b-giris\"", document.Chapters[1].Sections[0].Html, StringComparison.Ordinal);
        Assert.Contains("giris", document.Anchors);
        Assert.Contains("b-giris", document.Anchors);
        Assert.Contains("ikinci", document.Anchors);
    }

    [Fact]
    public void Relative_images_point_to_the_guide_image_endpoint()
    {
        var html = UserGuideRenderer.Render([Source("## R {#r}\n\n![ekran](images/ekran.png)")]).Chapters[0].Sections[0].Html;

        Assert.Contains($"src=\"{UserGuideRenderer.ImagePathToken}ekran.png\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Embedded_guide_loads_and_contains_every_help_anchor()
    {
        var document = new UserGuide().Document;

        Assert.Equal(UserGuide.Files.Count, document.Chapters.Count);
        Assert.All(document.Chapters, chapter => Assert.NotEmpty(chapter.Sections));
        var missing = GuideAnchors.All.Where(anchor => !document.Anchors.Contains(anchor)).ToList();
        Assert.True(missing.Count == 0, "Kılavuzda olmayan yardım çapaları: " + string.Join(", ", missing));
    }

    [Fact]
    public void Embedded_guide_has_unique_ids()
    {
        var ids = new UserGuide().Document.Chapters
            .SelectMany(chapter => chapter.Sections
                .SelectMany(section => section.Headings.Select(heading => heading.Id).Prepend(section.Id))
                .Prepend(chapter.Id))
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Picker_sections_map_to_existing_anchors()
    {
        var anchors = new UserGuide().Document.Anchors;
        foreach (var key in new[] { "docker", "images", "volumes", "networks", "terminal", "files", "services", "processes", "logs", "metrics", "x" })
            Assert.Contains(GuideAnchors.ForPicker(key), anchors);
    }

    /// <summary>Görünümlerdeki her _HelpLink, GuideAnchors sabitlerinden birini kullanmalıdır (düz metin çapa kırılabilir).</summary>
    [Fact]
    public void Views_use_only_known_help_anchors()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "ServerManager.Web", "Views");
        var known = typeof(GuideAnchors).GetFields().Where(field => field.IsLiteral).Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var usages = Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => HelpLinkUsage().Matches(File.ReadAllText(file)).Select(match => (file, model: match.Groups[1].Value)))
            .ToList();

        Assert.NotEmpty(usages);
        foreach (var (file, model) in usages)
        {
            if (model.StartsWith("GuideAnchors.ForPicker(", StringComparison.Ordinal)) continue;
            Assert.True(model.StartsWith("GuideAnchors.", StringComparison.Ordinal) && known.Contains(model["GuideAnchors.".Length..]),
                $"{Path.GetFileName(file)}: _HelpLink modeli GuideAnchors sabiti olmalı ({model}).");
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ServerManager.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Depo kökü bulunamadı.");
    }

    [GeneratedRegex("<partial name=\"_HelpLink\" model=\"([^\"]+)\"")]
    private static partial Regex HelpLinkUsage();
}
