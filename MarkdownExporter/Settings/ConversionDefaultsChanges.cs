using MarkdownExporter.Conversion;

namespace MarkdownExporter.Settings;

/// <summary>保存前後の既定設定を比較し、今回の変換へ反映する項目を示す。</summary>
internal sealed class ConversionDefaultsChanges
{
    /// <summary>保存前後の値を比較して各項目の変更有無を記録する。</summary>
    public ConversionDefaultsChanges(ConversionOptions previous, ConversionOptions saved)
    {
        Saved = saved.Copy();
        FormatChanged = previous.Format != saved.Format;
        OutputDirectoryChanged = !string.Equals(NormalizeOutputDirectory(previous.OutputDirectory),
            NormalizeOutputDirectory(saved.OutputDirectory), StringComparison.Ordinal);
        TemplateChanged = !string.Equals(previous.Template, saved.Template, StringComparison.Ordinal);
        EmbedImagesChanged = previous.EmbedImages != saved.EmbedImages;
        OrientationChanged = previous.Orientation != saved.Orientation;
        MarginsChanged = previous.Margins != saved.Margins;
        FitFiguresToPageChanged = previous.FitFiguresToPage != saved.FitFiguresToPage;
        KeepHeadingWithFigureChanged = previous.KeepHeadingWithFigure != saved.KeepHeadingWithFigure;
        FollowMarkdownLinksChanged = previous.FollowMarkdownLinks != saved.FollowMarkdownLinks;
    }

    public ConversionOptions Saved { get; }
    public bool FormatChanged { get; }
    public bool OutputDirectoryChanged { get; }
    public bool TemplateChanged { get; }
    public bool EmbedImagesChanged { get; }
    public bool OrientationChanged { get; }
    /// <summary>PDF余白が保存時に変わったかを示す。</summary>
    public bool MarginsChanged { get; }
    public bool FitFiguresToPageChanged { get; }
    /// <summary>図と見出しの配置設定が保存時に変わったかを示す。</summary>
    public bool KeepHeadingWithFigureChanged { get; }
    public bool FollowMarkdownLinksChanged { get; }
    public bool HasChanges => FormatChanged || OutputDirectoryChanged || TemplateChanged || EmbedImagesChanged
        || OrientationChanged || MarginsChanged || FitFiguresToPageChanged || KeepHeadingWithFigureChanged
        || FollowMarkdownLinksChanged;

    private static string? NormalizeOutputDirectory(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
