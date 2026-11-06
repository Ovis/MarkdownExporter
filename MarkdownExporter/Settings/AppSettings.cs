using System.Text.Json;
using MarkdownExporter.Conversion;
using MarkdownExporter.Localization;
using MarkdownExporter.Themes;

namespace MarkdownExporter.Settings;

public sealed class AppSettings
{
    public AppThemePreference Theme { get; set; } = AppThemePreference.System;
    public LanguagePreference Language { get; set; } = LanguagePreference.System;
    public ConversionOptions Defaults { get; set; } = new();
}

public static class SettingsStore
{
    private static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MarkdownExporter", "settings.json");

    public static AppSettings Load() => Load(PathName);

    internal static AppSettings Load(string path)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
            settings.Defaults ??= new ConversionOptions();
            settings.Defaults.Margins ??= new PdfMargins();
            return settings;
        }
        catch (FileNotFoundException) { return new(); }
        catch (DirectoryNotFoundException) { return new(); }
        catch (JsonException) { return new(); }
    }

    public static void Save(AppSettings settings) => Save(settings, PathName);

    internal static void Save(AppSettings settings, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
