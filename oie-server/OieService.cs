using OpenIntegrationEngine.ServerLauncher;
using System.Diagnostics;
using System.IO.Pipes;
using System.ServiceProcess;

public class OieService : ServiceBase
{
    private Job? _job;
    private ProcessInfoHandle? _process;
    private AnonymousPipeServerStream? _stdout;
    private AnonymousPipeServerStream? _stderr;
    private bool isExitRequested;

    public OieService()
    {
        ServiceName = "oie-server";
        AutoLog = true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;

        this._process?.Dispose();
        this._job?.Dispose();
    }

    protected override void OnStart(string[] args)
    {
        try
        {
            LaunchArguments.GetLaunchArguments(args, out var workingDirectory, out var javaExe, out var javaArgs);
            var launchArgs = PasteArguments.FromList(javaArgs);
            WriteEvent($"Starting Open Integration Engine with command:\r\n\"{javaExe}\" {launchArgs}", EventLogEntryType.Information);

            this._job = new Job();
            //this._process = Process.Start(new ProcessStartInfo(javaExe, launchArgs)
            //{
            //    UseShellExecute = false,
            //    RedirectStandardOutput = true,
            //    RedirectStandardError = true,
            //    CreateNoWindow = true,
            //    WorkingDirectory = workingDirectory
            //});
            //if (_process is null)
            //{
            //    // This should be impossible, since we're launching an executable with UseShellExecute = false
            //    throw new NotSupportedException("Process start resulted in null process");
            //}

            //// attach to job to clean up automatically if we die
            //// Note: there is a race condition here, but dotnet makes it hard to avoid
            //_job.AddProcess(_process.SafeHandle);

            //// Handle stdin/stdout/stderr
            //_process.OutputDataReceived += (sender, e) => WriteEvent(e.Data, EventLogEntryType.Information);
            //_process.ErrorDataReceived += (sender, e) => WriteEvent(e.Data, EventLogEntryType.Error);
            //_process.BeginOutputReadLine();
            //_process.BeginErrorReadLine();

            this._stderr = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            this._stdout = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            this._process = ProcessUtilities.StartProcessSuspended(javaExe, launchArgs, workingDirectory, _stdout, _stderr);
            this._stdout.DisposeLocalCopyOfClientHandle();
            this._stderr.DisposeLocalCopyOfClientHandle();
            //this._job.AddProcess(this._process.HProcess.SafeWaitHandle);
            //ProcessUtilities.ResumeProcess(this._process);

            BeginRead(_stdout, EventLogEntryType.Information);
            BeginRead(_stderr, EventLogEntryType.Error);
            WaitForExit();
        }
        catch (Exception ex)
        {
            WriteEvent($"Failed to start Open Integration Engine: {ex.Message}\n{ex.StackTrace}", EventLogEntryType.Error);
            // rethrow to ensure service fails to start
            throw;
        }
    }

    private void WaitForExit()
    {
        ThreadPool.RegisterWaitForSingleObject(_process!.HProcess, (state, timedOut) =>
        {
            if (isExitRequested) return;
            isExitRequested = true;
            this.Stop();
        }, null, Timeout.Infinite, true);
    }

    private async void BeginRead(AnonymousPipeServerStream pipe, EventLogEntryType defaultLogLevel)
    {
        try
        {
            var textReader = new StreamReader(pipe);
            while (true)
            {
                var line = await textReader.ReadLineAsync();
                if (line is null) break;
                WriteEvent(line, defaultLogLevel);
            }
        }
        catch (Exception ex)
        {
            WriteEvent($"Unexpected error reading pipe: {ex}", EventLogEntryType.Error);
        }
    }

    private void WriteEvent(string data, EventLogEntryType defaultLogLevel)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(data)) return;

            const int MAX_LOG_LENGTH = 32000;
            if (data.Length > MAX_LOG_LENGTH)
            {
                data = data.Substring(0, MAX_LOG_LENGTH);
                // actual limit is 32767, so this is ok.
                data += " [TRUNCATED]";
            }

            EventLog.WriteEntry(data, InferLog4JLogLevel(data, defaultLogLevel));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write event log entry: {ex}");
        }
    }

    private static EventLogEntryType InferLog4JLogLevel(string data, EventLogEntryType defaultLogLevel)
    {
        var cmp = StringComparison.Ordinal;
        if (data.StartsWith("TRACE ", cmp)) return EventLogEntryType.SuccessAudit;
        else if (data.StartsWith("DEBUG ", cmp)) return EventLogEntryType.SuccessAudit;
        else if (data.StartsWith("INFO ", cmp)) return EventLogEntryType.Information;
        else if (data.StartsWith("WARN ", cmp)) return EventLogEntryType.Warning;
        else if (data.StartsWith("ERROR ", cmp)) return EventLogEntryType.Error;
        else if (data.StartsWith("FATAL ", cmp)) return EventLogEntryType.Error;
        else return defaultLogLevel;
    }

    protected override void OnStop()
    {
        if (_process is null) return;

        // Send Ctrl+C to the process to gracefully stop it
        if (!isExitRequested)
        {
            ConsoleUtilities.SendCtrlC(_process.ProcessId, () => _process.HProcess.WaitOne());
            //if (!GenerateConsoleCtrlEvent(1 /* CTRL_BREAK_EVENT */, _process.ProcessId))
            //{
            //    var gle = Marshal.GetLastWin32Error();
            //    throw new Win32Exception($"GenerateConsoleCtrlEvent failed with error code {gle}");
            //}
        }
        isExitRequested = true;

        // Wait for Mirth to exit gracefully
        _process.HProcess.WaitOne();
    }
}
