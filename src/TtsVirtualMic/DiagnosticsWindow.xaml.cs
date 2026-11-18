using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TtsVirtualMic.Services;

namespace TtsVirtualMic;

public partial class DiagnosticsWindow : Window
{
    readonly AudioService _audio;

    public DiagnosticsWindow(AudioService audio)
    {
        InitializeComponent();
        _audio = audio;
        Loaded += async (_, _) => await RunAsync();
    }

    async void Run_Click(object sender, RoutedEventArgs e) => await RunAsync();

    static void Sound_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("control.exe", "mmsys.cpl") { UseShellExecute = true });

    static void Privacy_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("ms-settings:privacy-microphone") { UseShellExecute = true });

    async Task RunAsync()
    {
        RunBtn.IsEnabled = false;
        var sb = new StringBuilder();
        void Line(string s) { sb.AppendLine(s); Report.Text = sb.ToString(); }

        try
        {
            using var inDev = _audio.FindCableInput();
            using var outDev = _audio.FindCableOutput();

            Line(inDev != null ? "✔  CABLE Input (salida) detectado: " + inDev.FriendlyName
                               : "✘  No se encontró CABLE Input.");
            Line(outDev != null ? "✔  CABLE Output (micrófono) detectado: " + outDev.FriendlyName
                                : "✘  No se encontró CABLE Output.");

            if (inDev == null || outDev == null)
            {
                Line("");
                Line("VB-CABLE no está instalado o Windows aún no lo cargó.");
                Line("• Instalalo (el instalador de esta app puede hacerlo) y REINICIÁ Windows.");
                Line("• Si ya lo instalaste, reiniciá y volvé a ejecutar el diagnóstico.");
                return;
            }

            using (var def = _audio.DefaultRender())
            {
                if (def != null && def.ID == inDev.ID)
                    Line("⚠  CABLE Input es tu salida predeterminada de Windows: no vas a oír el sonido del sistema. Cambiá la salida predeterminada a tus auriculares/parlantes.");
            }

            Line("");
            Line("Enviando tono de prueba por CABLE Input...");
            float peak = 0;
            using var cap = new WasapiCapture(outDev);
            cap.DataAvailable += (_, e) => peak = Math.Max(peak, Peak(cap.WaveFormat, e.Buffer, e.BytesRecorded));
            cap.StartRecording();
            await Task.Delay(400);
            await AudioService.PlayProviderAsync(inDev, Tone, CancellationToken.None);
            await Task.Delay(400);
            cap.StopRecording();

            if (peak > 0.02f)
            {
                Line($"✔  El audio llega a CABLE Output (nivel {peak * 100:0}%). El micrófono virtual funciona.");
                Line("");
                Line("Si en Discord o el juego no se oye, el problema está allí:");
                Line("• Micrófono de entrada = \"CABLE Output\" (NO \"CABLE Input\").");
                Line("• Discord: desactivá la supresión de ruido y el control automático de ganancia, y poné \"Detección de actividad de voz\" con sensibilidad automática desactivada o baja.");
                Line("• Verificá que Windows permita el acceso al micrófono a esa aplicación (botón Privacidad del micrófono).");
            }
            else
            {
                Line("✘  El tono salió, pero CABLE Output no recibió señal.");
                Line("");
                Line("Revisá:");
                Line("• Reiniciar Windows tras instalar VB-CABLE.");
                Line("• Opciones de sonido → Grabación → CABLE Output → Propiedades → que no esté deshabilitado ni silenciado.");
                Line("• Que otra aplicación no tenga CABLE Input en modo exclusivo.");
            }
        }
        catch (Exception ex)
        {
            Line("✘  Error durante el diagnóstico: " + ex.Message);
        }
        finally
        {
            RunBtn.IsEnabled = true;
        }
    }

    static ISampleProvider Tone(WaveFormat mix)
    {
        var gen = new SignalGenerator(mix.SampleRate, 1) { Frequency = 440, Gain = 0.4, Type = SignalGeneratorType.Sin };
        var take = new OffsetSampleProvider(gen) { Take = TimeSpan.FromSeconds(1) };
        return AudioService.ToDeviceFormat(take, mix);
    }

    static float Peak(WaveFormat f, byte[] buf, int n)
    {
        float p = 0;
        if (f.BitsPerSample == 32)
        {
            for (int i = 0; i + 4 <= n; i += 4) p = Math.Max(p, Math.Abs(BitConverter.ToSingle(buf, i)));
        }
        else if (f.BitsPerSample == 16)
        {
            for (int i = 0; i + 2 <= n; i += 2) p = Math.Max(p, Math.Abs(BitConverter.ToInt16(buf, i)) / 32768f);
        }
        return p;
    }
}
