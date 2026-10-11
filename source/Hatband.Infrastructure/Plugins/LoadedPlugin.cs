using System.Reflection;
using Hatband.SDK.Plugins;

namespace Hatband.Infrastructure.Plugins;

public sealed record LoadedPlugin(string Id, string Name, string Version, bool IsExternal, Assembly Assembly);
