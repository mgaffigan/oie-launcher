using System.ComponentModel;
using static Windows.Win32.PInvoke;

namespace OpenIntegrationEngine.ServerLauncher
{
    internal sealed class ConsoleUtilities
    {
        public static void SendCtrlC(uint pid, Action waitForStop)
        {
            if (!AttachConsole(pid))
            {
                throw new Win32Exception("Could not attach to child console");
            }
            try
            {
                //Console.CancelKeyPress += IgnoreCtrlC;

                // Inhibit handling of CtrlC by dotnet
                if (!SetConsoleCtrlHandler(null, true))
                {
                    throw new Win32Exception("Could not inhibit handling of Ctrl+C for wrapper");
                }
                try
                {
                    if (!GenerateConsoleCtrlEvent(0 /* CTRL_C_EVENT */, pid))
                    {
                        throw new Win32Exception("Could not send Ctrl+C to child process");
                    }
                    waitForStop();
                }
                finally
                {
                    if (!SetConsoleCtrlHandler(null, false))
                    {
                        throw new Win32Exception("Could not restore handling of Ctrl+C for wrapper");
                    }

                    //Console.CancelKeyPress -= IgnoreCtrlC;
                }
            }
            finally
            {
                if (!FreeConsole())
                {
                    throw new Win32Exception("Could not free console after sending Ctrl+C");
                }
            }
        }

        private static void IgnoreCtrlC(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
        }
    }
}
