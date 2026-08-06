using CatClipComposer.Core.Models;
using CatClipComposer.Infrastructure.Composition;

namespace CatClipComposer.Cli.Commands;

internal sealed record CliCommandContext(
    ApplicationServices Services,
    ApplicationSettings Settings,
    bool Json,
    TextWriter Output,
    TextWriter Error,
    CancellationToken CancellationToken);
