using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.System.Console;
using Windows.Win32.System.Threading;
using static Windows.Win32.PInvoke;

namespace OpenIntegrationEngine.ServerLauncher
{
    internal static class ProcessUtilities
    {
        /// <summary>
        /// Run a process forwarding standard input, output, and error streams to the parent process.
        /// Use a job object to ensure the child process is terminated when the parent exits.
        /// </summary>
        /// <param name="exe">Path to the executable to run</param>
        /// <param name="commandLine">Command line to pass to the child process</param>
        /// <param name="workingDirectory">Optional working directory to set for the child process</param>
        /// <returns>The exit code of the child process</returns>
        /// <exception cref="Win32Exception"></exception>
        public static unsafe int RunProcessAsWrapper(string exe, string commandLine, string? workingDirectory = null)
        {
            using (var processInfo = StartProcessSuspended(exe, commandLine, workingDirectory))
            using (new ProcessTerminator(processInfo.HProcess.SafeWaitHandle))
            using (var job = new Job())
            {
                // Assign the child while suspended to avoid a race condition
                job.AddProcess(processInfo.HProcess.SafeWaitHandle);

                // Now that the child is safely assigned to the job, we can let it run.
                ResumeProcess(processInfo);

                // Wait for exit
                _ = processInfo.HProcess.WaitOne();

                // Retrieve the exit code of the child process
                return processInfo.GetExitCode();
            }
        }


        public static unsafe void ResumeProcess(ProcessInfoHandle processInfo)
        {
            uint resumeResult = ResumeThread(processInfo.HThread);
            if (resumeResult == unchecked((uint)-1))
            {
                throw new Win32Exception("Failed to resume child process thread");
            }
        }

        public static unsafe ProcessInfoHandle StartProcessSuspended(
            string applicationName, string commandLine, string? workingDirectory,
            AnonymousPipeServerStream stdout, AnonymousPipeServerStream stderr)
        {
            var mutableCommandLine = (commandLine + '\0').ToCharArray().AsSpan();
            var startupInfo = new STARTUPINFOW();
            startupInfo.cb = (uint)Marshal.SizeOf(startupInfo);
            startupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;
            startupInfo.hStdInput = HANDLE.Null;
            startupInfo.hStdOutput = (HANDLE)stdout.ClientSafePipeHandle.DangerousGetHandle();
            startupInfo.hStdError = (HANDLE)stderr.ClientSafePipeHandle.DangerousGetHandle();

            if (!CreateProcess(applicationName, ref mutableCommandLine, lpProcessAttributes: null,
                lpThreadAttributes: null, bInheritHandles: true,
                0,
                lpEnvironment: null, workingDirectory, startupInfo, out var processInfo))
            {
                throw new Win32Exception("Failed to create child process");
            }

            return new(processInfo);
        }

        private static unsafe ProcessInfoHandle StartProcessSuspended(string applicationName,
            string commandLine, string? workingDirectory)
        {
            var mutableCommandLine = (commandLine + '\0').ToCharArray().AsSpan();
            var startupInfo = new STARTUPINFOW();
            startupInfo.cb = (uint)Marshal.SizeOf(startupInfo);
            startupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;
            startupInfo.hStdInput = GetStdHandle(STD_HANDLE.STD_INPUT_HANDLE);
            startupInfo.hStdOutput = GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
            startupInfo.hStdError = GetStdHandle(STD_HANDLE.STD_ERROR_HANDLE);

            if (!CreateProcess(applicationName, ref mutableCommandLine, lpProcessAttributes: null,
                lpThreadAttributes: null, bInheritHandles: true, PROCESS_CREATION_FLAGS.CREATE_SUSPENDED,
                lpEnvironment: null, workingDirectory, startupInfo, out var processInfo))
            {
                throw new Win32Exception("Failed to create child process");
            }

            return new(processInfo);
        }
    }

    internal class ProcessTerminator(SafeHandle hProcess) : IDisposable
    {
        public void Dispose()
        {
            // Ensure the process is terminated when this object is disposed.
            // If it exited normally, this will be a no-op.
            _ = TerminateProcess(hProcess, 1);
        }
    }

    internal sealed class ProcessInfoHandle(PROCESS_INFORMATION processInfo) : IDisposable
    {
        public ProcessWaitHandle HProcess { get; } = new(new SafeWaitHandle(processInfo.hProcess, true));
        public SafeProcessHandle HThread { get; } = new(processInfo.hThread, true);
        public uint ProcessId => processInfo.dwProcessId;

        public void Dispose()
        {
            HProcess.Dispose();
            HThread.Dispose();
        }

        public unsafe int GetExitCode()
        {
            if (!GetExitCodeProcess(HProcess.SafeWaitHandle, out var childExitCode))
            {
                throw new Win32Exception("Failed to get child process exit code");
            }

            return (int)childExitCode;
        }
    }

    internal class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeWaitHandle waitHandle)
            : base()
        {
            this.SafeWaitHandle = waitHandle;
        }
    }
}
