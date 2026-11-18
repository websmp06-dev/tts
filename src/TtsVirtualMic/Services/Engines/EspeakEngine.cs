using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TtsVirtualMic.Models;

namespace TtsVirtualMic.Services.Engines;

/// <summary>
/// eSpeak NG (tools\espeak\espeak-ng.exe + espeak-ng-data). Voces robóticas, graves, agudas y caricaturescas.
/// La tabla se puede ajustar a gusto: (id, nombre, voz+variante, tono base 0-99).
/// </summary>
public sealed class EspeakEngine : ITtsEngine
{
    static readonly string Dir = Path.Combine(AppPaths.ToolsDir, "espeak");
    static string Exe => Path.Combine(Dir, "espeak-ng.exe");

    static readonly (string Id, string Name, string Voice, int Pitch)[] Table =
    {
        ("robot",    "Robot clásico",        "es-419+m3",      35),
        ("sintetico","Sintetizador retro",   "es-419+klatt3",  50),
        ("metal",    "Robot metálico",       "es-419+m6",      20),
        ("grave",    "Masculina grave",      "es-419+m4",      15),
        ("grave2",   "Masculina muy grave",  "es-419+m7",       5),
        ("aguda",    "Masculina aguda",      "es-419+m2",      75),
        ("fem",      "Femenina",             "es-419+f2",      50),
        ("fem-aguda","Femenina aguda",       "es-419+f4",      70),
        ("rana",     "Rana (caricatura)",    "es-419+croak",   60),
        ("susurro",  "Susurro",              "es-419+whisper", 50),
        ("duende",   "Duende (caricatura)",  "es-419+m5",      95),
    };

    public string Key => "espeak";

    public IEnumerable<VoiceInfo> GetVoices()
    {
        if (!File.Exists(Exe) || !Directory.Exists(Path.Combine(Dir, "espeak-ng-data")))
            return Enumerable.Empty<VoiceInfo>();
        return Table.Select(t => new VoiceInfo
        {
            Id = "espeak:" + t.Id,
            Name = t.Name,
            Category = "Retro / caricatura (eSpeak)",
            Engine = Key,
            Param = t.Voice,
            BasePitch = t.Pitch,
        }).ToList();
    }

    public async Task<byte[]> SynthesizeAsync(VoiceInfo voice, string text, double speed, CancellationToken ct)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"ttsvm_{Guid.NewGuid():N}.wav");
        try
        {
            var args = new[]
            {
                $"--path={Dir}",
                "-v", voice.Param,
                "-s", Math.Clamp((int)(175 * speed), 80, 450).ToString(CultureInfo.InvariantCulture),
                "-p", Math.Clamp(voice.BasePitch, 0, 99).ToString(CultureInfo.InvariantCulture),
                "-w", tmp,
                "--stdin",
            };
            await ProcessRunner.RunAsync(Exe, Dir, args, text.Replace('\n', ' ') + "\n", ct);
            return await File.ReadAllBytesAsync(tmp, ct);
        }
        finally { try { File.Delete(tmp); } catch { } }
    }
}
