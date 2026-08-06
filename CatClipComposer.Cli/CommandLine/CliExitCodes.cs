namespace CatClipComposer.Cli.CommandLine;

internal static class CliExitCodes
{
    public const int Success = 0;
    public const int InvalidArguments = 2;
    public const int InvalidConfiguration = 3;
    public const int CompletedWithWarnings = 4;
    public const int ExecutionFailed = 5;
    public const int Cancelled = 130;
}
