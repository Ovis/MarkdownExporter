using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MarkdownExporter.Conversion;
using MarkdownExporter.Localization;

namespace MarkdownExporter.Controls;

/// <summary>PDFの各辺の余白を入力し、現在値の要約を表示する。</summary>
public partial class PdfMarginsControl : UserControl
{
    /// <summary>初期余白を表示し、言語変更時の要約更新を登録する。</summary>
    public PdfMarginsControl()
    {
        InitializeComponent();
        SetMargins(new PdfMargins());
        Loaded += (_, _) =>
        {
            LanguageManager.Instance.LanguageChanged -= LanguageChanged;
            LanguageManager.Instance.LanguageChanged += LanguageChanged;
            UpdateSummary();
        };
        Unloaded += (_, _) => LanguageManager.Instance.LanguageChanged -= LanguageChanged;
    }

    /// <summary>保存済みの余白を現在の表示言語で入力欄に反映する。</summary>
    public void SetMargins(PdfMargins? margins)
    {
        margins ??= new PdfMargins();
        TopText.Text = margins.Top.ToString(CultureInfo.CurrentCulture);
        BottomText.Text = margins.Bottom.ToString(CultureInfo.CurrentCulture);
        LeftText.Text = margins.Left.ToString(CultureInfo.CurrentCulture);
        RightText.Text = margins.Right.ToString(CultureInfo.CurrentCulture);
        UpdateSummary();
    }

    /// <summary>入力値を読み取り、用紙の向きに対して有効な余白を返す。</summary>
    /// <exception cref="ArgumentException">入力が数値でないか、印刷領域が残らない場合。</exception>
    public PdfMargins ReadMargins(PageOrientation orientation)
    {
        decimal Read(string text)
        {
            const NumberStyles styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            if (!decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out var value) || value < 0)
                throw new ArgumentException(LanguageManager.Text("InvalidPdfMargins"));
            return value;
        }

        var margins = new PdfMargins
        {
            Top = Read(TopText.Text),
            Bottom = Read(BottomText.Text),
            Left = Read(LeftText.Text),
            Right = Read(RightText.Text)
        };
        _ = PdfPageLayout.Create(margins, orientation);
        return margins;
    }

    /// <summary>余白欄の変更を要約表示へ反映する。</summary>
    private void MarginTextChanged(object sender, TextChangedEventArgs e) => UpdateSummary();
    /// <summary>表示言語の変更を要約表示へ反映する。</summary>
    private void LanguageChanged(object? sender, EventArgs e) => UpdateSummary();

    /// <summary>現在の入力値を折りたたみ時の要約へ表示する。</summary>
    private void UpdateSummary()
    {
        if (SummaryText is null || TopText is null || BottomText is null || LeftText is null || RightText is null)
            return;
        SummaryText.Text = LanguageManager.Format("PdfMarginsSummary",
            TopText.Text, BottomText.Text, LeftText.Text, RightText.Text);
    }
}
