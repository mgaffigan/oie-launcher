using System.ServiceProcess;

namespace OpenIntegrationEngine.ServerLauncher
{
    public class JobObjectWrapper
    {
        public static int Main(string[] args)
        {
            // If someone runs us interactively, run attached to the console
            if (Environment.UserInteractive)
            {
                return ConsoleMain(args);
            }

            ServiceBase.Run(new OieService());
            return 0;
        }

        private static int ConsoleMain(string[] args)
        {
            // Build the command line (argv[0] must be the executable)
            LaunchArguments.GetLaunchArguments(args, out var workingDirectory, out var javaExe, out var javaArgs);
            javaArgs.Insert(0, javaExe);
            var commandLine = PasteArguments.FromList(javaArgs);

            // Print the command for diagnostic purposes
            Console.WriteLine("Starting Open Integration Engine...");
            Console.WriteLine(commandLine);

            // Let Ctrl+C go to the client and skip us
            Console.CancelKeyPress += (sender, e) => e.Cancel = true;
            return ProcessUtilities.RunProcessAsWrapper(javaExe, commandLine, workingDirectory);
        }
    }
}