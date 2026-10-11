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
    private readonly List<LoadedPlugin> _plugins = [];
    private readonly List<PluginLoadIssue> _issues = [];
    private readonly HashSet<string> _pluginIds = new(StringComparer.Ordinal);
    private readonly List<PluginAssemblyLoadContext> _loadContexts = [];

    public PluginCatalog LoadAndRegister(
        IServiceCollection services,
        string pluginDirectory,
        params Assembly[] builtInAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        foreach (var assembly in builtInAssemblies)
        {
            LoadBuiltInPlugins(services, assembly);
        }

        LoadExternalPlugins(services, pluginDirectory);
        services.AddSingleton(new PluginCatalog(_plugins, _issues));
        return new PluginCatalog(_plugins, _issues);
    }

    private void LoadBuiltInPlugins(IServiceCollection services, Assembly assembly)
    {
        foreach (var pluginType in GetPluginTypes(assembly))
        {
            var plugin = CreatePlugin(pluginType);
            RegisterPlugin(services, plugin, assembly, isExternal: false, plugin.Name, plugin.Version);
        }
    }

    private void LoadExternalPlugins(IServiceCollection services, string pluginDirectory)
    {
        Directory.CreateDirectory(pluginDirectory);
        foreach (var directory in Directory.EnumerateDirectories(pluginDirectory).Order(StringComparer.Ordinal))
        {
            LoadExternalPlugin(services, directory);
        }
    }

    private void LoadExternalPlugin(IServiceCollection services, string pluginDirectory)
    {
        PluginAssemblyLoadContext? loadContext = null;
        PluginManifest? manifest = null;
        try
        {
            var manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
            manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), ManifestJsonOptions)
                ?? throw new InvalidDataException("The plugin manifest is empty.");
            ValidateManifest(manifest);

            var assemblyPath = ResolvePluginPath(pluginDirectory, manifest.EntryAssembly);
            loadContext = new PluginAssemblyLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var pluginType = assembly.GetType(manifest.EntryType, throwOnError: true, ignoreCase: false)!;
            if (!typeof(HatbandPlugin).IsAssignableFrom(pluginType) || pluginType.IsAbstract)
            {
                throw new InvalidDataException($"Entry type '{manifest.EntryType}' must derive from HatbandPlugin.");
            }

            var plugin = CreatePlugin(pluginType);
            if (!string.Equals(plugin.Id, manifest.Id, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The plugin ID does not match the manifest ID.");
            }

            RegisterPlugin(services, plugin, assembly, isExternal: true, manifest.Name, manifest.Version);
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
        RegisterPluginCapabilities(pluginServices, plugin, assembly, isExternal);
        foreach (var descriptor in pluginServices)
        {
            services.Add(descriptor);
        }

        _pluginIds.Add(plugin.Id);
        _plugins.Add(new LoadedPlugin(plugin.Id, name, version, isExternal, assembly));
    }

    private static void RegisterPluginCapabilities(
        IServiceCollection services,
        HatbandPlugin plugin,
        Assembly assembly,
        bool isExternal)
    {
        var capabilityImplementations = plugin.CapabilityImplementations.Count > 0
            ? plugin.CapabilityImplementations
            : isExternal
                ? FindCapabilityImplementations(assembly)
                : [];
        if (capabilityImplementations.Count == 0)
        {
            return;
        }

        var capabilityTypes = PluginCapabilityTypes.All;
        var implementations = capabilityImplementations.ToHashSet();
        ValidateCapabilityImplementations(assembly, implementations, capabilityTypes, plugin.Id);
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.Where(implementations.Contains))
            .AsSelfWithInterfaces(contract => capabilityTypes.Contains(contract))
            .WithSingletonLifetime());
    }

    private static IReadOnlyCollection<Type> FindCapabilityImplementations(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           PluginCapabilityTypes.All.Any(capability => capability.IsAssignableFrom(type)))
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

    private static IReadOnlyList<Type> GetPluginTypes(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => typeof(HatbandPlugin).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            .ToArray();
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
            throw new InvalidDataException($"Plugin API version {manifest.ApiVersion} is unsupported. Expected {CurrentApiVersion}.");
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
