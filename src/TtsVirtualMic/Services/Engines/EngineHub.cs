using System.Collections.Generic;
using System.Linq;
using TtsVirtualMic.Models;

namespace TtsVirtualMic.Services.Engines;

public sealed class EngineHub
{
    readonly List<ITtsEngine> _engines = new() { new PiperEngine(), new EspeakEngine(), new SapiEngine() };

    public List<VoiceInfo> AllVoices()
    {
        var all = new List<VoiceInfo>();
        foreach (var e in _engines)
        {
            try { all.AddRange(e.GetVoices()); } catch { /* un motor roto no debe tumbar a los demás */ }
        }
        return all;
    }

    public ITtsEngine Get(VoiceInfo v) => _engines.First(e => e.Key == v.Engine);
}
