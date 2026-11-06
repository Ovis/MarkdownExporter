using MarkdownExporter.Localization;

namespace MarkdownExporter.Conversion;

public sealed record CommandLine(bool Run, bool Help, IReadOnlyList<string> Files, ConversionOptions Options);

/// <summary>CLI引数を変換設定と入力ファイルに解釈する。</summary>
public static class CommandLineParser
{
    /// <summary>GUI起動と直接変換の引数を解析し、依存するオプションの組み合わせを検証する。</summary>
    public static CommandLine Parse(string[] args)
    {
        var files = new List<string>();
        var options = new ConversionOptions();
        var run = false;
        var help = false;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Value()
            {
                if (++i >= args.Length)
                    throw new ArgumentException(LanguageManager.Format("CliMissingValue", arg));
                return args[i];
            }
            switch (arg)
            {
                case "--run":
                    run = true;
                    break;
                case "--help":
                case "-h":
                    help = true;
                    break;
                case "-o":
                case "--output":
                    options.OutputDirectory = Value();
                    break;
                case "--template":
                    options.Template = Value();
                    break;
                case "--embed-images":
                    options.EmbedImages = true;
                    break;
                case "--fit-figures-to-page":
                    options.FitFiguresToPage = true;
                    break;
                case "--keep-heading-with-figure":
                    options.KeepHeadingWithFigure = true;
                    break;
                case "--follow-markdown-links":
                    options.FollowMarkdownLinks = true;
                    break;
                case "--overwrite":
                    options.Overwrite = true;
                    break;
                case "--format":
                    options.Format = Value().ToLowerInvariant() switch
                    {
                        "html" => ExportFormat.Html,
                        "pdf" => ExportFormat.Pdf,
                        "both" => ExportFormat.Both,
                        _ => throw new ArgumentException(LanguageManager.Text("CliInvalidFormat"))
                    };
                    break;
                case "--orientation":
                    options.Orientation = Value().ToLowerInvariant() switch
                    {
                        "portrait" => PageOrientation.Portrait,
                        "landscape" => PageOrientation.Landscape,
                        _ => throw new ArgumentException(LanguageManager.Text("CliInvalidOrientation"))
                    };
                    break;
                case "--encoding":
                    options.Encoding = Value().ToLowerInvariant() switch
                    {
                        "auto" => InputEncoding.Auto,
                        "utf-8" => InputEncoding.Utf8,
                        "shift-jis" => InputEncoding.ShiftJis,
                        "euc-jp" => InputEncoding.EucJp,
                        _ => throw new ArgumentException(LanguageManager.Text("CliInvalidEncoding"))
                    };
                    break;
                default:
                    if (arg.StartsWith('-'))
                        throw new ArgumentException(LanguageManager.Format("CliUnknownOption", arg));
                    files.Add(arg);
                    break;
            }
        }
        if (run && files.Count == 0 && !help)
            throw new ArgumentException(LanguageManager.Text("CliRequiresFile"));
        if (run && options.KeepHeadingWithFigure && !options.FitFiguresToPage && !help)
            throw new ArgumentException(LanguageManager.Text("CliKeepHeadingRequiresFit"));
        if (!run && args.Any(x => x.StartsWith('-')) && !help)
            throw new ArgumentException(LanguageManager.Text("CliRequiresRun"));
        if (!run)
            files.RemoveAll(path => !ConversionService.IsMarkdown(path) || Directory.Exists(path));
        return new CommandLine(run, help, files, options);
    }

    public static string Usage => $"""
        mdexport --run <input.md> [other.md ...] [options]
          -o, --output <dir>             {LanguageManager.Text("CliOutputDesc")}
          --template <name-or-dir>      {LanguageManager.Text("CliTemplateDesc")}
          --format html|pdf|both        {LanguageManager.Text("CliFormatDesc")}
          --embed-images               {LanguageManager.Text("CliEmbedDesc")}
          --orientation portrait|landscape  {LanguageManager.Text("CliOrientationDesc")}
          --fit-figures-to-page        {LanguageManager.Text("CliFitDesc")}
          --keep-heading-with-figure  {LanguageManager.Text("CliKeepHeadingDesc")}
          --follow-markdown-links      {LanguageManager.Text("CliFollowMarkdownDesc")}
          --encoding auto|utf-8|shift-jis|euc-jp  {LanguageManager.Text("CliEncodingDesc")}
          --overwrite                  {LanguageManager.Text("CliOverwriteDesc")}
        """;
}
