using System.Text.RegularExpressions;

namespace OpenIntegrationEngine.ServerLauncher;

internal class ParsedVmOptions
{
    public List<string> VmOptions { get; } = new();
    public List<string> Classpath { get; } = new();
    public string? JavaCmdPath { get; set; }

    public HashSet<string> ParsedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Substitutes ${VAR_NAME} patterns within a given string.
    /// </summary>
    private static string SubstituteEnvVars(string s)
    {
        if (!s.Contains("${")) return s;

        return Regex.Replace(s, @"\$\{([a-zA-Z_][a-zA-Z0-9_]*)\}", match =>
        {
            string varName = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(varName) ?? string.Empty;
        });
    }

    /// <summary>
    /// Recursively parses a vmoptions file and any files included via -include-options.
    /// Accumulates JVM options, classpath segments, and the effective Java command path.
    /// </summary>
    public void AddFile(string filepath)
    {
        try
        {
            if (ParsedFiles.Contains(filepath))
            {
                throw new ParsedVmOptionsException($"Detected circular include for file: {filepath}");
            }
            ParsedFiles.Add(filepath);

            var baseDirectory = Path.GetDirectoryName(filepath);
            foreach (string line in File.ReadLines(filepath))
            {
                try
                {
                    AddOption(line, baseDirectory);
                }
                catch (Exception e)
                {
                    throw new ParsedVmOptionsException($"Error parsing {filepath} line: {line}", e);
                }
            }
        }
        catch (Exception ex) when (ex is not FileNotFoundException && ex is not ParsedVmOptionsException)
        {
            throw new ParsedVmOptionsException($"Failed to read or parse vmoptions file {filepath}: {ex.Message}");
        }
    }

    public void AddOption(string line, string baseDirectory)
    {
        line = line.Trim();
        if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
        {
            return;
        }

        int firstSpaceIndex = line.IndexOf(' ');
        string firstWord;
        string rest;

        if (firstSpaceIndex == -1)
        {
            firstWord = line;
            rest = string.Empty;
        }
        else
        {
            firstWord = line.Substring(0, firstSpaceIndex);
            rest = line.Substring(firstSpaceIndex + 1).Trim();
        }

        switch (firstWord)
        {
            case "-include-options":
                AddFile(Path.Combine(baseDirectory, SubstituteEnvVars(rest)));
                break;
            case "-java-cmd":
                JavaCmdPath = SubstituteEnvVars(rest);
                break;
            case "-classpath":
                Classpath.Clear();
                Classpath.Add(SubstituteEnvVars(rest));
                break;
            case "-classpath/a":
                Classpath.Add(SubstituteEnvVars(rest));
                break;
            case "-classpath/p":
                Classpath.Insert(0, SubstituteEnvVars(rest));
                break;
            default:
                VmOptions.Add(SubstituteEnvVars(line));
                break;
        }
    }

    /// <summary>
    /// Constructs the complete command to launch the Java application.
    /// </summary>
    public List<string> GetInvocation(string mainClass, IReadOnlyList<string> appArgs)
    {
        var command = new List<string>();
        command.AddRange(VmOptions);
        command.Add("-cp");
        command.Add(string.Join(Path.PathSeparator.ToString(), Classpath));
        command.Add(mainClass);
        command.AddRange(appArgs);
        return command;
    }
}
