using System;
using System.IO;

namespace TtsVirtualMic.Services;

public static class AppPaths
{
    public static string DataDir
    {
        get
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TtsVirtualMic");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public static string ToolsDir => Path.Combine(AppContext.BaseDirectory, "tools");
}
