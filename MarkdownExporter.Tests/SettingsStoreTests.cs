using MarkdownExporter.Conversion;
using MarkdownExporter.Localization;
using MarkdownExporter.Settings;
using MarkdownExporter.Themes;

namespace MarkdownExporter.Tests;

/// <summary>既定の変換設定の保存と変更検出を確認する。</summary>
[TestFixture]
public sealed class SettingsStoreTests
{
    [Test]
    public void Load_MissingOrMalformedFile_UsesDefaults()
    {
        using var folder = new TestFolder();
        var path = folder.PathFor("settings.json");

        Assert.That(SettingsStore.Load(path).Defaults.Format, Is.EqualTo(ExportFormat.Both));

        folder.Write("settings.json", "{invalid json");
        Assert.That(SettingsStore.Load(path).Defaults.Format, Is.EqualTo(ExportFormat.Both));
    }

    [Test]
    public void Load_NullDefaults_RepairsSettings()
    {
        using var folder = new TestFolder();
        var path = folder.Write("settings.json", "{\"Defaults\":null}");

        Assert.That(SettingsStore.Load(path).Defaults, Is.Not.Null);
    }

    /// <summary>図と見出しの配置設定を含む既定値を保存して読み戻す。</summary>
    [Test]
    public void Save_ThenLoad_RoundTripsAndReplacesExistingFile()
    {
        using var folder = new TestFolder();
        var path = folder.PathFor("nested/settings.json");
        var settings = new AppSettings
        {
            Theme = AppThemePreference.Dark,
            Language = LanguagePreference.English,
            Defaults = new ConversionOptions
            {
                Format = ExportFormat.Pdf,
                Template = "custom",
                KeepHeadingWithFigure = true,
                FollowMarkdownLinks = true,
                Margins = new PdfMargins { Top = 5m, Bottom = 10m, Left = 15m, Right = 20m }
            }
        };

        SettingsStore.Save(settings, path);
        settings.Defaults.Template = "changed";
        SettingsStore.Save(settings, path);
        var loaded = SettingsStore.Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Theme, Is.EqualTo(AppThemePreference.Dark));
            Assert.That(loaded.Language, Is.EqualTo(LanguagePreference.English));
            Assert.That(loaded.Defaults.Format, Is.EqualTo(ExportFormat.Pdf));
            Assert.That(loaded.Defaults.Template, Is.EqualTo("changed"));
            Assert.That(loaded.Defaults.KeepHeadingWithFigure, Is.True);
            Assert.That(loaded.Defaults.FollowMarkdownLinks, Is.True);
            Assert.That(loaded.Defaults.Margins, Is.EqualTo(new PdfMargins
            { Top = 5m, Bottom = 10m, Left = 15m, Right = 20m }));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        });
    }

    /// <summary>既定の見出し配置だけを変えても今回の変換へ反映する確認対象に含める。</summary>
    [Test]
    public void ConversionDefaultsChanges_KeepHeadingOnly_RequiresCurrentSettingsPrompt()
    {
        var changes = new ConversionDefaultsChanges(new ConversionOptions(),
            new ConversionOptions { KeepHeadingWithFigure = true });

        Assert.Multiple(() =>
        {
            Assert.That(changes.HasChanges, Is.True);
            Assert.That(changes.KeepHeadingWithFigureChanged, Is.True);
            Assert.That(changes.Saved.KeepHeadingWithFigure, Is.True);
        });
    }

    /// <summary>古い設定に余白がなくても各辺15mmを適用する。</summary>
    [Test]
    public void Load_OlderSettingsWithoutMargins_Uses15MmDefaults()
    {
        using var folder = new TestFolder();
        var path = folder.Write("settings.json", "{\"Defaults\":{\"Format\":1}}");

        Assert.That(SettingsStore.Load(path).Defaults.Margins, Is.EqualTo(new PdfMargins()));
    }

    /// <summary>保存された余白がnullの場合に既定値へ復元する。</summary>
    [Test]
    public void Load_NullMargins_RepairsSettings()
    {
        using var folder = new TestFolder();
        var path = folder.Write("settings.json", "{\"Defaults\":{\"Margins\":null}}");

        Assert.That(SettingsStore.Load(path).Defaults.Margins, Is.EqualTo(new PdfMargins()));
    }

    /// <summary>余白だけを変更した場合も今回の変換への反映確認に含める。</summary>
    [Test]
    public void ConversionDefaultsChanges_MarginsOnly_RequiresCurrentSettingsPrompt()
    {
        var changes = new ConversionDefaultsChanges(new ConversionOptions(),
            new ConversionOptions { Margins = new PdfMargins { Top = 12m } });

        Assert.Multiple(() =>
        {
            Assert.That(changes.HasChanges, Is.True);
            Assert.That(changes.MarginsChanged, Is.True);
            Assert.That(changes.Saved.Margins.Top, Is.EqualTo(12m));
        });
    }

    [Test]
    public void ConversionDefaultsChanges_FollowLinksOnly_RequiresCurrentSettingsPrompt()
    {
        var changes = new ConversionDefaultsChanges(new ConversionOptions(),
            new ConversionOptions { FollowMarkdownLinks = true });

        Assert.Multiple(() =>
        {
            Assert.That(changes.HasChanges, Is.True);
            Assert.That(changes.FollowMarkdownLinksChanged, Is.True);
            Assert.That(changes.Saved.FollowMarkdownLinks, Is.True);
        });
    }
}
