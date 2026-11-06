using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MarkdownExporter.Conversion;
using MarkdownExporter.Dialogs;
using MarkdownExporter.Localization;
using MarkdownExporter.Settings;
using Microsoft.Win32;

namespace MarkdownExporter;

public partial class MainWindow : Window
{
    public ObservableCollection<FileRow> Files { get; } = [];
    private readonly ObservableCollection<string> templateChoices = [];
    private readonly App app = (App)Application.Current;
    private (int Success, int Failure)? lastSummary;
    private bool isConverting;

    public MainWindow(IEnumerable<string> initialFiles)
    {
        InitializeComponent();
        FilesGrid.AddHandler(DragDrop.DragOverEvent, new DragEventHandler(FilesGrid_DragOver), true);
        FilesGrid.AddHandler(DragDrop.DropEvent, new DragEventHandler(FilesGrid_Drop), true);
        DataContext = this;
        TemplateCombo.ItemsSource = templateChoices;
        RefreshTemplates();
        ApplyOptions(app.Settings.Defaults);
        AddFiles(initialFiles);
        LanguageManager.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => LanguageManager.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void Window_SourceInitialized(object sender, EventArgs e) => app.ThemeManager.ApplyTitleBar(this);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateFileCount();
        foreach (var row in Files)
            row.RefreshLanguage();
        Status.Text = lastSummary is { } summary
            ? LanguageManager.Format("Summary", summary.Success, summary.Failure)
            : LanguageManager.Text(Files.Count == 0 ? "PromptAddFiles" : "PromptCheckSettings");
    }

    private void UpdateFileCount()
    {
        FileCount.Text = LanguageManager.Format("FilesCount", Files.Count);
        ConvertButton.Content = LanguageManager.Format("ConvertCount", Files.Count(x => !x.IsLinked));
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        var rejected = false;
        foreach (var raw in paths)
        {
            var path = Path.GetFullPath(raw);
            var existing = Files.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.IsLinked = false;
                continue;
            }
            if (Directory.Exists(path) || !ConversionService.IsMarkdown(path))
            {
                Status.Text = LanguageManager.Format("OnlyMarkdown", raw);
                rejected = true;
                continue;
            }
            Files.Add(new FileRow(path));
        }
        UpdateFileCount();
        lastSummary = null;
        if (!rejected)
            Status.Text = LanguageManager.Text(Files.Count == 0 ? "PromptAddFiles" : "PromptCheckSettings");
    }

    private void FilesGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !isConverting && e.Data.GetData(DataFormats.FileDrop) is string[] paths &&
            paths.Any(path => !Directory.Exists(path) && ConversionService.IsMarkdown(path))
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FilesGrid_Drop(object sender, DragEventArgs e)
    {
        if (!isConverting && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            AddFiles(files);
        e.Handled = true;
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (isConverting)
            return;
        var dialog = new OpenFileDialog { Filter = LanguageManager.Text("FilterMarkdown"), Multiselect = true };
        if (dialog.ShowDialog(this) == true)
            AddFiles(dialog.FileNames);
    }

    private void RemoveFiles_Click(object sender, RoutedEventArgs e)
    {
        if (isConverting)
            return;
        var selected = FilesGrid.SelectedItems.Cast<FileRow>().ToArray();
        if (selected.Length == 0)
            return;
        foreach (var row in selected)
            Files.Remove(row);
        UpdateFileCount();
        lastSummary = null;
        Status.Text = LanguageManager.Text(Files.Count == 0 ? "PromptAddFiles" : "PromptCheckSettings");
    }

    private void RemoveMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = RemoveMenuButton.ContextMenu;
        if (menu is null || isConverting)
            return;
        menu.PlacementTarget = RemoveSelectedButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ClearFiles_Click(object sender, RoutedEventArgs e)
    {
        if (isConverting)
            return;
        Files.Clear();
        UpdateFileCount();
        lastSummary = null;
        Status.Text = LanguageManager.Text("PromptAddFiles");
    }

    private void OutputBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) == true)
            OutputText.Text = dialog.FolderName;
    }

    private void TemplateBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) == true)
            SelectTemplate(dialog.FolderName);
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

    /// <summary>変換設定欄の現在値を検証して変換オプションにまとめる。</summary>
    private ConversionOptions ReadOptions()
    {
        if (HtmlCheck.IsChecked != true && PdfCheck.IsChecked != true)
            throw new InvalidOperationException(LanguageManager.Text("SelectHtmlOrPdf"));
        if (CustomFolderRadio.IsChecked == true && string.IsNullOrWhiteSpace(OutputText.Text))
            throw new InvalidOperationException(LanguageManager.Text("OutputDirectoryRequired"));
        var orientation = LandscapeRadio.IsChecked == true ? PageOrientation.Landscape : PageOrientation.Portrait;
        return new ConversionOptions
        {
            Format = HtmlCheck.IsChecked == true && PdfCheck.IsChecked == true ? ExportFormat.Both
                : HtmlCheck.IsChecked == true ? ExportFormat.Html : ExportFormat.Pdf,
            OutputDirectory = SourceFolderRadio.IsChecked == true ? null : OutputText.Text.Trim(),
            Template = (TemplateCombo.SelectedItem as string ?? "").Trim(),
            EmbedImages = EmbedCheck.IsChecked == true,
            Orientation = orientation,
            Margins = PdfCheck.IsChecked == true ? MarginEditor.ReadMargins(orientation) : app.Settings.Defaults.Margins,
            FitFiguresToPage = FitCheck.IsChecked == true,
            KeepHeadingWithFigure = KeepHeadingCheck.IsChecked == true,
            FollowMarkdownLinks = FollowMarkdownCheck.IsChecked == true
        };
    }

    /// <summary>保存済みの変換設定を画面の各項目に反映する。</summary>
    private void ApplyOptions(ConversionOptions options)
    {
        HtmlCheck.IsChecked = options.Format != ExportFormat.Pdf;
        PdfCheck.IsChecked = options.Format != ExportFormat.Html;
        ApplyOutputDirectory(options.OutputDirectory);
        SelectTemplate(options.Template);
        EmbedCheck.IsChecked = options.EmbedImages;
        PortraitRadio.IsChecked = options.Orientation == PageOrientation.Portrait;
        LandscapeRadio.IsChecked = options.Orientation == PageOrientation.Landscape;
        MarginEditor.SetMargins(options.Margins);
        FitCheck.IsChecked = options.FitFiguresToPage;
        KeepHeadingCheck.IsChecked = options.KeepHeadingWithFigure;
        FollowMarkdownCheck.IsChecked = options.FollowMarkdownLinks;
    }

    private void ApplyOutputDirectory(string? directory)
    {
        var sourceFolder = string.IsNullOrWhiteSpace(directory);
        OutputText.Text = sourceFolder ? "" : directory!.Trim();
        SourceFolderRadio.IsChecked = sourceFolder;
        CustomFolderRadio.IsChecked = !sourceFolder;
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e) => ApplyOptions(app.Settings.Defaults);

    private void Defaults_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DefaultsWindow { Owner = this };
        var saved = dialog.ShowDialog() == true;
        RefreshTemplates();
        if (saved && dialog.SavedConversionChanges is { HasChanges: true } changes &&
            AppDialog.Confirm(this, LanguageManager.Text("ApplyDefaultsToCurrentPrompt"),
                LanguageManager.Text("DialogConfirmation")))
        {
            ApplyChangedDefaults(changes);
        }
    }

    /// <summary>利用者が反映を選んだ既定設定の変更項目だけを現在の変換設定へ適用する。</summary>
    private void ApplyChangedDefaults(ConversionDefaultsChanges changes)
    {
        var saved = changes.Saved;
        if (changes.FormatChanged)
        {
            HtmlCheck.IsChecked = saved.Format != ExportFormat.Pdf;
            PdfCheck.IsChecked = saved.Format != ExportFormat.Html;
        }
        if (changes.OutputDirectoryChanged)
            ApplyOutputDirectory(saved.OutputDirectory);
        if (changes.TemplateChanged)
            SelectTemplate(saved.Template);
        if (changes.EmbedImagesChanged)
            EmbedCheck.IsChecked = saved.EmbedImages;
        if (changes.OrientationChanged)
        {
            PortraitRadio.IsChecked = saved.Orientation == PageOrientation.Portrait;
            LandscapeRadio.IsChecked = saved.Orientation == PageOrientation.Landscape;
        }
        if (changes.MarginsChanged)
            MarginEditor.SetMargins(saved.Margins);
        if (changes.FitFiguresToPageChanged)
            FitCheck.IsChecked = saved.FitFiguresToPage;
        if (changes.KeepHeadingWithFigureChanged)
            KeepHeadingCheck.IsChecked = saved.KeepHeadingWithFigure;
        if (changes.FollowMarkdownLinksChanged)
            FollowMarkdownCheck.IsChecked = saved.FollowMarkdownLinks;
    }

    public void ShowWebView2Missing()
    {
        var answer = AppDialog.Confirm(this, LanguageManager.Format("WebView2MissingPrompt", WebView2Runtime.DownloadUrl),
            LanguageManager.Text("WebView2MissingTitle"), AppDialogKind.Warning);
        if (answer)
        {
            try
            {
                Process.Start(new ProcessStartInfo(WebView2Runtime.DownloadUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppDialog.ShowMessage(this, ex.Message, LanguageManager.Text("WebView2MissingTitle"));
            }
        }
    }

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        if (isConverting)
            return;
        if (!Files.Any(x => !x.IsLinked))
        {
            Status.Text = LanguageManager.Text("PromptAddFiles");
            return;
        }
        if (!WebView2Runtime.IsAvailable())
        {
            ShowWebView2Missing();
            return;
        }
        ConversionOptions options;
        try
        {
            options = ReadOptions();
        }
        catch (Exception ex) { AppDialog.ShowMessage(this, ex.Message, LanguageManager.Text("DialogSettingsError")); return; }
        foreach (var linked in Files.Where(x => x.IsLinked).ToArray())
            Files.Remove(linked);
        UpdateFileCount();
        var inputs = Files.Select(x => new InputFile(x.Path, x.SelectedEncoding)).ToArray();
        isConverting = true;
        FileActions.IsEnabled = false;
        ConvertButton.IsEnabled = false;
        try
        {
            Status.Text = LanguageManager.Text("CheckingRelativeLinks");
            var preflight = await Task.Run(() => ConversionService.Preflight(inputs, options));
            var existing = preflight.Where(x => x.Kind == PreflightIssueKind.OutputExists).ToArray();
            if (existing.Length > 0)
            {
                var answer = AppDialog.Confirm(this, LanguageManager.Format("OverwritePrompt", string.Join("\n", existing.Select(x => x.Message))),
                    LanguageManager.Text("DialogConfirmation"));
                if (!answer)
                    return;
                options.Overwrite = true;
            }
            Status.Text = LanguageManager.Text("CheckingRelativeLinks");
            var risks = await Task.Run(() => RelativeLinkAnalyzer.Scan(inputs, options));
            if (risks.Count > 0)
            {
                var total = risks.Sum(x => x.Links.Count);
                var promptKey = options.FollowMarkdownLinks
                    ? "RelativeLinksPromptFollow" : "RelativeLinksPromptListed";
                var message = LanguageManager.Format(promptKey, risks.Count, total);
                if (!AppDialog.Confirm(this, message, LanguageManager.Text("RelativeLinksTitle"),
                    AppDialogKind.Warning, LanguageManager.Text("RelativeLinksContinue"),
                    LanguageManager.Text("RelativeLinksCancel"), showToneMark: false))
                {
                    Status.Text = LanguageManager.Text("PromptCheckSettings");
                    return;
                }
            }

            Status.Text = LanguageManager.Text("Converting");
            foreach (var row in Files)
            {
                row.HtmlStatus = options.Format == ExportFormat.Pdf ? "NotApplicable" : "Waiting";
                row.PdfStatus = options.Format == ExportFormat.Html ? "NotApplicable" : "Waiting";
                row.State = "Processing";
                row.Detail = "";
            }
            var results = await new ConversionService().ConvertAsync(inputs, options,
                result =>
                {
                    var row = FindOrAddRow(result.Path);
                    row.HtmlStatus = options.Format == ExportFormat.Pdf ? "NotApplicable" : result.HtmlSuccess ? "Success" : "Failure";
                    row.PdfStatus = options.Format == ExportFormat.Html ? "NotApplicable" : result.PdfSuccess ? "Success" : "Failure";
                    row.State = result.Error is not null ? "Failure" : result.Warnings.Count > 0 ? "Warning" : "Success";
                    row.Detail = result.Error ?? string.Join("\n", result.Warnings);
                    Status.Text = row.Detail.Length > 0 ? $"{row.Name}: {row.Detail}" : $"{row.Name}: {LanguageManager.Text("Complete")}";
                },
                (path, state) =>
                {
                    var row = FindOrAddRow(path);
                    row.State = state;
                    Status.Text = $"{row.Name}: {LanguageManager.Text(state)}";
                });
            lastSummary = (results.Count(x => x.Success), results.Count(x => !x.Success));
            Status.Text = LanguageManager.Format("Summary", lastSummary.Value.Success, lastSummary.Value.Failure);
            if (results.Any(x => !x.Success))
                AppDialog.ShowMessage(this, string.Join("\n", results.Where(x => !x.Success).Select(x => Path.GetFileName(x.Path) + ": " + x.Error)), LanguageManager.Text("DialogConversionError"));
        }
        catch (Exception ex) { Status.Text = ex.Message; AppDialog.ShowMessage(this, ex.Message, LanguageManager.Text("DialogConversionError")); }
        finally
        {
            isConverting = false;
            FileActions.IsEnabled = true;
            ConvertButton.IsEnabled = true;
        }
    }

    private FileRow FindOrAddRow(string path)
    {
        var row = Files.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
            return row;
        row = new FileRow(path, true);
        Files.Add(row);
        UpdateFileCount();
        return row;
    }

    private void WarningDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: FileRow { IsWarning: true } row })
            return;
        AppDialog.ShowMessage(this, $"{row.Name}{Environment.NewLine}{Environment.NewLine}{row.Detail}",
            LanguageManager.Text("WarningDetailsTitle"), AppDialogKind.Warning);
    }
}

public sealed class FileRow : INotifyPropertyChanged
{
    private string htmlStatusKey = "Waiting";
    private string pdfStatusKey = "Waiting";
    private string stateKey = "Waiting";
    private string detail = "";
    private bool isLinked;
    private EncodingOption selectedEncodingOption;

    public FileRow(string path, bool isLinked = false)
    {
        Path = path;
        IsLinked = isLinked;
        selectedEncodingOption = EncodingOptions[0];
    }

    public string Path { get; }
    public bool IsLinked
    {
        get => isLinked;
        set
        {
            isLinked = value;
            Notify();
            Notify(nameof(CanEditEncoding));
            Notify(nameof(ToolTipText));
        }
    }
    public bool CanEditEncoding => !IsLinked;
    public string Name => System.IO.Path.GetFileName(Path);
    private string OriginText => IsLinked ? Environment.NewLine + LanguageManager.Text("LinkedMarkdownRow") : "";
    public string ToolTipText => string.IsNullOrEmpty(detail)
        ? $"{Name}{Environment.NewLine}{Path}{OriginText}"
        : $"{Name}{Environment.NewLine}{Path}{OriginText}{Environment.NewLine}{Environment.NewLine}{detail}";
    public EncodingOption[] EncodingOptions { get; } =
    [
        new(InputEncoding.Auto, "EncodingAuto"),
        new(InputEncoding.Utf8, "UTF-8"),
        new(InputEncoding.ShiftJis, "Shift-JIS"),
        new(InputEncoding.EucJp, "EUC-JP")
    ];
    public EncodingOption SelectedEncodingOption
    {
        get => selectedEncodingOption;
        set
        {
            if (value is null)
                return;
            selectedEncodingOption = value;
            Notify();
        }
    }
    public InputEncoding SelectedEncoding => SelectedEncodingOption.Value;
    public string HtmlStatus
    {
        get => LanguageManager.Text(htmlStatusKey);
        set { htmlStatusKey = value; Notify(); }
    }
    public string PdfStatus
    {
        get => LanguageManager.Text(pdfStatusKey);
        set { pdfStatusKey = value; Notify(); }
    }
    public string State
    {
        get => LanguageManager.Text(stateKey);
        set { stateKey = value; Notify(); Notify(nameof(IsWarning)); }
    }
    public bool IsWarning => stateKey == "Warning";
    public string Detail
    {
        get => detail;
        set { detail = value; Notify(); Notify(nameof(ToolTipText)); }
    }

    public void RefreshLanguage()
    {
        foreach (var option in EncodingOptions)
            option.RefreshLanguage();
        Notify(nameof(HtmlStatus));
        Notify(nameof(PdfStatus));
        Notify(nameof(State));
        Notify(nameof(ToolTipText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class EncodingOption(InputEncoding value, string label) : INotifyPropertyChanged
{
    public InputEncoding Value { get; } = value;
    public string Name => Value == InputEncoding.Auto ? LanguageManager.Text(label) : label;

    public void RefreshLanguage() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
    public event PropertyChangedEventHandler? PropertyChanged;
}
