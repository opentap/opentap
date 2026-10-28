namespace GitVersionCalculator;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 2 || args.Contains("--help"))
        {
            Console.WriteLine("Usage: GitVersionCalculator [repository-or-subdirectory] [HEAD|branch|tag|commit-hash]");
            return args.Contains("--help") ? 0 : 2;
        }
        try
        {
            var calculator = new VersionCalculator(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory())
            {
                Warning = message => Console.Error.WriteLine("Warning: " + message)
            };
            Console.WriteLine(calculator.GetVersion(args.Length > 1 ? args[1] : "HEAD"));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }
}
