namespace CatClipComposer.Cli.CommandLine;

internal sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
