using System.Text.Json;
using System.Text.Json.Serialization;
using QuickTools.Model;

namespace QuickTools.Services;

internal sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickTools", "settings.json");

    public string FilePath => filePath;

    /// <summary>Set by <see cref="Load"/> when the file was unreadable and defaults were used instead.</summary>
    public string? LastLoadError { get; private set; }

    public AppSettings Load()
    {
        LastLoadError = null;

        if (!File.Exists(filePath))
        {
            return SaveDefaults();
        }

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file may be fine, just locked or inaccessible right now, so never overwrite it.
            LastLoadError = $"Couldn't read {filePath}, so default menus are used for this session. ({ex.Message})";
            return AppSettings.CreateDefault();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json, s_options)
                ?? throw new JsonException("The settings file is empty.");
            Normalize(settings);
            return settings;
        }
        catch (JsonException ex)
        {
            // Keep the broken file for the user to inspect, and always start.
            string backupPath = filePath + ".bad";
            try
            {
                File.Move(filePath, backupPath, overwrite: true);
            }
            catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
            {
                LastLoadError = $"{filePath} is invalid and couldn't be backed up, so default menus are used for this session. ({ex.Message})";
                return AppSettings.CreateDefault();
            }

            LastLoadError = $"Settings were unreadable and have been reset. The old file was saved as {backupPath}. ({ex.Message})";
            return SaveDefaults();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        // Write to a temp file and swap it in, so a crash mid-write can't corrupt the settings.
        string tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, s_options));

        if (File.Exists(filePath))
        {
            File.Replace(tempPath, filePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, filePath);
        }
    }

    private AppSettings SaveDefaults()
    {
        var settings = AppSettings.CreateDefault();
        try
        {
            Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Running with defaults is fine; the next successful Save will create the file.
        }

        return settings;
    }

    // Explicit JSON nulls override property initializers, so repair them here.
    private static void Normalize(AppSettings settings)
    {
        settings.LeftMenu ??= [];
        settings.RightMenu ??= [];
        Normalize(settings.LeftMenu);
        Normalize(settings.RightMenu);
    }

    private static void Normalize(List<MenuEntry> entries)
    {
        entries.RemoveAll(entry => entry is null);
        foreach (var entry in entries)
        {
            entry.Name ??= string.Empty;
            if (entry.Kind == EntryKind.Folder)
            {
                entry.Children ??= [];
                Normalize(entry.Children);
            }
            else
            {
                entry.Children = null;
            }
        }
    }
}
