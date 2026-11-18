using System;
using System.IO;
using System.Windows;

namespace TtsVirtualMic;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) =>
        {
            try
            {
                var dir = Services.AppPaths.DataDir;
                File.AppendAllText(Path.Combine(dir, "error.log"), $"[{DateTime.Now:s}] {ex.Exception}\n\n");
            }
            catch { }
            MessageBox.Show("Ocurrió un error inesperado:\n\n" + ex.Exception.Message, "TTS Virtual Mic",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
    }
}
