using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TtsVirtualMic.Models;

namespace TtsVirtualMic.Services.Engines;

public interface ITtsEngine
{
    string Key { get; }
    IEnumerable<VoiceInfo> GetVoices();
    /// <summary>Devuelve un WAV PCM completo (con cabecera) de la frase.</summary>
    Task<byte[]> SynthesizeAsync(VoiceInfo voice, string text, double speed, CancellationToken ct);
}
