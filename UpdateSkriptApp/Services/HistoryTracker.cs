using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UpdateSkriptApp.Services;

public interface IUpdateHistoryTracker
{
    int GetAttempts(string titleId);
    void IncrementAttempt(string titleId);
    void MarkStatus(string titleId, string status);
    bool IsSkippedOrInstalled(string titleId);
}

public class UpdateHistoryTracker : IUpdateHistoryTracker
{
    private readonly IFileSystem _fileSystem;
    private readonly string _historyFile;
    private readonly object _lock = new object();
    private Dictionary<string, UpdateRecord> _records;

    public UpdateHistoryTracker(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _historyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WindowsUpdates_History.json");
        Load();
    }

    private void Load()
    {
        lock (_lock)
        {
            if (_fileSystem.FileExists(_historyFile))
            {
                try
                {
                    string json = _fileSystem.ReadAllText(_historyFile);
                    _records = JsonSerializer.Deserialize<Dictionary<string, UpdateRecord>>(json) ?? new();
                }
                catch
                {
                    _records = new Dictionary<string, UpdateRecord>(StringComparer.OrdinalIgnoreCase);
                }
            }
            else
            {
                _records = new Dictionary<string, UpdateRecord>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private void Save()
    {
        lock (_lock)
        {
            string json = JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
            _fileSystem.WriteAllText(_historyFile, json);
        }
    }

    private string CleanId(string titleId) => titleId?.Trim() ?? "UNKNOWN";

    public int GetAttempts(string titleId)
    {
        var id = CleanId(titleId);
        lock (_lock)
        {
            if (_records.TryGetValue(id, out var record))
            {
                return record.Attempts;
            }
            return 0;
        }
    }

    public void IncrementAttempt(string titleId)
    {
        var id = CleanId(titleId);
        lock (_lock)
        {
            if (!_records.ContainsKey(id))
            {
                _records[id] = new UpdateRecord();
            }
            _records[id].Attempts++;
            _records[id].LastAttempt = DateTime.Now;
            _records[id].Status = "Attempting";
            Save();
        }
    }

    public void MarkStatus(string titleId, string status)
    {
        var id = CleanId(titleId);
        lock (_lock)
        {
            if (!_records.ContainsKey(id))
            {
                _records[id] = new UpdateRecord();
            }
            _records[id].Status = status;
            Save();
        }
    }

    public bool IsSkippedOrInstalled(string titleId)
    {
        var id = CleanId(titleId);
        lock (_lock)
        {
            if (_records.TryGetValue(id, out var record))
            {
                return record.Status == "Installed" || record.Status == "Skipped_3_Strikes" || record.Attempts >= 3;
            }
            return false;
        }
    }
}

public class UpdateRecord
{
    public int Attempts { get; set; } = 0;
    public string Status { get; set; } = "Pending";
    public DateTime? LastAttempt { get; set; }
}
