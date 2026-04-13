using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace UpdateSkriptApp.Services
{
    public class PowerShellService
    {
        private Runspace _runspace;

        public PowerShellService()
        {
            _runspace = RunspaceFactory.CreateRunspace();
            _runspace.Open();
        }

        public async Task ExecuteScriptAsync(string script, Action<string> onLogReceived)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.Runspace = _runspace;
                ps.AddScript(script);

                var output = await Task.Run(() => ps.Invoke());
                
                foreach (var item in output)
                {
                    onLogReceived?.Invoke(item.ToString());
                }
            }
        }
    }
}
