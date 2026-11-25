using System.Text.Json;

namespace WowRgbBridge;

public static class Config
{
    public static AppConfig LoadConfig(string path)
    {
        // Read the JSON settings and reject invalid process, color, LED, or polling values.
        if (!File.Exists(path))
        {
            var examplePath = Path.Combine(AppContext.BaseDirectory, "config.example.json");
            throw new FileNotFoundException(
                $"Configuration file not found. Copy \"{examplePath}\" to \"{path}\" and fill in its values.",
                path);
        }

        var config = JsonSerializer.Deserialize<AppConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Configuration file \"{path}\" is empty or invalid.");

        if (string.IsNullOrWhiteSpace(config.ForegroundProcessName))
        {
            throw new InvalidDataException("foregroundProcessName must contain a process name.");
        }

        if (config.SignatureColors is null || config.SignatureColors.Count != 4)
        {
            throw new InvalidDataException("signatureColors must contain red, green, blue, and black in that order.");
        }

        if (config.UpdateFrequencyMs <= 0)
        {
            throw new InvalidDataException("updateFrequencyMs must be greater than zero.");
        }

        if (config.SignatureColorTolerance is < 0 or > 127)
        {
            throw new InvalidDataException("signatureColorTolerance must be between 0 and 127.");
        }

        return config;
    }
}

// Settings deserialized from config.json.
public sealed class AppConfig
{
    public string ForegroundProcessName { get; set; } = string.Empty;
    public string KeyboardName { get; set; } = string.Empty;
    public List<string> SignatureColors { get; set; } = [];
    public string FooterColor { get; set; } = string.Empty;
    public List<string> LedMappings { get; set; } = [];
    public int UpdateFrequencyMs { get; set; }
    public int SignatureColorTolerance { get; set; }
    public string? StatusLed { get; set; }
    public string defaultKeyColor { get; set; } = "#AA0000";
    public Dictionary<string,string> StaticLeds { get; set; } = [];
}