using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MarkdownExporter.Conversion;
using MarkdownExporter.Dialogs;
using MarkdownExporter.Localization;
using MarkdownExporter.Settings;
using MarkdownExporter.Themes;
using Microsoft.Win32;

namespace MarkdownExporter;

public partial class DefaultsWindow : Window
{
    private readonly App app = (App)Application.Current;
    private readonly AppThemePreference originalTheme;
    private readonly LanguagePreference originalLanguage;
    private readonly ObservableCollection<string> templateChoices = [];
    private readonly bool ready;

    internal ConversionDefaultsChanges? SavedConversionChanges { get; private set; }

    /// <summary>保存済みの既定設定を編集画面へ読み込む。</summary>
    public DefaultsWindow()
    {
        InitializeComponent();
        TemplateCombo.ItemsSource = templateChoices;
        originalTheme = app.Settings.Theme;
        originalLanguage = LanguageManager.Instance.Preference;
        ThemeCombo.SelectedIndex = (int)originalTheme;
        LanguageCombo.SelectedIndex = (int)originalLanguage;
        var defaults = app.Settings.Defaults;
        HtmlCheck.IsChecked = defaults.Format != ExportFormat.Pdf;
        PdfCheck.IsChecked = defaults.Format != ExportFormat.Html;
        var sourceFolder = string.IsNullOrWhiteSpace(defaults.OutputDirectory);
        OutputText.Text = sourceFolder ? "" : defaults.OutputDirectory!.Trim();
        SourceFolderRadio.IsChecked = sourceFolder;
        CustomFolderRadio.IsChecked = !sourceFolder;
        RefreshTemplates();
        SelectTemplate(defaults.Template);
        EmbedCheck.IsChecked = defaults.EmbedImages;
        PortraitRadio.IsChecked = defaults.Orientation == PageOrientation.Portrait;
        LandscapeRadio.IsChecked = defaults.Orientation == PageOrientation.Landscape;
        MarginEditor.SetMargins(defaults.Margins);
        FitCheck.IsChecked = defaults.FitFiguresToPage;
        KeepHeadingCheck.IsChecked = defaults.KeepHeadingWithFigure;
        FollowMarkdownCheck.IsChecked = defaults.FollowMarkdownLinks;
        ready = true;
    }

    private void Window_SourceInitialized(object sender, EventArgs e) => app.ThemeManager.ApplyTitleBar(this);
    private void Window_Loaded(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, SettingsScroll.ScrollToTop);
    private void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ready && ThemeCombo.SelectedIndex >= 0)
            app.ThemeManager.SetPreference((AppThemePreference)ThemeCombo.SelectedIndex);
    }
    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ready && LanguageCombo.SelectedIndex >= 0)
            LanguageManager.Instance.SetPreference((LanguagePreference)LanguageCombo.SelectedIndex);
    }
    private void RefreshTemplates()
    {
        var selected = TemplateCombo.SelectedItem as string;
        templateChoices.Clear();
        foreach (var name in TemplateStore.Names().OrderBy(x => x))
            templateChoices.Add(name);
        if (selected is not null)
            SelectTemplate(selected);
    }
    private void SelectTemplate(string value)
    {
        if (!templateChoices.Contains(value))
            templateChoices.Add(value);
        TemplateCombo.SelectedItem = value;
    }
    private void RefreshTemplates_Click(object sender, RoutedEventArgs e) => RefreshTemplates();
    private void OpenTemplate_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(TemplateStore.Root);
        Process.Start(new ProcessStartInfo { FileName = TemplateStore.Root, UseShellExecute = true });
    }
    private void ResetTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Confirm(this, LanguageManager.Text("TemplateResetPrompt"), LanguageManager.Text("DialogConfirmation")))
            return;
        var source = Path.Combine(AppContext.BaseDirectory, "Template", "default");
        var dest = Path.Combine(TemplateStore.Root, "default");
        try
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            RefreshTemplates();
        }
        catch (Exception ex) { AppDialog.ShowMessage(this, ex.Message, LanguageManager.Text("DialogTemplateResetError")); }
    }
    private void OutputBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) == true)
            OutputText.Text = dialog.FolderName;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (DialogResult != true)
        {
            app.ThemeManager.SetPreference(originalTheme);
            LanguageManager.Instance.SetPreference(originalLanguage);
        }
        base.OnClosing(e);
    }
    /// <summary>既定の変換設定を検証して保存し、今回の変換へ反映するための変更内容を残す。</summary>
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (HtmlCheck.IsChecked != true && PdfCheck.IsChecked != true)
        {
            AppDialog.ShowMessage(this, LanguageManager.Text("SelectHtmlOrPdf"), LanguageManager.Text("DialogSettingsError"));
            return;
        }
        if (CustomFolderRadio.IsChecked == true && string.IsNullOrWhiteSpace(OutputText.Text))
        {
            AppDialog.ShowMessage(this, LanguageManager.Text("OutputDirectoryRequired"), LanguageManager.Text("DialogSettingsError"));
            return;
        }
        var orientation = LandscapeRadio.IsChecked == true ? PageOrientation.Landscape : PageOrientation.Portrait;
        PdfMargins margins;
        try
        {
            margins = PdfCheck.IsChecked == true ? MarginEditor.ReadMargins(orientation) : app.Settings.Defaults.Margins;
        }
        catch (Exception ex)
        {
            AppDialog.ShowMessage(this, ex.Message, LanguageManager.Text("DialogSettingsError"));
            return;
        }
        var options = new ConversionOptions
        {
            Format = HtmlCheck.IsChecked == true && PdfCheck.IsChecked == true ? ExportFormat.Both
                : HtmlCheck.IsChecked == true ? ExportFormat.Html : ExportFormat.Pdf,
            OutputDirectory = SourceFolderRadio.IsChecked == true ? null : OutputText.Text.Trim(),
            Template = (TemplateCombo.SelectedItem as string ?? "").Trim(),
            EmbedImages = EmbedCheck.IsChecked == true,
            Orientation = orientation,
            Margins = margins,
            FitFiguresToPage = FitCheck.IsChecked == true,
            KeepHeadingWithFigure = KeepHeadingCheck.IsChecked == true,
            FollowMarkdownLinks = FollowMarkdownCheck.IsChecked == true
        };
        try
        {
            TemplateStore.Load(options.Template);
            var changes = new ConversionDefaultsChanges(app.Settings.Defaults, options);
            app.Settings.Defaults = options;
            app.Settings.Theme = (AppThemePreference)ThemeCombo.SelectedIndex;
            app.Settings.Language = (LanguagePreference)LanguageCombo.SelectedIndex;
            SettingsStore.Save(app.Settings);
            SavedConversionChanges = changes;
            DialogResult = true;
        }
        catch (Exception ex) { AppDialog.ShowMessage(this, ex.Message, LanguageManager.Text("DialogSettingsError")); }
    }
}
