using System.Reflection;
using System.Text.Json;
using Hatband.Core.Enums.Stores;
using Hatband.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Hatband.Infrastructure.Plugins;

internal sealed class PluginCatalogLoader
{
    private const int CurrentApiVersion = HatbandSdk.ApiVersion;
    private const string ManifestFileName = "hatband-plugin.json";
    private readonly PluginLoadingOptions _options;
    private readonly List<LoadedPlugin> _plugins = [];
    private readonly List<PluginLoadIssue> _issues = [];
    private readonly HashSet<string> _pluginIds = new(StringComparer.Ordinal);
    private readonly List<PluginAssemblyLoadContext> _loadContexts = [];

    public PluginCatalogLoader(PluginLoadingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public PluginCatalog LoadAndRegister(IServiceCollection services, string userPluginDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPluginDirectory);

        LoadPlugins(services, _options.BuiltInPluginDirectory, isExternal: false);
        LoadPlugins(services, userPluginDirectory, isExternal: true);
        var catalog = new PluginCatalog(_plugins, _issues);
        services.AddSingleton(catalog);
        return catalog;
    }

    private void LoadPlugins(IServiceCollection services, string directory, bool isExternal)
    {
        if (!Directory.Exists(directory) && !isExternal)
        {
            _issues.Add(new PluginLoadIssue(directory, "The built-in plugin directory was not found.", null));
            return;
        }

        Directory.CreateDirectory(directory);
        foreach (var pluginDirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            LoadPlugin(services, pluginDirectory, isExternal);
        }
    }

    private void LoadPlugin(IServiceCollection services, string pluginDirectory, bool isExternal)
    {
        PluginAssemblyLoadContext? loadContext = null;
        try
        {
            var manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), ManifestJsonOptions)
                ?? throw new InvalidDataException("The plugin manifest is empty.");
            ValidateManifest(manifest);

            var assemblyPath = ResolvePluginPath(pluginDirectory, manifest.EntryAssembly);
            loadContext = new PluginAssemblyLoadContext(assemblyPath, _options.SharedAssemblies);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var pluginType = assembly.GetType(manifest.EntryType, throwOnError: true, ignoreCase: false)
                ?? throw new InvalidDataException($"Entry type '{manifest.EntryType}' was not found.");
            if (!typeof(HatbandPlugin).IsAssignableFrom(pluginType) || pluginType.IsAbstract)
            {
                throw new InvalidDataException($"Entry type '{manifest.EntryType}' must derive from HatbandPlugin.");
            }

            var plugin = CreatePlugin(pluginType);
            if (!string.Equals(plugin.Id, manifest.Id, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The plugin ID does not match the manifest ID.");
            }

            RegisterPlugin(services, plugin, assembly, isExternal, manifest.Name, manifest.Version);
            _loadContexts.Add(loadContext);
        }
        catch (Exception exception)
        {
            _issues.Add(new PluginLoadIssue(pluginDirectory, exception.Message, exception));
        }
    }

    private void RegisterPlugin(
        IServiceCollection services,
        HatbandPlugin plugin,
        Assembly assembly,
        bool isExternal,
        string name,
        string version)
    {
        ValidatePluginId(plugin.Id);
        if (_pluginIds.Contains(plugin.Id))
        {
            throw new InvalidDataException($"A plugin with ID '{plugin.Id}' is already registered.");
        }

        var pluginServices = new ServiceCollection();
        plugin.ConfigureServices(pluginServices);
        RegisterPluginCapabilities(pluginServices, plugin, assembly);
        foreach (var descriptor in pluginServices)
        {
            services.Add(descriptor);
        }

        _pluginIds.Add(plugin.Id);
        _plugins.Add(new LoadedPlugin(plugin.Id, name, version, isExternal, assembly));
    }

    private void RegisterPluginCapabilities(ServiceCollection services, HatbandPlugin plugin, Assembly assembly)
    {
        var capabilityTypes = PluginCapabilityTypes.Get(_options);
        var implementations = plugin.CapabilityImplementations.Count > 0
            ? plugin.CapabilityImplementations
            : FindCapabilityImplementations(assembly, capabilityTypes);
        var implementationSet = implementations.ToHashSet();
        ValidateCapabilityImplementations(assembly, implementationSet, capabilityTypes, plugin.Id);
        if (implementationSet.Count == 0)
        {
            return;
        }

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.Where(implementationSet.Contains))
            .AsSelfWithInterfaces(contract => capabilityTypes.Contains(contract))
            .WithSingletonLifetime());
    }

    private static IReadOnlyCollection<Type> FindCapabilityImplementations(
        Assembly assembly,
        IReadOnlyCollection<Type> capabilityTypes)
    {
        return assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           capabilityTypes.Any(capability => capability.IsAssignableFrom(type)))
            .ToArray();
    }

    private static void ValidateCapabilityImplementations(
        Assembly assembly,
        IReadOnlySet<Type> implementations,
        IReadOnlyCollection<Type> capabilityTypes,
        string pluginId)
    {
        foreach (var implementation in implementations)
        {
            if (implementation.Assembly != assembly || implementation.IsAbstract || implementation.IsInterface ||
                !capabilityTypes.Any(capability => capability.IsAssignableFrom(implementation)))
            {
                throw new InvalidDataException(
                    $"Plugin '{pluginId}' declared an invalid capability implementation '{implementation.FullName}'.");
            }
        }
    }

    private static HatbandPlugin CreatePlugin(Type pluginType)
    {
        return Activator.CreateInstance(pluginType) as HatbandPlugin
            ?? throw new InvalidDataException($"Plugin type '{pluginType.FullName}' needs a public parameterless constructor.");
    }

    private static void ValidateManifest(PluginManifest manifest)
    {
        ValidatePluginId(manifest.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.EntryAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.EntryType);
        if (!Version.TryParse(manifest.Version, out _))
        {
            throw new InvalidDataException("The plugin version must use a numeric version format.");
        }

        if (manifest.ApiVersion != CurrentApiVersion)
        {
            throw new InvalidDataException(
                $"Plugin API version {manifest.ApiVersion} is unsupported. Expected {CurrentApiVersion}; rebuild this plugin with the current Hatband SDK.");
        }
    }

    private static void ValidatePluginId(string pluginId)
    {
        try
        {
            GameSourceIds.Validate(pluginId, nameof(pluginId));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Plugin IDs must use lowercase letters, digits, dot, dash, or underscore.", exception);
        }
    }

    private static string ResolvePluginPath(string pluginDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("Plugin paths must be relative to the plugin directory.");
        }

        var directoryPath = Path.GetFullPath(pluginDirectory);
        var filePath = Path.GetFullPath(Path.Combine(directoryPath, relativePath));
        var directoryPrefix = directoryPath.EndsWith(Path.DirectorySeparatorChar)
            ? directoryPath
            : directoryPath + Path.DirectorySeparatorChar;
        if (!filePath.StartsWith(directoryPrefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidDataException("Plugin paths must stay inside the plugin directory.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The plugin entry assembly was not found.", filePath);
        }

        return filePath;
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
