namespace MarkdownExporter.Conversion;

public enum ExportFormat
{
    Html,
    Pdf,
    Both
}

public enum InputEncoding
{
    Auto,
    Utf8,
    ShiftJis,
    EucJp
}

public enum PageOrientation
{
    Portrait,
    Landscape
}

public sealed class ConversionOptions
{
    public ExportFormat Format { get; set; } = ExportFormat.Both;
    public string? OutputDirectory { get; set; }
    public string Template { get; set; } = "default";
    public bool EmbedImages { get; set; }
    public PageOrientation Orientation { get; set; }
    public bool FitFiguresToPage { get; set; }
    /// <summary>PDFで図の直前にある見出し1つを図と同じページに配置する。</summary>
    public bool KeepHeadingWithFigure { get; set; }
    public bool FollowMarkdownLinks { get; set; }
    public bool Overwrite { get; set; }
    public InputEncoding Encoding { get; set; } = InputEncoding.Auto;

    public ConversionOptions Copy() => (ConversionOptions)MemberwiseClone();
}

public sealed record InputFile(string Path, InputEncoding Encoding);
public sealed record ConversionResult(string Path, bool HtmlSuccess, bool PdfSuccess, string? Error, IReadOnlyList<string> Warnings)
{
    public bool Success => Error is null;
}
