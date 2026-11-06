using MarkdownExporter.Conversion;

namespace MarkdownExporter.Tests;

/// <summary>A4印刷領域と余白の境界条件を確認する。</summary>
[TestFixture]
public sealed class PdfPageLayoutTests
{
    /// <summary>既定の15mm余白で縦向きの印刷領域を計算する。</summary>
    [Test]
    public void DefaultMargins_KeepPortraitPrintableArea()
    {
        var layout = PdfPageLayout.Create(new PdfMargins(), PageOrientation.Portrait);

        Assert.Multiple(() =>
        {
            Assert.That(layout.FigureMaxWidthMm, Is.EqualTo(180m));
            Assert.That(layout.FigureMaxHeightMm, Is.EqualTo(267m));
            Assert.That(layout.PageCss, Is.EqualTo("@page{size:A4 portrait;margin:15mm 15mm 15mm 15mm}"));
            Assert.That(PdfPageLayout.Inches(layout.Margins.Top), Is.EqualTo(15d / 25.4d).Within(0.000001));
        });
    }

    /// <summary>横向きの個別余白を図の上限とCSSへ反映する。</summary>
    [Test]
    public void CustomMargins_AdjustLandscapePrintableAreaAndCss()
    {
        var margins = new PdfMargins { Top = 5m, Bottom = 20m, Left = 10.5m, Right = 25m };
        var layout = PdfPageLayout.Create(margins, PageOrientation.Landscape);

        Assert.Multiple(() =>
        {
            Assert.That(layout.FigureMaxWidthMm, Is.EqualTo(261.5m));
            Assert.That(layout.FigureMaxHeightMm, Is.EqualTo(185m));
            Assert.That(layout.PageCss, Is.EqualTo("@page{size:A4 landscape;margin:5mm 25mm 20mm 10.5mm}"));
        });
    }

    /// <summary>既定の横向きでは印刷可能な高さ全体を図の上限にする。</summary>
    [Test]
    public void DefaultMargins_UseFullLandscapePrintableHeight()
    {
        var layout = PdfPageLayout.Create(new PdfMargins(), PageOrientation.Landscape);

        Assert.Multiple(() =>
        {
            Assert.That(layout.FigureMaxWidthMm, Is.EqualTo(267m));
            Assert.That(layout.FigureMaxHeightMm, Is.EqualTo(180m));
        });
    }

    /// <summary>負数や印刷領域がなくなる余白を拒否する。</summary>
    [TestCase(-1, 15, 15, 15)]
    [TestCase(15, 15, 200, 15)]
    [TestCase(150, 150, 15, 15)]
    public void InvalidMargins_AreRejected(decimal top, decimal bottom, decimal left, decimal right)
    {
        var margins = new PdfMargins { Top = top, Bottom = bottom, Left = left, Right = right };

        Assert.Throws<ArgumentException>(() => PdfPageLayout.Create(margins, PageOrientation.Portrait));
    }
}
