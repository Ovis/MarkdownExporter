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

/// <summary>PDFの各辺に指定する余白をmm単位で保持する。</summary>
public sealed record PdfMargins
{
    /// <summary>上余白。</summary>
    public decimal Top { get; init; } = 15m;
    /// <summary>下余白。</summary>
    public decimal Bottom { get; init; } = 15m;
    /// <summary>左余白。</summary>
    public decimal Left { get; init; } = 15m;
    /// <summary>右余白。</summary>
    public decimal Right { get; init; } = 15m;
}

public sealed class ConversionOptions
{
    public ExportFormat Format { get; set; } = ExportFormat.Both;
    public string? OutputDirectory { get; set; }
    public string Template { get; set; } = "default";
    public bool EmbedImages { get; set; }
    public PageOrientation Orientation { get; set; }
    /// <summary>PDF印刷と図の縮小に適用する余白。</summary>
    public PdfMargins Margins { get; set; } = new();
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
