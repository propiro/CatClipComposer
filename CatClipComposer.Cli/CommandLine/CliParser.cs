namespace CatClipComposer.Cli.CommandLine;

internal static class CliParser
{
    private static readonly HashSet<string> FlagOptions = new(
        ["help", "version", "json", "all", "overwrite", "create", "clear-tags", "regenerate-previews"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ValueOptions = new(
        ["config", "data", "output", "orientation", "encoder", "clip", "screen",
            "project-file", "project-name", "tags"],
        StringComparer.OrdinalIgnoreCase);

    public static CliInvocation Parse(IReadOnlyList<string> arguments)
    {
        string? command = null;
        var options = new List<CliOption>();

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                if (command is not null)
                {
                    throw new CliUsageException($"Unexpected argument '{argument}'.");
                }

                command = argument.ToLowerInvariant();
                continue;
            }

            var name = argument[2..];
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new CliUsageException("An option name cannot be empty.");
            }

            if (FlagOptions.Contains(name))
            {
                options.Add(new CliOption(name.ToLowerInvariant(), null));
                continue;
            }

            if (!ValueOptions.Contains(name))
            {
                throw new CliUsageException($"Unknown option '--{name}'.");
            }

            if (++index >= arguments.Count || arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new CliUsageException($"Option '--{name}' requires a value.");
            }

            options.Add(new CliOption(name.ToLowerInvariant(), arguments[index]));
        }

        return new CliInvocation(command, options);
    }
}
