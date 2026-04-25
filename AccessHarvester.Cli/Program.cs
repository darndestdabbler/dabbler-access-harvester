namespace AccessHarvester.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.Error.WriteLine("AccessHarvester — extract Access DB metadata and translate into a JSON knowledge base.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Subcommands (extract / translate / run) land in Session 3 and Session 4.");
        return args.Length == 0 ? 1 : 0;
    }
}
