using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace UpdateSkriptApp.Services;

public class PowerShellHost : IPowerShellRunner
{
    private readonly IFileSystem _fileSystem;
    private readonly IAppEnvironment _env;

    public PowerShellHost(IFileSystem fileSystem, IAppEnvironment env)
    {
        _fileSystem = fileSystem;
        _env = env;
    }

    public async Task<(int ExitCode, string Output)> ExecuteScriptAsync(string scriptContent, bool hidden = true)
    {
        string tmpFile = Path.Combine(_env.TempDirectory, $"UpdateSkript_tmp_{Guid.NewGuid():N}.ps1");
        _fileSystem.WriteAllText(tmpFile, scriptContent);

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

        _fileSystem.DeleteFile(tmpFile);

        return (process.ExitCode, outputTask.Result + "\n" + errorTask.Result);
    }
}
