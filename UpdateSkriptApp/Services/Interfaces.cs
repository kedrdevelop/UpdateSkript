using System;
using System.IO;
using System.Threading.Tasks;

namespace UpdateSkriptApp.Services;

public interface IFileSystem
{
    bool FileExists(string path);
    void DeleteFile(string path);
    void WriteAllText(string path, string content);
    string ReadAllText(string path);
    string[] ReadAllLines(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteDirectory(string path, bool recursive);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    Stream OpenRead(string path);
    long GetFileLength(string path);
}

public interface IRegistryWrapper
{
    (int Build, string Version) GetCurrentOsVersion();
}

public interface IAppEnvironment
{
    string GetEnvironmentVariable(string variable);
    string BaseDirectory { get; }
    string TempDirectory { get; }
}

public interface IPowerShellRunner
{
    Task<(int ExitCode, string Output)> ExecuteScriptAsync(string scriptContent, bool hidden = true, Action<string> onOutputLine = null);
}

public interface ILogger
{
    void LogInfo(string message);
    void LogError(string message);
}

public interface IRegistryService
{
    bool IsPhaseCompleted(string phase);
    void MarkPhaseCompleted(string phase);
    void ResetAllFlags();
    (int Build, string Version) GetCurrentOsVersion();
}

public interface ISetupCompleteBuilder
{
    void InjectSetupCompleteCmd();
}

public interface IProgressDownloader
{
    Task<bool> DownloadFileAsync(string url, string destination, string label, int maxRetries = 3);
}

public interface ILogWatcher
{
    Task MonitorSetupLogAsync(string logPath, Func<bool> isProcessActive);
    bool CheckIfUpgradeSucceeded(string logPath);
}
