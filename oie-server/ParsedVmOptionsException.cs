namespace OpenIntegrationEngine.ServerLauncher;

public class ParsedVmOptionsException : Exception
{
    public ParsedVmOptionsException(string message) : base(message) { }
    public ParsedVmOptionsException(string message, Exception innerException) : base(message, innerException) { }
}