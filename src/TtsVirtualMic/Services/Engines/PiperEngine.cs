using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TtsVirtualMic.Models;

namespace TtsVirtualMic.Services.Engines;

/// <summary>Voces neuronales locales con Piper (tools\piper\piper.exe + tools\piper\voices\*.onnx).</summary>
public sealed class PiperEngine : ITtsEngine
{
    static readonly string Dir = Path.Combine(AppPaths.ToolsDir, "piper");
    static string Exe => Path.Combine(Dir, "piper.exe");

    static readonly Dictionary<string, string> Friendly = new()
    {
        ["es_ES-davefx-medium"] = "Dave (España)",
        ["es_MX-ald-medium"] = "Ald (México)",
    };

    public string Key => "piper";

    public IEnumerable<VoiceInfo> GetVoices()
    {
        var vdir = Path.Combine(Dir, "voices");
        if (!File.Exists(Exe) || !Directory.Exists(vdir)) yield break;
        foreach (var f in Directory.GetFiles(vdir, "*.onnx"))
        {
            var stem = Path.GetFileNameWithoutExtension(f);
            yield return new VoiceInfo
            {
                Id = "piper:" + stem,
                Name = Friendly.TryGetValue(stem, out var n) ? n : stem,
                Category = "Neuronal (Piper)",
                Engine = Key,
                Param = f,
            };
        }
    }

    public async Task<byte[]> SynthesizeAsync(VoiceInfo voice, string text, double speed, CancellationToken ct)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"ttsvm_{Guid.NewGuid():N}.wav");
        try
        {
            var args = new[]
            {
                "--model", voice.Param,
                "--output_file", tmp,
                "--length_scale", (1.0 / speed).ToString("0.00", CultureInfo.InvariantCulture),
            };
            await ProcessRunner.RunAsync(Exe, Dir, args, text.Replace('\n', ' ') + "\n", ct);
            return await File.ReadAllBytesAsync(tmp, ct);
        }
        finally { try { File.Delete(tmp); } catch { } }
    }
}
