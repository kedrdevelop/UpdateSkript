using System;
using System.IO;
using Microsoft.Win32;

namespace UpdateSkriptApp.Services;

public class RealFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public void DeleteFile(string path) => File.Delete(path);
    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => Directory.GetFiles(path, searchPattern, searchOption);
    public Stream OpenRead(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    public long GetFileLength(string path) => new FileInfo(path).Length;
}

public class RealRegistryWrapper : IRegistryWrapper
{
    public (int Build, string Version) GetCurrentOsVersion()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (key == null) return (0, "Unknown");

        var buildStr = key.GetValue("CurrentBuild")?.ToString() ?? "0";
        var displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? "Unknown";

        int.TryParse(buildStr, out int build);
        return (build, displayVersion);
    }
}

public class RealAppEnvironment : IAppEnvironment
{
    public string GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);
    public string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;
    public string TempDirectory => Path.GetTempPath();
}

public class AppLogger : ILogger
{
    private readonly string _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServierDE_Deploy.log");
    private readonly object _lock = new object();

    public void LogInfo(string message) => WriteToFile($"[INFO ] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    public void LogError(string message) => WriteToFile($"[ERROR] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");

    private void WriteToFile(string line)
    {
        lock (_lock)
        {
            try
            {
                using var fs = new FileStream(_logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs);
                sw.WriteLine(line);
                sw.Flush();
            }
            catch
            {
                // Ignore log failures to prevent crash
            }
        }
    }
}
