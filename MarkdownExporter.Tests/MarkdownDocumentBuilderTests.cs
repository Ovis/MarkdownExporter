using AngleSharp.Html.Parser;
using MarkdownExporter.Conversion;

namespace MarkdownExporter.Tests;

/// <summary>Markdownから生成されるHTMLと付随する素材の振る舞いを確認する。</summary>
[TestFixture]
public sealed class MarkdownDocumentBuilderTests
{
    [Test]
    public void Build_LocalImage_CopiesToDocumentAssets()
    {
        using var folder = new TestFolder();
        var input = folder.Write("source/report.md", "![diagram](image.png)");
        folder.Write("source/image.png", [1, 2, 3]);
        var output = folder.PathFor("output");
        var html = Build(input, File.ReadAllText(input), output, new ConversionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("report_assets/content/image.png"));
            Assert.That(File.ReadAllBytes(folder.PathFor("output/report_assets/content/image.png")),
                Is.EqualTo(new byte[] { 1, 2, 3 }));
        });
    }

    [Test]
    public void Build_EmbedImages_WritesDataUriWithoutAssets()
    {
        using var folder = new TestFolder();
        var input = folder.Write("source/report.md", "![diagram](image.png)");
        folder.Write("source/image.png", [1, 2, 3]);
        var output = folder.PathFor("output");

        var html = Build(input, File.ReadAllText(input), output,
            new ConversionOptions { EmbedImages = true });

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("data:image/png;base64,AQID"));
            Assert.That(Directory.Exists(folder.PathFor("output/report_assets")), Is.False);
        });
    }

    [Test]
    public void Build_MissingLocalImage_FailsBeforeCreatingAssets()
    {
        using var folder = new TestFolder();
        var input = folder.Write("source/report.md", "![missing](missing.png)");
        var output = folder.PathFor("output");

        Assert.Throws<FileNotFoundException>(() =>
            Build(input, File.ReadAllText(input), output, new ConversionOptions()));
        Assert.That(Directory.Exists(folder.PathFor("output/report_assets")), Is.False);
    }

    [Test]
    public void Build_RawHtml_RemovesUnsafeMarkupAndKeepsFormatting()
    {
        using var folder = new TestFolder();
        var input = folder.Write("report.md", "<script>alert(1)</script><strong>safe</strong><a href='javascript:alert(1)' onclick='bad()'>link</a>");

        var html = Build(input, File.ReadAllText(input), folder.PathFor("output"), new ConversionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("<script"));
            Assert.That(html, Does.Not.Contain("onclick"));
            Assert.That(html, Does.Not.Contain("javascript:"));
            Assert.That(html, Does.Contain("<strong>safe</strong>"));
        });
    }

    [Test]
    public void Build_IncludedMarkdownLink_RecordsFallbackForFinalization()
    {
        using var folder = new TestFolder();
        var input = folder.Write("source/report.md", "[Next](docs/next.md#section)");
        var output = folder.PathFor("output");
        var target = folder.PathFor("source/docs/next.md");
        var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [target] = folder.PathFor("output/next.html")
        };

        var html = Build(input, File.ReadAllText(input), output, new ConversionOptions(), targets);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("href=\"next.html#section\""));
            Assert.That(html, Does.Contain("data-mdexport-original=\"docs/next.md#section\""));
        });
    }

    [Test]
    public void Build_PageBreak_AddsMarkerAndPrintRule()
    {
        using var folder = new TestFolder();
        var input = folder.Write("report.md", "first\n\n<!-- pagebreak -->\n\nsecond");

        var html = Build(input, File.ReadAllText(input), folder.PathFor("output"), new ConversionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("class=\"pagebreak\""));
            Assert.That(html, Does.Contain("break-before:page"));
        });
    }

    [TestCase("\n", false)]
    [TestCase("\n", true)]
    [TestCase("\r\n", false)]
    [TestCase("\r\n", true)]
    public void Build_PageBreak_PreservesMarkdownAfterMarker(string newline, bool includeBlankLines)
    {
        using var folder = new TestFolder();
        var spacer = includeBlankLines ? newline : "";
        var input = folder.Write("report.md",
            $"first{newline}{spacer}<!-- pagebreak -->{newline}{spacer}## 2. Team plan{newline}{newline}This is **bold**.");

        var html = Build(input, File.ReadAllText(input), folder.PathFor("output"), new ConversionOptions());
        var document = new HtmlParser().ParseDocument(html);

        Assert.Multiple(() =>
        {
            Assert.That(document.QuerySelector(".pagebreak"), Is.Not.Null);
            Assert.That(document.QuerySelector("h2")?.TextContent, Is.EqualTo("2. Team plan"));
            Assert.That(document.QuerySelector("strong")?.TextContent, Is.EqualTo("bold"));
        });
    }

    /// <summary>Frontmatterを先頭の表にし、HTMLタイトルは本文の見出しから決める。</summary>
    [Test]
    public void Build_FrontMatter_DisplaysRowsBeforeBodyWithoutChangingTitle()
    {
        using var folder = new TestFolder();
        var input = folder.Write("report.md", """
            ---
            title: "FrontmatterTitle"
            retrieved_at: "2026-11-05T15:13:13.171000+09:00"
            status: "completed"
            ---

            # 本文の見出し
            """);

        var html = Build(input, File.ReadAllText(input), folder.PathFor("output"), new ConversionOptions());
        var document = new HtmlParser().ParseDocument(html);
        var rows = document.QuerySelectorAll("body > table.frontmatter > tbody > tr");

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(row => row.QuerySelector("th")?.TextContent),
                Is.EqualTo(new[] { "title", "retrieved_at", "status" }));
            Assert.That(rows[0].QuerySelector("td")?.TextContent, Is.EqualTo("\"FrontmatterTitle\""));
            Assert.That(rows[1].QuerySelector("td")?.TextContent,
                Is.EqualTo("\"2026-11-05T15:13:13.171000+09:00\""));
            Assert.That(document.QuerySelector("title")?.TextContent, Is.EqualTo("本文の見出し"));
            Assert.That(document.Body?.Children[0].LocalName, Is.EqualTo("table"));
            Assert.That(document.QuerySelector("h1")?.TextContent, Is.EqualTo("本文の見出し"));
        });
    }

    /// <summary>複数行・配列・入れ子を表示し、値内のHTMLを実行可能にしない。</summary>
    [Test]
    public void Build_FrontMatter_DisplaysNestedValuesSafely()
    {
        using var folder = new TestFolder();
        var input = folder.Write("report.md", """
            ---
            summary: |
              first line
              second line
            tags:
              - alpha
              - beta
            details:
              owner: Alice
              flags:
                - ready
            unsafe: "<script>alert(1)</script>"
            ---

            # Heading
            """);

        var html = Build(input, File.ReadAllText(input), folder.PathFor("output"), new ConversionOptions());
        var document = new HtmlParser().ParseDocument(html);
        var frontMatter = document.QuerySelector("body > table.frontmatter");

        Assert.Multiple(() =>
        {
            Assert.That(frontMatter?.QuerySelector(":scope > tbody > tr:first-child > td")?.TextContent,
                Is.EqualTo("first line\nsecond line\n"));
            Assert.That(frontMatter?.QuerySelectorAll(":scope > tbody > tr:nth-child(2) > td > ul > li")
                .Select(item => item.TextContent), Is.EqualTo(new[] { "alpha", "beta" }));
            Assert.That(frontMatter?.QuerySelector(":scope > tbody > tr:nth-child(3) > td > table > tbody > tr:first-child > td")?
                .TextContent, Is.EqualTo("Alice"));
            Assert.That(frontMatter?.QuerySelector(":scope > tbody > tr:nth-child(3) > td > table > tbody > tr:nth-child(2) li")?
                .TextContent, Is.EqualTo("ready"));
            Assert.That(frontMatter?.TextContent, Does.Contain("<script>alert(1)</script>"));
            Assert.That(document.QuerySelector("script"), Is.Null);
        });
    }

    /// <summary>複数行値内の改ページ文字列を値として残し、本文の改ページだけ処理する。</summary>
    [Test]
    public void Build_FrontMatter_PageBreakInsideValueRemainsText()
    {
        using var folder = new TestFolder();
        var input = folder.Write("report.md", """
            ---
            summary: |
              before
              <!-- pagebreak -->
              after
            ---

            # Heading

            <!-- pagebreak -->

            ## Next
            """);

        var html = Build(input, File.ReadAllText(input), folder.PathFor("output"), new ConversionOptions());
        var document = new HtmlParser().ParseDocument(html);

        Assert.Multiple(() =>
        {
            Assert.That(document.QuerySelector(".frontmatter td")?.TextContent,
                Does.Contain("<!-- pagebreak -->"));
            Assert.That(document.QuerySelectorAll(".pagebreak"), Has.Count.EqualTo(1));
            Assert.That(document.QuerySelector("h2")?.TextContent, Is.EqualTo("Next"));
        });
    }

    /// <summary>壊れたYAMLを通常の本文へ混ぜず、変換エラーとして知らせる。</summary>
    [Test]
    public void Build_FrontMatter_InvalidYamlFails()
    {
        using var folder = new TestFolder();
        var input = folder.Write("report.md", "---\nitems: [one, two\n---\n\n# Heading");

        Assert.That(() => Build(input, File.ReadAllText(input), folder.PathFor("output"), new ConversionOptions()),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void Build_Math_CopiesKaTeXLicenseWithFonts()
    {
        using var folder = new TestFolder();
        var input = folder.Write("report.md", "<span class=\"math\">x</span>");
        var output = folder.PathFor("output");

        var html = Build(input, File.ReadAllText(input), output, new ConversionOptions());
        var license = folder.PathFor("output/report_assets/template/fonts/LICENSE-KaTeX.txt");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("report_assets/template/fonts/KaTeX_Main-Regular.woff2"));
            Assert.That(File.Exists(license), Is.True);
            Assert.That(File.ReadAllText(license), Does.Contain("SIL OPEN FONT LICENSE Version 1.1"));
        });
    }

    /// <summary>テスト用テンプレートと素材出力先を使ってHTML文書を組み立てる。</summary>
    private static string Build(string input, string markdown, string output, ConversionOptions options,
        IReadOnlyDictionary<string, string>? targets = null)
    {
        var template = new TemplateDocument(Path.GetDirectoryName(input)!,
            "<!doctype html><html><head><title>{{title}}</title><style>{{style}}</style></head><body>{{content}}</body></html>",
            "body { color: black; }");
        var assets = new AssetWriter(output, Path.GetFileNameWithoutExtension(input));
        return new MarkdownDocumentBuilder().Build(input, markdown, output,
            Path.GetFileNameWithoutExtension(input), options, template, assets, targets ?? new Dictionary<string, string>());
    }
}
