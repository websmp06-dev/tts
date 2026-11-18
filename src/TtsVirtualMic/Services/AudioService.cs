using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TtsVirtualMic.Services;

/// <summary>
/// Todo lo relacionado con dispositivos WASAPI: localizar VB-CABLE, reproducir hacia un dispositivo concreto
/// (nunca el predeterminado) y construir la cadena DSP (tono, volumen, remuestreo, medidor).
/// </summary>
public sealed class AudioService : IDisposable
{
    readonly MMDeviceEnumerator _enum = new();

    /// <summary>"CABLE Input": dispositivo de reproducción donde escribimos el TTS.</summary>
    public MMDevice? FindCableInput() => Find(DataFlow.Render, "CABLE Input");

    /// <summary>"CABLE Output": el micrófono que se elige en Discord / juegos.</summary>
    public MMDevice? FindCableOutput() => Find(DataFlow.Capture, "CABLE Output");

    MMDevice? Find(DataFlow flow, string name)
    {
        try
        {
            return _enum.EnumerateAudioEndPoints(flow, DeviceState.Active)
                .FirstOrDefault(d => d.FriendlyName.Contains(name, StringComparison.OrdinalIgnoreCase));
        }
        catch { return null; }
    }

    public List<(string Id, string Name)> ListRenderDevices()
    {
        try
        {
            return _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(d => (d.ID, d.FriendlyName)).ToList();
        }
        catch { return new(); }
    }

    /// <summary>Dispositivo de monitor (auriculares). Si no hay uno elegido o desapareció, usa el predeterminado.</summary>
    public MMDevice? ResolveMonitor(string id)
    {
        try
        {
            if (!string.IsNullOrEmpty(id))
            {
                var d = _enum.GetDevice(id);
                if (d.State == DeviceState.Active) return d;
            }
        }
        catch { }
        return DefaultRender();
    }

    public MMDevice? DefaultRender()
    {
        try { return _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); }
        catch { return null; }
    }

    /// <summary>Convierte cualquier proveedor mono al formato de mezcla del dispositivo (frecuencia y canales).</summary>
    public static ISampleProvider ToDeviceFormat(ISampleProvider sp, WaveFormat mix)
    {
        if (sp.WaveFormat.Channels == 2) sp = new StereoToMonoSampleProvider(sp);
        if (sp.WaveFormat.SampleRate != mix.SampleRate) sp = new WdlResamplingSampleProvider(sp, mix.SampleRate);
        if (mix.Channels > 1)
        {
            var mux = new MultiplexingSampleProvider(new[] { sp }, mix.Channels);
            mux.ConnectInputToOutput(0, 0);
            mux.ConnectInputToOutput(0, 1);
            sp = mux;
        }
        return sp;
    }

    public static ISampleProvider BuildChain(byte[] wav, double pitchSemitones, double volumePercent,
        WaveFormat mix, Action<float>? onLevel)
    {
        var reader = new WaveFileReader(new MemoryStream(wav));
        ISampleProvider sp = reader.ToSampleProvider();
        if (sp.WaveFormat.Channels == 2) sp = new StereoToMonoSampleProvider(sp);
        if (Math.Abs(pitchSemitones) > 0.01)
            sp = new SmbPitchShiftingSampleProvider(sp, 4096, 4, (float)Math.Pow(2, pitchSemitones / 12.0));
        sp = new OffsetSampleProvider(sp) { LeadOut = TimeSpan.FromMilliseconds(250) };
        sp = new VolumeSampleProvider(sp) { Volume = (float)(Math.Clamp(volumePercent, 0, 100) / 100.0) };
        sp = ToDeviceFormat(sp, mix);
        var meter = new MeteringSampleProvider(sp);
        if (onLevel != null)
            meter.StreamVolume += (_, e) => onLevel(e.MaxSampleValues.Length == 0 ? 0 : e.MaxSampleValues.Max());
        return meter;
    }

    /// <summary>Reproduce en un dispositivo concreto (modo compartido) y termina cuando acaba o se cancela.</summary>
    public static async Task PlayProviderAsync(MMDevice dev, Func<WaveFormat, ISampleProvider> build, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;
        var mix = dev.AudioClient.MixFormat;
        var chain = build(mix);
        using var wo = new WasapiOut(dev, AudioClientShareMode.Shared, true, 100);
        var tcs = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        wo.PlaybackStopped += (_, e) => tcs.TrySetResult(e.Exception);
        wo.Init(chain.ToWaveProvider());
        wo.Play();
        using var reg = ct.Register(() => { try { wo.Stop(); } catch { } });
        var ex = await tcs.Task.ConfigureAwait(false);
        if (ex != null) throw ex;
    }

    public void Dispose() => _enum.Dispose();
}
