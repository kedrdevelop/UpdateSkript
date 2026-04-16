using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace UpdateSkriptApp.Services;

public class PowerShellHost : IPowerShellRunner
{
    private readonly IFileSystem _fileSystem;
    private readonly IAppEnvironment _env;
    private readonly ILogger _logger;

    public PowerShellHost(IFileSystem fileSystem, IAppEnvironment env, ILogger logger)
    {
        _fileSystem = fileSystem;
        _env = env;
        _logger = logger;
    }

    public async Task<(int ExitCode, string Output)> ExecuteScriptAsync(string scriptContent, bool hidden = true, Action<string> onOutputLine = null)
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
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.Start();

        var outTask = ReadStreamAsync(process.StandardOutput, line =>
        {
            _logger.LogInfo(line);
            onOutputLine?.Invoke(line);
            lock (outputBuilder) outputBuilder.AppendLine(line);
        });

        var errTask = ReadStreamAsync(process.StandardError, line =>
        {
            _logger.LogError(line);
            onOutputLine?.Invoke(line);
            lock (errorBuilder) errorBuilder.AppendLine(line);
        });

        await Task.WhenAll(outTask, errTask, process.WaitForExitAsync());

        _fileSystem.DeleteFile(tmpFile);

        string finalOutput = string.Empty;
        if (outputBuilder.Length > 0) finalOutput += outputBuilder.ToString() + "\n";
        if (errorBuilder.Length > 0) finalOutput += errorBuilder.ToString();

        return (process.ExitCode, finalOutput);
    }

    private async Task ReadStreamAsync(StreamReader reader, Action<string> onLine)
    {
        string line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                onLine(line);
            }
        }
    }
}
