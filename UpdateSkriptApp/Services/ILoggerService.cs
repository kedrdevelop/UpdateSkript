namespace UpdateSkriptApp.Services
{
    public interface ILoggerService
    {
        event Action<string, string> OnLogLineReceived; // Message, Color
        void Log(string message, string color = "White");
        void Clear();
    }
}
