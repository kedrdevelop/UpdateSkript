using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace UpdateSkriptApp.Services;

public static class PowerShellHost
{
    public static async Task<(int ExitCode, string Output)> ExecuteScriptAsync(string scriptContent, bool hidden = true)
    {
        string tmpFile = Path.Combine(Path.GetTempPath(), $"UpdateSkript_tmp_{Guid.NewGuid():N}.ps1");
        File.WriteAllText(tmpFile, scriptContent);

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tmpFile}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = hidden
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());

        File.Delete(tmpFile);

        return (process.ExitCode, outputTask.Result + "\n" + errorTask.Result);
    }
}
