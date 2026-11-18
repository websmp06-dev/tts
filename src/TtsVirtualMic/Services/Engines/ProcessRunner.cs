using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TtsVirtualMic.Services.Engines;

internal static class ProcessRunner
{
    /// <summary>Ejecuta un proceso oculto, le pasa texto UTF-8 por stdin y espera a que termine.</summary>
    public static async Task RunAsync(string exe, string workDir, string[] args, string stdin, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar " + exe);
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        await p.StandardInput.WriteAsync(stdin);
        p.StandardInput.Close();

        using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } });
        await p.WaitForExitAsync(CancellationToken.None);
        ct.ThrowIfCancellationRequested();
        await outTask;
        var err = await errTask;
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(exe)} terminó con código {p.ExitCode}: {Tail(err)}");
    }

    static string Tail(string s) => s.Length <= 300 ? s : s[^300..];
}
