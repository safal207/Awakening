using System;
using System.IO;
using System.Text.Json;

namespace Probuzhdenie;

public sealed class GameSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Probuzhdenie");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public bool Fullscreen { get; set; }
    public bool VSync { get; set; } = true;
    public bool Shadows { get; set; } = true;
    public float MouseSensitivity { get; set; } = 1f;

    public static GameSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new GameSettings();

            string json = File.ReadAllText(SettingsPath);
            return Normalize(JsonSerializer.Deserialize<GameSettings>(json) ?? new GameSettings());
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load settings: {e.Message}");
            return new GameSettings();
        }
    }

    public void Save()
    {
        try
        {
            Normalize(this);
            Directory.CreateDirectory(SettingsDirectory);
            string temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(temporaryPath, SettingsPath, true);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to save settings: {e.Message}");
        }
    }

    public static GameSettings Normalize(GameSettings settings)
    {
        settings.Width = Math.Clamp(settings.Width, 960, 3840);
        settings.Height = Math.Clamp(settings.Height, 540, 2160);
        settings.MouseSensitivity = Math.Clamp(settings.MouseSensitivity, 0.5f, 2f);
        return settings;
    }
}
