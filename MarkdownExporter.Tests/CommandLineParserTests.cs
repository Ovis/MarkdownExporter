using MarkdownExporter.Conversion;

namespace MarkdownExporter.Tests;

/// <summary>CLI引数から変換設定とGUI起動用のファイル一覧を構成できることを確認する。</summary>
[TestFixture]
public sealed class CommandLineParserTests
{
    /// <summary>PDF図と見出しの設定を含むCLIオプションを適用する。</summary>
    [Test]
    public void Parse_RunOptions_AppliesEachOption()
    {
        var command = CommandLineParser.Parse([
            "--run", "first.md", "second.markdown", "-o", "output", "--template", "custom",
            "--format", "pdf", "--encoding", "euc-jp", "--orientation", "landscape",
            "--margin-top", "5", "--margin-bottom", "20", "--margin-left", "10.5", "--margin-right", "25",
            "--embed-images", "--fit-figures-to-page", "--keep-heading-with-figure",
            "--follow-markdown-links", "--overwrite"
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(command.Run, Is.True);
            Assert.That(command.Files, Is.EqualTo(new[] { "first.md", "second.markdown" }));
            Assert.That(command.Options.OutputDirectory, Is.EqualTo("output"));
            Assert.That(command.Options.Template, Is.EqualTo("custom"));
            Assert.That(command.Options.Format, Is.EqualTo(ExportFormat.Pdf));
            Assert.That(command.Options.Encoding, Is.EqualTo(InputEncoding.EucJp));
            Assert.That(command.Options.Orientation, Is.EqualTo(PageOrientation.Landscape));
            Assert.That(command.Options.Margins, Is.EqualTo(new PdfMargins
            { Top = 5m, Bottom = 20m, Left = 10.5m, Right = 25m }));
            Assert.That(command.Options.EmbedImages, Is.True);
            Assert.That(command.Options.FitFiguresToPage, Is.True);
            Assert.That(command.Options.KeepHeadingWithFigure, Is.True);
            Assert.That(command.Options.FollowMarkdownLinks, Is.True);
            Assert.That(command.Options.Overwrite, Is.True);
        });
    }

    /// <summary>図の縮小なしで見出しだけをまとめる指定をCLIで拒否する。</summary>
    [Test]
    public void Parse_KeepHeadingWithoutFit_ReportsMissingOption()
    {
        Assert.That(() => CommandLineParser.Parse(["--run", "report.md", "--keep-heading-with-figure"]),
            Throws.ArgumentException.With.Message.Contains("--fit-figures-to-page"));
    }

    [Test]
    public void Parse_DroppedFiles_OpensGuiWithCliDefaults()
    {
        var command = CommandLineParser.Parse(["first.md", "two spaces.md"]);

        Assert.Multiple(() =>
        {
            Assert.That(command.Run, Is.False);
            Assert.That(command.Files, Is.EqualTo(new[] { "first.md", "two spaces.md" }));
            Assert.That(command.Options.Format, Is.EqualTo(ExportFormat.Both));
            Assert.That(command.Options.Encoding, Is.EqualTo(InputEncoding.Auto));
            Assert.That(command.Options.FollowMarkdownLinks, Is.False);
            Assert.That(command.Options.Margins, Is.EqualTo(new PdfMargins()));
        });
    }

    [Test]
    public void Parse_SendToSelection_IgnoresNonMarkdownFilesAndDirectories()
    {
        using var folder = new TestFolder();
        var first = folder.Write("first.md", "# First");
        var other = folder.Write("notes.txt", "Notes");
        var directory = folder.PathFor("directory.md");
        Directory.CreateDirectory(directory);
        var second = folder.Write("two spaces.MARKDOWN", "# Second");

        var command = CommandLineParser.Parse([first, other, directory, second]);

        Assert.Multiple(() =>
        {
            Assert.That(command.Run, Is.False);
            Assert.That(command.Files, Is.EqualTo(new[] { first, second }));
        });
    }

    [Test]
    public void Parse_Run_KeepsNonMarkdownInputForCliValidation()
    {
        var command = CommandLineParser.Parse(["--run", "notes.txt"]);

        Assert.That(command.Files, Is.EqualTo(new[] { "notes.txt" }));
    }

    [Test]
    public void Parse_SendToSelection_WithOnlyNonMarkdownFiles_OpensEmptyGui()
    {
        var command = CommandLineParser.Parse(["notes.txt", "image.png"]);

        Assert.Multiple(() =>
        {
            Assert.That(command.Run, Is.False);
            Assert.That(command.Files, Is.Empty);
        });
    }

    [TestCase("--run")]
    [TestCase("--run a.md --format other")]
    [TestCase("--run a.md --encoding")]
    [TestCase("--run a.md --unknown")]
    [TestCase("--run a.md --margin-top -1")]
    [TestCase("--run a.md --margin-left 2,5")]
    [TestCase("--run a.md --margin-bottom 999")]
    [TestCase("--run a.md --margin-right")]
    [TestCase("--format pdf a.md")]
    public void Parse_InvalidArguments_Throws(string arguments)
    {
        Assert.Throws<ArgumentException>(() => CommandLineParser.Parse(arguments.Split(' ')));
    }
}
