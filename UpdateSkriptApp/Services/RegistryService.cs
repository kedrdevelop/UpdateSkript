using System;
using System.IO;

namespace UpdateSkriptApp.Services;

public class RegistryService : IRegistryService
{
    private readonly IFileSystem _fileSystem;
    private readonly IAppEnvironment _env;
    private readonly IRegistryWrapper _registry;

    public RegistryService(IFileSystem fileSystem, IAppEnvironment env, IRegistryWrapper registry)
    {
        _fileSystem = fileSystem;
        _env = env;
        _registry = registry;
    }

    private string PublicDesktop => _env.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public";

    private string GetFlagPath(string flagName) => Path.Combine(PublicDesktop, flagName);

    public bool IsPhaseCompleted(string phase)
    {
        return _fileSystem.FileExists(GetFlagPath($"UpdateSkript_{phase}.flag"));
    }

    public void MarkPhaseCompleted(string phase)
    {
        var path = GetFlagPath($"UpdateSkript_{phase}.flag");
        _fileSystem.WriteAllText(path, DateTime.Now.ToString("O"));
    }

    public void ResetAllFlags()
    {
        var flags = new[] { "WU", "Dell", "Win11" };
        foreach (var flag in flags)
        {
            var path = GetFlagPath($"UpdateSkript_{flag}.flag");
            if (_fileSystem.FileExists(path))
            {
                _fileSystem.DeleteFile(path);
            }
        }
    }

    public (int Build, string Version) GetCurrentOsVersion()
    {
        return _registry.GetCurrentOsVersion();
    }
}
