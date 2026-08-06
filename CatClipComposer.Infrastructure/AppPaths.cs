namespace CatClipComposer.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string? dataFolder = null, string? configurationPath = null)
    {
        DataFolder = dataFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatClipComposer");
        DatabasePath = Path.Combine(DataFolder, "catalog.db");
        ThumbnailFolder = Path.Combine(DataFolder, "thumbnails");
        ConfigurationPath = configurationPath ?? Path.Combine(
            AppContext.BaseDirectory,
            "CatClipComposer.ini");
    }

    public string DataFolder { get; }

    public string DatabasePath { get; }

    public string ThumbnailFolder { get; }

    public string ConfigurationPath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(ThumbnailFolder);
    }
}
