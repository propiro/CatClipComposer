using System.Reflection;
using CatClipComposer.Core.Plugins;

namespace CatClipComposer.Infrastructure.Plugins;

public sealed class PluginCatalog : IPluginCatalog
{
    private readonly Dictionary<string, ICatClipPlugin> _plugins;
    private readonly List<PluginLoadContext> _loadContexts;

    private PluginCatalog(
        Dictionary<string, ICatClipPlugin> plugins,
        List<string> diagnostics,
        List<PluginLoadContext> loadContexts)
    {
        _plugins = plugins;
        _loadContexts = loadContexts;
        Plugins = plugins.Values.OrderBy(plugin => plugin.Descriptor.Name).ToList();
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<ICatClipPlugin> Plugins { get; }

    public IReadOnlyList<string> Diagnostics { get; }

    public ICatClipPlugin? Find(string pluginId) =>
        _plugins.GetValueOrDefault(pluginId);

    public static PluginCatalog Load(string pluginFolder)
    {
        var plugins = new Dictionary<string, ICatClipPlugin>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<string>();
        var contexts = new List<PluginLoadContext>();
        if (!Directory.Exists(pluginFolder))
        {
            diagnostics.Add($"Plugin folder not found: {pluginFolder}");
            return new PluginCatalog(plugins, diagnostics, contexts);
        }

        foreach (var pluginPath in Directory.EnumerateFiles(
                     pluginFolder,
                     "*.dll",
                     SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var context = new PluginLoadContext(pluginPath);
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(pluginPath));
                var loadedAny = false;
                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (type.IsAbstract || type.IsInterface ||
                        !typeof(ICatClipPlugin).IsAssignableFrom(type) ||
                        type.GetConstructor(Type.EmptyTypes) is null)
                    {
                        continue;
                    }

                    var plugin = (ICatClipPlugin)Activator.CreateInstance(type)!;
                    Validate(plugin, pluginPath);
                    if (!plugins.TryAdd(plugin.Descriptor.Id, plugin))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate plugin ID '{plugin.Descriptor.Id}'.");
                    }

                    loadedAny = true;
                    diagnostics.Add(
                        $"Loaded {plugin.Descriptor.Name} {plugin.Descriptor.Version} ({plugin.Descriptor.Id}).");
                }

                if (loadedAny)
                {
                    contexts.Add(context);
                }
                else
                {
                    diagnostics.Add($"No Cat Clip Composer plugin types found in {pluginPath}.");
                }
            }
            catch (Exception exception)
            {
                diagnostics.Add($"Plugin load failed for {pluginPath}: {exception.Message}");
            }
        }

        return new PluginCatalog(plugins, diagnostics, contexts);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private static void Validate(ICatClipPlugin plugin, string path)
    {
        if (plugin.ApiVersion != PluginContract.CurrentApiVersion)
        {
            throw new InvalidOperationException(
                $"Plugin API {plugin.ApiVersion} is incompatible with host API {PluginContract.CurrentApiVersion}.");
        }

        var descriptor = plugin.Descriptor;
        if (string.IsNullOrWhiteSpace(descriptor.Id) ||
            string.IsNullOrWhiteSpace(descriptor.Name) ||
            string.IsNullOrWhiteSpace(descriptor.Version) ||
            descriptor.CompatibleTracks.Count == 0)
        {
            throw new InvalidOperationException($"Plugin metadata is incomplete in {path}.");
        }

        if (!descriptor.Id.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'))
        {
            throw new InvalidOperationException(
                $"Plugin ID '{descriptor.Id}' contains unsupported characters.");
        }

        var duplicateParameter = descriptor.Parameters
            .GroupBy(parameter => parameter.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateParameter is not null)
        {
            throw new InvalidOperationException(
                $"Plugin '{descriptor.Id}' declares parameter '{duplicateParameter.Key}' more than once.");
        }
    }
}
