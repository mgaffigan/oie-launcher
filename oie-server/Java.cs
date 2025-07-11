using Microsoft.Win32;

namespace OpenIntegrationEngine.ServerLauncher;

public class Java
{
    public static string GetJavaExePath()
    {
        return GetJavaExePath(null);
    }

    public static string GetJavaExePath(string? javaHome)
    {
        string javaPath;
        if (string.IsNullOrWhiteSpace(javaHome))
        {
            javaPath = GetJavaHomePath();
        }
        else
        {
            javaPath = javaHome;
        }

        var javaExePath = Path.Combine(javaPath, "bin", "java.exe");
        if (!File.Exists(javaExePath))
        {
            throw new FileNotFoundException("Java.exe not found", javaExePath);
        }

        return javaExePath;
    }

    public static string GetJavaHomePath()
    {
        var environmentPath = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(environmentPath))
        {
            return environmentPath;
        }

        var path64 = GetRegistryPath(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64));
        if (path64 != null)
        {
            return path64;
        }

        var path32 = GetRegistryPath(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32));
        if (path32 != null)
        {
            return path32;
        }

        throw new PlatformNotSupportedException("Cannot locate java");
    }

    private static string? GetRegistryPath(RegistryKey root)
    {
        string javaKey = @"SOFTWARE\JavaSoft\Java Runtime Environment\";
        using (var rk = root.OpenSubKey(javaKey, false))
        {
            if (rk == null)
                return null;

            var currentVersion = rk.GetValue("CurrentVersion") as string;
            if (currentVersion == null)
                return null;

            using (var key = rk.OpenSubKey(currentVersion, false))
            {
                return key?.GetValue("JavaHome") as string;
            }
        }
    }
}
