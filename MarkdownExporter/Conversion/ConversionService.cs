using System.Text;
using MarkdownExporter.Localization;

namespace MarkdownExporter.Conversion;

public enum PreflightIssueKind { MissingInput, InvalidMarkdown, OutputExists }

public sealed record PreflightIssue(PreflightIssueKind Kind, string Path)
{
    public string Message => LanguageManager.Format(Kind switch
    {
        PreflightIssueKind.MissingInput => "InputMissing",
        PreflightIssueKind.InvalidMarkdown => "InvalidMarkdown",
        _ => "OutputExists"
    }, Path);
}

public sealed class ConversionService
{
    private readonly MarkdownDocumentBuilder builder = new();

    public static IReadOnlyList<PreflightIssue> Preflight(IReadOnlyList<InputFile> inputs, ConversionOptions options)
    {
        return Preflight(ConversionPlan.Create(inputs, options), options);
    }

    private static IReadOnlyList<PreflightIssue> Preflight(ConversionPlan plan, ConversionOptions options)
    {
        var errors = new List<PreflightIssue>();
        foreach (var input in plan.Documents)
        {
            var path = input.Path;
            if (!File.Exists(path))
                errors.Add(new(PreflightIssueKind.MissingInput, path));
            if (!IsMarkdown(path))
                errors.Add(new(PreflightIssueKind.InvalidMarkdown, path));
            foreach (var format in options.Format switch
            {
                ExportFormat.Html => new[] { ".html" },
                ExportFormat.Pdf => [".pdf"],
                _ => [".html", ".pdf"]
            })
            {
                var target = format == ".html" ? input.HtmlPath : input.PdfPath;
                if (File.Exists(target) && !options.Overwrite)
                    errors.Add(new(PreflightIssueKind.OutputExists, target));
            }
        }
        return errors;
    }

    public static bool IsMarkdown(string path) => Path.GetExtension(path).ToLowerInvariant() is ".md" or ".markdown";

    /// <summary>入力文書を描画し、指定されたHTMLとPDFを出力する。</summary>
    public async Task<IReadOnlyList<ConversionResult>> ConvertAsync(IReadOnlyList<InputFile> inputs,
        ConversionOptions options, Action<ConversionResult>? onResult = null,
        Action<string, string>? onProgress = null)
    {
        var plan = await Task.Run(() => ConversionPlan.Create(inputs, options));
        WebView2Runtime.EnsureAvailable();
        TemplateStore.Initialize();
        var template = TemplateStore.Load(options.Template);
        var htmlWanted = options.Format is ExportFormat.Html or ExportFormat.Both;
        var pdfWanted = options.Format is ExportFormat.Pdf or ExportFormat.Both;
        var htmlTargets = plan.Documents.ToDictionary(x => x.Path, x => x.HtmlPath, StringComparer.OrdinalIgnoreCase);
        var pdfTargets = plan.Documents.ToDictionary(x => x.Path, x => x.PdfPath, StringComparer.OrdinalIgnoreCase);
        var convertedMarkdown = htmlTargets.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = new List<PendingDocument>();
        using var renderer = new WebViewRenderer();
        try
        {
            // First determine which HTML files can actually be produced.
            foreach (var input in plan.Documents)
            {
                var path = input.Path;
                var outputDir = input.OutputDirectory;
                var stem = input.Stem;
                var entry = new PendingDocument(input);
                pending.Add(entry);
                if (plan.MissingLinks.TryGetValue(path, out var missingLinks))
                    entry.Warnings.AddRange(missingLinks.Select(link => LanguageManager.Format("LinkedMarkdownMissing", link)));
                try
                {
                    if (!File.Exists(path))
                        throw new FileNotFoundException(LanguageManager.Format("InputMissing", path));
                    if (!IsMarkdown(path))
                        throw new InvalidDataException(LanguageManager.Format("InvalidMarkdown", path));
                    if (!options.Overwrite && ((htmlWanted && File.Exists(entry.HtmlPath)) || (pdfWanted && File.Exists(entry.PdfPath))))
                        throw new IOException(LanguageManager.Format("OutputExists", htmlWanted && File.Exists(entry.HtmlPath) ? entry.HtmlPath : entry.PdfPath));
                    Directory.CreateDirectory(outputDir);
                    var markdown = EncodingReader.Read(path, input.Input.Encoding);
                    entry.MarkdownLinks = LinkedMarkdownLinks.Find(path, markdown);
                    entry.RelativeLinkCount = RelativeLinkAnalyzer.Find(path, markdown, outputDir, convertedMarkdown).Count;
                    entry.Assets = new AssetWriter(outputDir, stem);
                    var html = builder.Build(path, markdown, outputDir, stem, options, template, entry.Assets, htmlTargets);
                    File.WriteAllText(entry.StagePath, html, new UTF8Encoding(false));
                    var rendered = await renderer.RenderAsync(entry.StagePath, null, options);
                    entry.Warnings.AddRange(rendered.Warnings);
                    if (htmlWanted)
                    {
                        WriteAtomically(entry.HtmlPath, rendered.Html);
                        entry.HtmlSuccess = true;
                    }
                    entry.Ready = true;
                    onProgress?.Invoke(path, htmlWanted ? "HtmlDone" : "RenderDone");
                }
                catch (Exception ex) { entry.Error = ex.Message; onProgress?.Invoke(path, "Failure"); }
            }

            if (htmlWanted)
                FinalizeMarkdownLinks(pending);

            // Print after all renderable documents are known. PDF links point to PDFs,
            // including when HTML is generated in the same run.
            if (pdfWanted)
            {
                var ready = pending.Where(x => x.Ready).Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in pending.Where(x => x.Ready && x.Error is null))
                {
                    var temporaryPdf = entry.PdfTemporaryPath;
                    try
                    {
                        var pdfHtml = MarkdownLinkRewriter.Rewrite(File.ReadAllText(entry.StagePath), entry.Path,
                            ready, entry.Warnings, !htmlWanted, pdfTargets, entry.OutputDirectory);
                        WriteAtomically(entry.StagePath, pdfHtml);
                        var rendered = await renderer.RenderAsync(entry.StagePath, temporaryPdf, options);
                        foreach (var warning in rendered.Warnings.Except(entry.Warnings))
                            entry.Warnings.Add(warning);
                        entry.PdfSuccess = true;
                        onProgress?.Invoke(entry.Path, "PdfDone");
                    }
                    catch (Exception ex) { entry.Error = ex.Message; onProgress?.Invoke(entry.Path, "Failure"); }
                    finally
                    {
                        if (!entry.PdfSuccess && File.Exists(temporaryPdf))
                            File.Delete(temporaryPdf);
                    }
                }
                await FinalizePdfLinksAsync(pending, pdfTargets);
            }
        }
        finally
        {
            foreach (var entry in pending)
            {
                if (File.Exists(entry.StagePath))
                    File.Delete(entry.StagePath);
                if (File.Exists(entry.PdfTemporaryPath))
                    File.Delete(entry.PdfTemporaryPath);
                if (!entry.HtmlSuccess)
                    entry.Assets?.CleanCreated();
            }
        }

        foreach (var entry in pending.Where(x => (x.HtmlSuccess || x.PdfSuccess) && x.RelativeLinkCount > 0))
            entry.Warnings.Add(LanguageManager.Format("RelativeLinksWarning", entry.RelativeLinkCount));

        var results = pending.Select(x => new ConversionResult(x.Path, x.HtmlSuccess, x.PdfSuccess, x.Error,
            x.Warnings.Distinct().ToArray(), x.HtmlSuccess ? x.HtmlPath : null,
            x.PdfSuccess ? x.PdfPath : null)).ToArray();
        foreach (var result in results)
            onResult?.Invoke(result);
        return results;
    }

    private static void FinalizeMarkdownLinks(List<PendingDocument> pending)
    {
        var successful = pending.Where(x => x.HtmlSuccess).Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in pending.Where(x => x.HtmlSuccess))
        {
            try
            {
                var completedHtml = MarkdownLinkRewriter.Rewrite(File.ReadAllText(entry.HtmlPath), entry.Path, successful, entry.Warnings, true);
                WriteAtomically(entry.HtmlPath, completedHtml);
            }
            catch (Exception ex) { entry.Error = LanguageManager.Format("LinkFinalizeFailed", ex.Message); }
        }
    }

    private static async Task FinalizePdfLinksAsync(List<PendingDocument> pending, IReadOnlyDictionary<string, string> pdfTargets)
    {
        var printed = pending.Where(x => x.PdfSuccess).ToArray();
        var rewritten = new Dictionary<PendingDocument, bool>();
        try
        {
            // A link-finalization failure removes that target from the conversion set.
            // Retry the remaining PDFs so links to it can revert to their original URLs.
            bool targetFailed;
            do
            {
                targetFailed = false;
                var successful = printed.Where(x => x.PdfSuccess).Select(x => x.Path)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in printed.Where(x => x.PdfSuccess))
                {
                    var portablePdf = entry.StagePath + ".portable.pdf";
                    try
                    {
                        if (File.Exists(portablePdf))
                            File.Delete(portablePdf);
                        var originalLinks = BuildOriginalPdfLinks(entry, pdfTargets, successful);
                        rewritten[entry] = await PdfLinkRewriter.RewriteLocalPdfLinksAsync(
                            entry.PdfTemporaryPath, portablePdf, entry.PdfPath,
                            originalMarkdownLinks: originalLinks);
                    }
                    catch (Exception ex)
                    {
                        entry.Error = LanguageManager.Format("LinkFinalizeFailed", ex.Message);
                        entry.PdfSuccess = false;
                        targetFailed = true;
                    }
                }
            }
            while (targetFailed);

            var completed = printed.Where(x => x.PdfSuccess).Select(x => x.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in printed.Where(x => x.PdfSuccess))
            {
                foreach (var link in entry.MarkdownLinks.Where(x =>
                    pdfTargets.ContainsKey(x.TargetPath) && !completed.Contains(x.TargetPath)))
                    entry.Warnings.Add(LanguageManager.Format("LinkTargetFailed", link.Url));
                try
                {
                    var source = rewritten[entry] ? entry.StagePath + ".portable.pdf" : entry.PdfTemporaryPath;
                    File.Move(source, entry.PdfPath, true);
                }
                catch (Exception ex)
                {
                    entry.Error = LanguageManager.Format("LinkFinalizeFailed", ex.Message);
                    entry.PdfSuccess = false;
                }
            }
        }
        finally
        {
            foreach (var entry in printed)
            {
                if (File.Exists(entry.PdfTemporaryPath))
                    File.Delete(entry.PdfTemporaryPath);
                var portablePdf = entry.StagePath + ".portable.pdf";
                if (File.Exists(portablePdf))
                    File.Delete(portablePdf);
            }
        }
    }

    private static Dictionary<string, string> BuildOriginalPdfLinks(PendingDocument entry,
        IReadOnlyDictionary<string, string> pdfTargets, IReadOnlySet<string> successful)
    {
        var originalLinks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in entry.MarkdownLinks)
        {
            if (successful.Contains(link.TargetPath))
                continue;
            var (pathPart, suffix) = MarkdownDocumentBuilder.SplitUrl(link.Url);
            var originalTarget = Path.GetFullPath(Path.Combine(entry.OutputDirectory,
                Uri.UnescapeDataString(pathPart).Replace('/', Path.DirectorySeparatorChar)));
            originalLinks[new Uri(originalTarget).AbsoluteUri + suffix] = link.Url;
            if (pdfTargets.TryGetValue(link.TargetPath, out var pdfPath))
                originalLinks[new Uri(pdfPath).AbsoluteUri + suffix] = link.Url;
        }
        return originalLinks;
    }

    private static void WriteAtomically(string path, string contents)
    {
        var temporary = path + ".mdexport-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, contents, new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class PendingDocument(PlannedInput input)
    {
        public string Path { get; } = input.Path;
        public string OutputDirectory { get; } = input.OutputDirectory;
        public string HtmlPath { get; } = input.HtmlPath;
        public string PdfPath { get; } = input.PdfPath;
        public string StagePath { get; } = System.IO.Path.Combine(input.OutputDirectory, ".mdexport-" + Guid.NewGuid().ToString("N") + ".html");
        public string PdfTemporaryPath => StagePath + ".pdf";
        public AssetWriter? Assets
        {
            get; set;
        }
        public List<string> Warnings { get; } = [];
        public IReadOnlyList<LinkedMarkdownLink> MarkdownLinks { get; set; } = [];
        public string? Error
        {
            get; set;
        }
        public bool Ready
        {
            get; set;
        }
        public bool HtmlSuccess
        {
            get; set;
        }
        public bool PdfSuccess
        {
            get; set;
        }
        public int RelativeLinkCount
        {
            get; set;
        }
    }
}
