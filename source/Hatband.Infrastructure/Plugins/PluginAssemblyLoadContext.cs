using System.Reflection;
using System.Runtime.Loader;
using Hatband.Core.Models.Games;
using Hatband.SDK.Plugins;

namespace Hatband.Infrastructure.Plugins;

internal sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginAssemblyLoadContext(string entryAssemblyPath)
        : base(Path.GetFileNameWithoutExtension(entryAssemblyPath), isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(entryAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsHostContract(assemblyName))
        {
            return null;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
    }

    private static bool IsHostContract(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        return string.Equals(name, typeof(HatbandPlugin).Assembly.GetName().Name, StringComparison.Ordinal) ||
               string.Equals(name, typeof(Game).Assembly.GetName().Name, StringComparison.Ordinal) ||
               name?.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) == true;
    }
}
