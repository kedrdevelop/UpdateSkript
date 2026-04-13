using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace UpdateSkriptApp.Services
{
    public class PowerShellService
    {
        private Runspace _runspace;
        public event Action<string, string>? OnOutputReceived; // Message, Color
        public event Action<int, string>? OnProgressChanged;   // Percentage, Status

        public PowerShellService()
        {
            _runspace = RunspaceFactory.CreateRunspace();
            _runspace.Open();
        }

        public async Task ExecuteScriptAsync(string script)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.Runspace = _runspace;
                ps.AddScript(script);

                // Subscribe to streams
                ps.Streams.Information.DataAdded += (s, e) => { OnOutputReceived?.Invoke(ps.Streams.Information[e.Index].ToString(), "White"); };
                ps.Streams.Error.DataAdded       += (s, e) => { OnOutputReceived?.Invoke(ps.Streams.Error[e.Index].ToString(), "Red"); };
                ps.Streams.Warning.DataAdded     += (s, e) => { OnOutputReceived?.Invoke(ps.Streams.Warning[e.Index].ToString(), "Yellow"); };
                ps.Streams.Progress.DataAdded    += (s, e) => 
                { 
                    var record = ps.Streams.Progress[e.Index];
                    OnProgressChanged?.Invoke(record.PercentComplete, record.StatusDescription);
                };

                await Task.Run(() => ps.Invoke());
            }
        }

        public async Task<T?> GetFirstObjectAsync<T>(string script)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.Runspace = _runspace;
                ps.AddScript(script);
                var results = await Task.Run(() => ps.Invoke());
                if (results.Count > 0)
                {
                    return (T)results[0].BaseObject;
                }
            }
            return default;
        }
    }
}
