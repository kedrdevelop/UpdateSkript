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
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteDirectory(string path, bool recursive);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
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
    Task<(int ExitCode, string Output)> ExecuteScriptAsync(string scriptContent, bool hidden = true);
}
