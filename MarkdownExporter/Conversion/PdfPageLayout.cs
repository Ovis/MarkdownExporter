using System.Globalization;
using MarkdownExporter.Localization;

namespace MarkdownExporter.Conversion;

/// <summary>A4用紙の余白と図を配置できる領域を計算する。</summary>
internal sealed class PdfPageLayout
{
    /// <summary>検証済みの余白から印刷領域を組み立てる。</summary>
    private PdfPageLayout(PdfMargins margins, bool landscape)
    {
        Margins = margins;
        Landscape = landscape;
        PageWidthMm = landscape ? 297m : 210m;
        PageHeightMm = landscape ? 210m : 297m;
        FigureMaxWidthMm = PageWidthMm - margins.Left - margins.Right;
        FigureMaxHeightMm = PageHeightMm - margins.Top - margins.Bottom;
    }

    /// <summary>印刷に適用する余白。</summary>
    public PdfMargins Margins { get; }
    /// <summary>横向きの用紙かどうか。</summary>
    public bool Landscape { get; }
    /// <summary>用紙の幅。</summary>
    public decimal PageWidthMm { get; }
    /// <summary>用紙の高さ。</summary>
    public decimal PageHeightMm { get; }
    /// <summary>余白を除いた図の最大幅。</summary>
    public decimal FigureMaxWidthMm { get; }
    /// <summary>余白を除いた図の最大高さ。</summary>
    public decimal FigureMaxHeightMm { get; }
    /// <summary>指定した向きと余白を反映した印刷用CSS。</summary>
    public string PageCss => $"@page{{size:A4 {(Landscape ? "landscape" : "portrait")};margin:{Mm(Margins.Top)} {Mm(Margins.Right)} {Mm(Margins.Bottom)} {Mm(Margins.Left)}}}";

    /// <summary>余白が用紙内に収まることを検証し、印刷領域を返す。</summary>
    /// <exception cref="ArgumentException">余白が負数、または用紙を使い切る場合。</exception>
    public static PdfPageLayout Create(PdfMargins? margins, PageOrientation orientation)
    {
        if (margins is null || margins.Top < 0 || margins.Bottom < 0 || margins.Left < 0 || margins.Right < 0)
            throw new ArgumentException(LanguageManager.Text("InvalidPdfMargins"));
        var pageWidth = orientation == PageOrientation.Landscape ? 297m : 210m;
        var pageHeight = orientation == PageOrientation.Landscape ? 210m : 297m;
        if (margins.Left >= pageWidth || margins.Right >= pageWidth ||
            margins.Top >= pageHeight || margins.Bottom >= pageHeight ||
            margins.Left + margins.Right >= pageWidth || margins.Top + margins.Bottom >= pageHeight)
            throw new ArgumentException(LanguageManager.Text("PdfMarginsTooLarge"));
        var layout = new PdfPageLayout(margins, orientation == PageOrientation.Landscape);
        return layout;
    }

    /// <summary>mm単位の数値をCSSの長さに変換する。</summary>
    public static string Mm(decimal value) => value.ToString(CultureInfo.InvariantCulture) + "mm";
    /// <summary>mm単位の数値をWebView2印刷設定のインチに変換する。</summary>
    public static double Inches(decimal value) => (double)(value / 25.4m);
}
