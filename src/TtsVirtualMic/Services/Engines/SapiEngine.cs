using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using TtsVirtualMic.Models;
using VoiceInfo = TtsVirtualMic.Models.VoiceInfo;

namespace TtsVirtualMic.Services.Engines;

/// <summary>Voces SAPI5 instaladas en Windows (no requiere nada extra).</summary>
public sealed class SapiEngine : ITtsEngine
{
    public string Key => "sapi";

    public IEnumerable<VoiceInfo> GetVoices()
    {
        using var s = new SpeechSynthesizer();
        return s.GetInstalledVoices().Where(v => v.Enabled).Select(v => new VoiceInfo
        {
            Id = "sapi:" + v.VoiceInfo.Name,
            Name = v.VoiceInfo.Name,
            Category = "Voz de Windows",
            Engine = Key,
            Param = v.VoiceInfo.Name,
        }).ToList();
    }

    public Task<byte[]> SynthesizeAsync(VoiceInfo voice, string text, double speed, CancellationToken ct) =>
        Task.Run(() =>
        {
            using var ms = new MemoryStream();
            using (var s = new SpeechSynthesizer())
            {
                s.SelectVoice(voice.Param);
                s.Rate = Math.Clamp((int)Math.Round(Math.Log2(speed) * 10), -10, 10);
                s.SetOutputToWaveStream(ms);
                s.Speak(text);
                s.SetOutputToNull();
            }
            return ms.ToArray();
        }, ct);
}
