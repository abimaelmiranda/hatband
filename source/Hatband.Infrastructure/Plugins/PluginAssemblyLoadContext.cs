using System.Reflection;
using System.Runtime.Loader;
using Hatband.Core.Models.Games;
using Hatband.SDK.Plugins;

namespace Hatband.Infrastructure.Plugins;

internal sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly IReadOnlyDictionary<string, Assembly> _sharedAssemblies;

    public PluginAssemblyLoadContext(string entryAssemblyPath, IEnumerable<Assembly> sharedAssemblies)
        : base(Path.GetFileNameWithoutExtension(entryAssemblyPath), isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(entryAssemblyPath);
        _sharedAssemblies = sharedAssemblies
            .Append(typeof(HatbandPlugin).Assembly)
            .Append(typeof(Game).Assembly)
            .Distinct()
            .ToDictionary(GetAssemblySimpleName, StringComparer.Ordinal);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsHostContract(assemblyName))
        {
            return assemblyName.Name is not null && _sharedAssemblies.TryGetValue(assemblyName.Name, out var assembly)
                ? assembly
                : null;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
    }

    private bool IsHostContract(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        return name is not null && (_sharedAssemblies.ContainsKey(name) ||
               name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) ||
               name.StartsWith("Avalonia", StringComparison.Ordinal) ||
               name.StartsWith("CommunityToolkit", StringComparison.Ordinal));
    }

    private static string GetAssemblySimpleName(Assembly assembly)
    {
        return assembly.GetName().Name
            ?? throw new InvalidOperationException($"Shared assembly '{assembly.FullName}' has no simple name.");
    }
}
