using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Spectre.Console;

namespace UpdateSkriptApp.Services;

public class ProgressDownloader : IProgressDownloader
{
    private readonly IFileSystem _fileSystem;

    public ProgressDownloader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<bool> DownloadFileAsync(string url, string destination, string label, int maxRetries = 3)
    {
        int attempt = 0;
        using var client = new HttpClient();

        while (attempt < maxRetries)
        {
            attempt++;
            try
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;

                await AnsiConsole.Progress()
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask($"[cyan]{label}[/]");
                        if (totalBytes.HasValue)
                        {
                            task.MaxValue = totalBytes.Value;
                        }
                        else
                        {
                            task.IsIndeterminate = true;
                        }

                        using var contentStream = await response.Content.ReadAsStreamAsync();
                        using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                        var buffer = new byte[8192];
                        int bytesRead;
                        long totalRead = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                            task.Value = totalRead;
                        }
                    });

                if (_fileSystem.FileExists(destination))
                {
                    return true; // Success
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Download attempt {attempt} failed: {ex.Message}[/]");
                if (_fileSystem.FileExists(destination)) _fileSystem.DeleteFile(destination);
                await Task.Delay(3000); // Wait before retry
            }
        }

        AnsiConsole.MarkupLine($"[red]ERROR: Failed to download after {maxRetries} attempts: {url}[/]");
        return false;
    }
}
