using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TtsVirtualMic.Services;

namespace TtsVirtualMic.Models;

public sealed class AppSettings
{
    public string VoiceId { get; set; } = "";
    public double Speed { get; set; } = 1.0;      // 0.5 - 2.0
    public double Pitch { get; set; } = 0;        // semitonos -12 .. +12
    public double Volume { get; set; } = 90;      // 0 - 100
    public int PauseMs { get; set; } = 400;       // pausa entre frases
    public bool Monitor { get; set; } = true;     // escucharse por los auriculares
    public string MonitorDeviceId { get; set; } = "";
    public bool MinimizeToTray { get; set; } = true;
    public List<string> History { get; set; } = new();

    static string FilePath => Path.Combine(AppPaths.DataDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* archivo corrupto: se parte de cero */ }
        return new AppSettings();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }
}
