namespace CatClipComposer.Cli.CommandLine;

internal sealed class CliUsageException(string message) : Exception(message);

internal sealed class CliConfigurationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
