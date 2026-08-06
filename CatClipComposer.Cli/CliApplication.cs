using CatClipComposer.Cli.CommandLine;
using CatClipComposer.Cli.Commands;
using CatClipComposer.Core;
using CatClipComposer.Infrastructure.Composition;

namespace CatClipComposer.Cli;

internal sealed class CliApplication(
    TextWriter? output = null,
    TextWriter? error = null)
{
    private readonly TextWriter _output = output ?? Console.Out;
    private readonly TextWriter _error = error ?? Console.Error;

    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var jsonRequested = arguments.Any(argument =>
            argument.Equals("--json", StringComparison.OrdinalIgnoreCase));

        try
        {
            var invocation = CliParser.Parse(arguments);
            if (invocation.HasOption("version") || invocation.Command == "version")
            {
                invocation.EnsureOnlyOptions("version", "json");
                await WriteVersionAsync(invocation.HasOption("json"));
                return CliExitCodes.Success;
            }

            if (invocation.HasOption("help") || invocation.Command is null or "help")
            {
                invocation.EnsureOnlyOptions("help", "version", "json");
                await WriteUsageAsync(invocation.HasOption("json"));
                return CliExitCodes.Success;
            }

            var configurationPath = ResolveOptionalPath(invocation.GetSingleValue("config"), "config");
            var dataFolder = ResolveOptionalPath(invocation.GetSingleValue("data"), "data");
            ApplicationServices services;
            try
            {
                services = await ApplicationServicesFactory.CreateAsync(
                    dataFolder,
                    configurationPath,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new CliConfigurationException(
                    "Could not initialize the configured data location.",
                    exception);
            }

            var settings = await LoadSettingsAsync(services, cancellationToken);
            var context = new CliCommandContext(
                services,
                settings,
                invocation.HasOption("json"),
                _output,
                _error,
                cancellationToken);

            return invocation.Command switch
            {
                "config" => await ConfigCommand.ExecuteAsync(invocation, context),
                "scan" => await ScanCommand.ExecuteAsync(invocation, context),
                "list" => await ListCommand.ExecuteAsync(invocation, context),
                "history" => await HistoryCommand.ExecuteAsync(invocation, context),
                "tag" => await TagCommand.ExecuteAsync(invocation, context),
                "usage" => await UsageCommand.ExecuteAsync(invocation, context),
                "project" => await ProjectCommand.ExecuteAsync(invocation, context),
                "render" => await RenderCommand.ExecuteAsync(invocation, context),
                _ => throw new CliUsageException(
                    $"Unknown command '{invocation.Command}'. Run with '--help' for usage.")
            };
        }
        catch (OperationCanceledException)
        {
            return await WriteErrorAsync(
                "Operation cancelled.",
                CliExitCodes.Cancelled,
                jsonRequested);
        }
        catch (CliUsageException exception)
        {
            return await WriteErrorAsync(
                exception.Message,
                CliExitCodes.InvalidArguments,
                jsonRequested,
                "Run with '--help' for usage.");
        }
        catch (CliConfigurationException exception)
        {
            return await WriteErrorAsync(
                GetDetailedMessage(exception),
                CliExitCodes.InvalidConfiguration,
                jsonRequested);
        }
        catch (Exception exception)
        {
            return await WriteErrorAsync(
                GetDetailedMessage(exception),
                CliExitCodes.ExecutionFailed,
                jsonRequested);
        }
    }

    private static async Task<Core.Models.ApplicationSettings> LoadSettingsAsync(
        ApplicationServices services,
        CancellationToken cancellationToken)
    {
        try
        {
            return await services.SettingsStore.LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CliConfigurationException(
                $"Could not read configuration '{services.Paths.ConfigurationPath}'.",
                exception);
        }
    }

    private static string? ResolveOptionalPath(string? value, string optionName)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new CliUsageException($"Invalid --{optionName} path '{value}': {exception.Message}");
        }
    }

    private async Task<int> WriteErrorAsync(
        string message,
        int exitCode,
        bool json,
        string? hint = null)
    {
        if (json)
        {
            await CliJson.WriteAsync(_output, new
            {
                status = "error",
                exitCode,
                error = message,
                hint
            });
        }
        else
        {
            await _error.WriteLineAsync($"Error: {message}");
            if (hint is not null)
            {
                await _error.WriteLineAsync(hint);
            }
        }

        return exitCode;
    }

    private Task WriteUsageAsync(bool json)
    {
        if (json)
        {
            return CliJson.WriteAsync(_output, new
            {
                name = "Cat Clip Composer headless CLI",
                version = ProductInfo.Version,
                usage = "CatClipComposer.Cli <command> [options]",
                commands = new[] { "version", "config", "scan", "list", "history", "tag", "usage", "project", "render" },
                commonOptions = new[] { "--config <file>", "--data <folder>", "--json", "--version", "--help" },
                exitCodes = new
                {
                    success = CliExitCodes.Success,
                    invalidArguments = CliExitCodes.InvalidArguments,
                    invalidConfiguration = CliExitCodes.InvalidConfiguration,
                    completedWithWarnings = CliExitCodes.CompletedWithWarnings,
                    executionFailed = CliExitCodes.ExecutionFailed,
                    cancelled = CliExitCodes.Cancelled
                }
            });
        }

        return _output.WriteLineAsync(
            $"""
        Cat Clip Composer headless CLI {ProductInfo.DisplayVersion}

        Usage:
          CatClipComposer.Cli <command> [options]

        Commands:
          version                Show the application version without initializing data.
          config                 Show resolved paths and effective INI settings.
          scan                   Scan configured source folders into the catalog.
          list [--all]           List available catalog clips; --all includes missing clips.
          history                List completed exports and their ordered source clips.
          tag                    Set or clear catalog tags for one clip.
          usage                  List completed project/export uses for one clip.
          project                Create or inspect a versioned project document.
          render                 Render ordered catalog clips and still screens.

        Common options:
          --config <file>        Override the INI path (default: executable directory).
          --data <folder>        Override the SQLite/cache data folder.
          --json                 Write one machine-readable JSON document to stdout.
          --version              Show the application version without initializing data.
          --help                 Show this help without initializing application data.

        Render options:
          --output <file>        Required. Relative paths use the configured output folder.
          --clip <catalog-id>    Add a catalog clip; repeat in the desired order.
          --screen "S|PATH"      Add a still image for S seconds; repeat in the desired order.
          --orientation <value>  landscape or portrait; defaults to INI.
          --encoder <value>      native-mpeg4, windows-h264, or libx264-gpl; defaults to INI.
          --project-file <file>  Render saved tracks/settings and record project identity.
          --project-name <name>  Associate a project name with the completed export.
          --overwrite            Permit replacement of an existing output file.

        Project options:
          --project-file <file>  Required .ccproject path.
          --create               Create a new empty five-track project.
          --project-name <name>  Optional name used with --create.
          --overwrite            Permit replacement when creating a project.

        Metadata options:
          tag --clip <id> --tags "cat; funny"
          tag --clip <id> --clear-tags
          usage --clip <id>

        Segment order follows the order of --clip and --screen options on the command line.
        Progress and diagnostics use stderr; command results use stdout.
        """);
    }

    private Task WriteVersionAsync(bool json) => json
        ? CliJson.WriteAsync(_output, new
        {
            name = ProductInfo.Name,
            version = ProductInfo.Version
        })
        : _output.WriteLineAsync($"{ProductInfo.Name} {ProductInfo.DisplayVersion}");

    private static string GetDetailedMessage(Exception exception) =>
        exception.InnerException is null
            ? exception.Message
            : $"{exception.Message} {exception.InnerException.Message}";
}
