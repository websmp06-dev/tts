using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using TtsVirtualMic.Models;
using TtsVirtualMic.Services.Engines;

namespace TtsVirtualMic.Services;

/// <summary>Orquesta: dividir en frases -> sintetizar -> enviar a CABLE Input (y al monitor).</summary>
public sealed class SpeechController
{
    readonly AudioService _audio;
    readonly EngineHub _hub;
    readonly AppSettings _s;
    CancellationTokenSource? _cts;

    public event Action<bool>? TransmittingChanged;
    public event Action<float>? Level;
    public event Action<string>? Error;

    public SpeechController(AudioService audio, EngineHub hub, AppSettings settings)
    {
        _audio = audio; _hub = hub; _s = settings;
    }

    public static List<string> SplitPhrases(string text) =>
        Regex.Split(text.Replace("\r", ""), @"(?<=[\.\!\?…])\s+|\n")
            .Select(p => p.Trim()).Where(p => p.Length > 0).ToList();

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
    }

    /// <param name="toCable">true: transmite por el micrófono virtual. false: solo prueba local (monitor).</param>
    public async Task SpeakAsync(string text, VoiceInfo voice, bool toCable)
    {
        Stop();
        var phrases = SplitPhrases(text);
        if (phrases.Count == 0) return;

        var cts = new CancellationTokenSource();
        _cts = cts;
        var ct = cts.Token;
        MMDevice? cable = null, monitor = null;
        try
        {
            if (toCable)
            {
                cable = _audio.FindCableInput();
                if (cable == null)
                {
                    Error?.Invoke("No se encontró el dispositivo \"CABLE Input\". Instalá VB-CABLE (y reiniciá Windows) o abrí el Diagnóstico.");
                    return;
                }
            }
            if (!toCable || _s.Monitor)
            {
                monitor = _audio.ResolveMonitor(_s.MonitorDeviceId);
                // Si el predeterminado de Windows es el propio cable, el monitor duplicaría la señal.
                if (monitor != null && cable != null && monitor.ID == cable.ID) monitor = null;
                if (!toCable && monitor == null)
                {
                    Error?.Invoke("No hay ningún dispositivo de salida disponible para la prueba.");
                    return;
                }
            }

            TransmittingChanged?.Invoke(true);
            var engine = _hub.Get(voice);
            Task<byte[]> Synth(string p) => engine.SynthesizeAsync(voice, p, _s.Speed, ct);

            var next = Synth(phrases[0]);
            for (int i = 0; i < phrases.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var wav = await next;
                next = i + 1 < phrases.Count ? Synth(phrases[i + 1]) : Task.FromResult(Array.Empty<byte>());
                await PlayBoth(wav, cable, monitor, ct);
                if (i + 1 < phrases.Count && _s.PauseMs > 0) await Task.Delay(_s.PauseMs, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Error?.Invoke("Error de audio: " + ex.Message);
        }
        finally
        {
            cable?.Dispose();
            monitor?.Dispose();
            if (ReferenceEquals(_cts, cts))
            {
                _cts = null;
                TransmittingChanged?.Invoke(false);
            }
            cts.Dispose();
        }
    }

    async Task PlayBoth(byte[] wav, MMDevice? cable, MMDevice? monitor, CancellationToken ct)
    {
        var tasks = new List<Task>();
        bool first = true;
        foreach (var dev in new[] { cable, monitor })
        {
            if (dev == null) continue;
            bool report = first; first = false;
            double pitch = _s.Pitch, vol = _s.Volume;
            tasks.Add(AudioService.PlayProviderAsync(dev,
                fmt => AudioService.BuildChain(wav, pitch, vol, fmt, report ? v => Level?.Invoke(v) : null), ct));
        }
        await Task.WhenAll(tasks);
    }
}
