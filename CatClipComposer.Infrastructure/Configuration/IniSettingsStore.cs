using System.Text;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure.Configuration;

public sealed class IniSettingsStore(AppPaths paths) : ISettingsStore
{
    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var ini = await IniFile.LoadAsync(paths.ConfigurationPath, cancellationToken);
        return ApplicationSettingsIniMapper.FromIni(ini);
    }

    public async Task SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        var content = ApplicationSettingsIniMapper.ToIni(settings);
        var temporaryPath = $"{paths.ConfigurationPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            File.Move(temporaryPath, paths.ConfigurationPath, overwrite: true);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidOperationException(
                $"The executable directory is not writable. Cat Clip Composer must save its configuration at '{paths.ConfigurationPath}'.",
                exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
