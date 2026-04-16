using System;
using System.IO;
using Microsoft.Win32;

namespace UpdateSkriptApp.Services;

public static class RegistryService
{
    private static readonly string PublicDesktop = Environment.GetEnvironmentVariable("PUBLIC") 
        ?? @"C:\Users\Public";

    private static string GetFlagPath(string flagName) => Path.Combine(PublicDesktop, flagName);

    public static bool IsPhaseCompleted(string phase)
    {
        return File.Exists(GetFlagPath($"UpdateSkript_{phase}.flag"));
    }

    public static void MarkPhaseCompleted(string phase)
    {
        var path = GetFlagPath($"UpdateSkript_{phase}.flag");
        File.WriteAllText(path, DateTime.Now.ToString("O"));
    }

    public static void ResetAllFlags()
    {
        var flags = new[] { "WU", "Dell", "Win11" };
        foreach (var flag in flags)
        {
            var path = GetFlagPath($"UpdateSkript_{flag}.flag");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public static (int Build, string Version) GetCurrentOsVersion()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (key == null) return (0, "Unknown");

        var buildStr = key.GetValue("CurrentBuild")?.ToString() ?? "0";
        var displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? "Unknown";

        int.TryParse(buildStr, out int build);
        return (build, displayVersion);
    }
}
