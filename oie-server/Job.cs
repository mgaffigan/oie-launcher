using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32.System.JobObjects;
using static Windows.Win32.PInvoke;

namespace OpenIntegrationEngine.ServerLauncher
{
    internal sealed class Job : IDisposable
    {
        private readonly SafeFileHandle _Handle;

        public unsafe Job()
        {
            _Handle = CreateJobObject(null, null);
            if (_Handle.IsInvalid) throw new Win32Exception("Failed to create job object");

            // Set the job object to kill all processes when the job is closed
            var jeli = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            jeli.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            if (!SetInformationJobObject(_Handle, JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                &jeli, (uint)Marshal.SizeOf(jeli)))
            {
                throw new Win32Exception("Failed to set job object information");
            }
        }

        public void Dispose() => _Handle.Dispose();

        public void AddProcess(SafeHandle processHandle)
        {
            if (!AssignProcessToJobObject(_Handle, processHandle))
            {
                throw new Win32Exception("Failed to assign process to job object");
            }
        }
    }
}
