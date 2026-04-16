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

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tmpFile}\"",
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

            // Set a global timeout to prevent infinite hangs (e.g. 2 hours limit)
            var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromHours(2));
            try
            {
                await Task.WhenAll(outTask, errTask, process.WaitForExitAsync(timeoutCts.Token));
            }
            catch (TaskCanceledException)
            {
                process.Kill();
                _logger.LogError("Process killed due to 2 hour timeout.");
                errorBuilder.AppendLine("ERROR: Timeout reached (120 minutes)");
            }

            string finalOutput = string.Empty;
            if (outputBuilder.Length > 0) finalOutput += outputBuilder.ToString() + "\n";
            if (errorBuilder.Length > 0) finalOutput += errorBuilder.ToString();

            return (process.HasExited ? process.ExitCode : -1, finalOutput);
        }
        catch (Exception ex)
        {
            _logger.LogError($"PowerShell execution failed: {ex.Message}");
            return (-1, ex.ToString());
        }
        finally
        {
            if (_fileSystem.FileExists(tmpFile))
                _fileSystem.DeleteFile(tmpFile);
        }
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
