namespace OpenIntegrationEngine.ServerLauncher
{
    internal static class LaunchArguments
    {
        public static void GetLaunchArguments(string[] args, out string workingDirectory, out string javaExe, out List<string> javaArgs)
        {
            // args = extra command line arguments to forward to oie
            workingDirectory = AppContext.BaseDirectory;
            var launcherJarPath = Path.Combine(workingDirectory, "mirth-server-launcher.jar");
            var options = new ParsedVmOptions() { Classpath = { launcherJarPath } };
            options.AddFile(Path.Combine(workingDirectory, "oieserver.vmoptions"));

            string? bundledJavaHome = Path.Combine(workingDirectory, "jre");
            if (!Directory.Exists(bundledJavaHome)) bundledJavaHome = null;

            javaExe = options.JavaCmdPath ?? Java.GetJavaExePath(bundledJavaHome);
            javaArgs = options.GetInvocation("com.mirth.connect.server.launcher.MirthLauncher", args);
        }
    }
}
