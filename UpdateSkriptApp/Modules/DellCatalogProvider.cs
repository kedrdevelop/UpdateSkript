using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Xml.Linq;
using Spectre.Console;
using UpdateSkriptApp.Services;

namespace UpdateSkriptApp.Modules;

public static class DellCatalogProvider
{
    public static async Task<string> DownloadAndExtractDriverPackAsync()
    {
        string systemModel;
        string systemManufacturer;
        
        using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
        {
            var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (obj == null) return null;
            
            systemManufacturer = obj["Manufacturer"].ToString()?.Trim();
            systemModel = obj["Model"].ToString()?.Trim();
        }

        if (string.IsNullOrEmpty(systemManufacturer) || !systemManufacturer.Contains("Dell", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[yellow]This system is not manufactured by Dell (found: {systemManufacturer}). Skipping.[/]");
            return null;
        }

        AnsiConsole.MarkupLine($"[cyan]Confirmed Dell system: {systemModel}[/]");

        string tempDir = Path.Combine(Path.GetTempPath(), "DellDriverPack");
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        Directory.CreateDirectory(tempDir);

        string cabPath = Path.Combine(tempDir, "DriverPackCatalog.cab");
        string xmlPath = Path.Combine(tempDir, "DriverPackCatalog.xml");

        AnsiConsole.MarkupLine("[cyan]Downloading Dell Server Catalog...[/]");
        bool catalogOk = await ProgressDownloader.DownloadFileAsync("https://downloads.dell.com/catalog/DriverPackCatalog.cab", cabPath, "Downloading Dell catalog...");
        
        if (!catalogOk) return null;

        AnsiConsole.MarkupLine("[cyan]Extracting catalog...[/]");
        await PowerShellHost.ExecuteScriptAsync($"expand.exe \"{cabPath}\" \"{xmlPath}\"");

        if (!File.Exists(xmlPath))
        {
            AnsiConsole.MarkupLine("[red]Failed to extract DriverPackCatalog.xml[/]");
            return null;
        }

        AnsiConsole.MarkupLine("[cyan]Parsing XML catalog...[/]");
        
        var (build, _) = RegistryService.GetCurrentOsVersion();
        string[] osCodeTargets = build >= 22000 ? new[] { "Windows11", "Windows10" } : new[] { "Windows10" };

        XDocument doc;
        try { doc = XDocument.Load(xmlPath); } catch { return null; }

        XNamespace ns = "";
        if (doc.Root.Name.Namespace != XNamespace.None) ns = doc.Root.Name.Namespace;

        var matchingPack = doc.Descendants(ns + "DriverPackage")
            .Where(p => p.Attribute("type")?.Value != "WinPE")
            .FirstOrDefault(p =>
            {
                bool modelMatch = p.Descendants(ns + "Model").Any(m => m.Attribute("name")?.Value == systemModel);
                bool osMatch = p.Descendants(ns + "OperatingSystem").Any(os => 
                    osCodeTargets.Contains(os.Attribute("osCode")?.Value) && os.Attribute("osArch")?.Value == "x64");
                return modelMatch && osMatch;
            });

        if (matchingPack == null)
        {
            AnsiConsole.MarkupLine($"[red]No driver pack found for model {systemModel}[/]");
            return null;
        }

        string packPath = matchingPack.Attribute("path")?.Value;
        string packName = Path.GetFileName(packPath);
        string packUrl = "https://downloads.dell.com/" + packPath;
        string localPackPath = Path.Combine(tempDir, packName);
        string extractDir = Path.Combine(tempDir, "Drivers");

        AnsiConsole.MarkupLine($"[green]Found driver pack: {packName}[/]");
        bool packOk = await ProgressDownloader.DownloadFileAsync(packUrl, localPackPath, $"Downloading {packName}...");
        if (!packOk) return null;

        Directory.CreateDirectory(extractDir);
        AnsiConsole.MarkupLine("[cyan]Extracting driver pack...[/]");

        if (packName.EndsWith(".cab", StringComparison.OrdinalIgnoreCase))
        {
            await PowerShellHost.ExecuteScriptAsync($"expand.exe \"{localPackPath}\" -F:* \"{extractDir}\"");
        }
        else if (packName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = localPackPath,
                Arguments = $"/s /e=\"{extractDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(startInfo);
            await proc.WaitForExitAsync();
        }

        return extractDir;
    }
}
