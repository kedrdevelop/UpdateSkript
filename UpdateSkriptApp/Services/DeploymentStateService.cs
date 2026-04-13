using System.IO;

namespace UpdateSkriptApp.Services
{
    public interface IDeploymentStateService
    {
        bool IsPhaseCompleted(string phaseName);
        void MarkPhaseCompleted(string phaseName);
        void ResetAll();
    }

    public class DeploymentStateService : IDeploymentStateService
    {
        private readonly string _flagDirectory = @"C:\Users\Public";
        private readonly Dictionary<string, string> _flagFiles;

        public DeploymentStateService()
        {
            _flagFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "WU", "UpdateSkript_WU.flag" },
                { "Dell", "UpdateSkript_Dell.flag" },
                { "Win11", "UpdateSkript_Win11.flag" }
            };
        }

        public bool IsPhaseCompleted(string phaseName)
        {
            if (_flagFiles.TryGetValue(phaseName, out var fileName))
            {
                return File.Exists(Path.Combine(_flagDirectory, fileName));
            }
            return false;
        }

        public void MarkPhaseCompleted(string phaseName)
        {
            if (_flagFiles.TryGetValue(phaseName, out var fileName))
            {
                var path = Path.Combine(_flagDirectory, fileName);
                if (!File.Exists(path))
                {
                    File.Create(path).Dispose();
                }
            }
        }

        public void ResetAll()
        {
            foreach (var fileName in _flagFiles.Values)
            {
                var path = Path.Combine(_flagDirectory, fileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
