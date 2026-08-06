namespace CatClipComposer.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string? dataFolder = null)
    {
        DataFolder = dataFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatClipComposer");
        DatabasePath = Path.Combine(DataFolder, "catalog.db");
        SettingsPath = Path.Combine(DataFolder, "settings.json");
        ThumbnailFolder = Path.Combine(DataFolder, "thumbnails");
    }

    public string DataFolder { get; }

    public string DatabasePath { get; }

    public string SettingsPath { get; }

    public string ThumbnailFolder { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(ThumbnailFolder);
    }
}
