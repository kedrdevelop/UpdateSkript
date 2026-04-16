using Spectre.Console;

namespace UpdateSkriptApp;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Branding Header
        AnsiConsole.Write(
            new FigletText("Servier DE OOBE Preparation Tool")
                .Centered()
                .Color(Color.Cyan1));

        AnsiConsole.Write(new Rule("[yellow]Windows 11 Corporate Automation Tool[/]").Centered());
        AnsiConsole.WriteLine();

        // 2. Main Logic Flow
        try 
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Initializing deployment engine...", async ctx => 
                {
                    await Task.Delay(1000); // Simulate init
                    AnsiConsole.MarkupLine("[green]✔[/] Environment checked.");
                });

            // Placeholder for the phases logic
            var table = new Table();
            table.AddColumn("Phase");
            table.AddColumn("Status");

            table.AddRow("1. Windows Updates", "[grey]Pending[/]");
            table.AddRow("2. Dell Driver Updates", "[grey]Pending[/]");
            table.AddRow("3. Windows 11 25H2 Upgrade", "[grey]Pending[/]");

            AnsiConsole.Write(table);

            AnsiConsole.MarkupLine("\n[yellow]Ready for Phase 1. Press any key to start...[/]");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }
}
