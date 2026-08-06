namespace CatClipComposer.Cli.CommandLine;

internal sealed record CliOption(string Name, string? Value);

internal sealed record CliInvocation(
    string? Command,
    IReadOnlyList<CliOption> Options)
{
    public bool HasOption(string name) =>
        Options.Any(option => option.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public string? GetSingleValue(string name)
    {
        var values = Options
            .Where(option => option.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(option => option.Value)
            .ToList();
        if (values.Count > 1)
        {
            throw new CliUsageException($"Option '--{name}' can only be specified once.");
        }

        return values.SingleOrDefault();
    }

    public void EnsureOnlyOptions(params string[] allowedNames)
    {
        var allowed = new HashSet<string>(allowedNames, StringComparer.OrdinalIgnoreCase);
        var invalid = Options.FirstOrDefault(option => !allowed.Contains(option.Name));
        if (invalid is not null)
        {
            throw new CliUsageException(
                $"Option '--{invalid.Name}' is not valid for the '{Command}' command.");
        }
    }
}
