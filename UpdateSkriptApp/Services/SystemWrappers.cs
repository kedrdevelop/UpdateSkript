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
