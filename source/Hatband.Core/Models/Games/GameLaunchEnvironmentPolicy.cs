namespace Hatband.Core.Models.Games;

public static class GameLaunchEnvironmentPolicy
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PROTONPATH", "WINEPREFIX", "GAMEID", "UMU_ID", "STORE", "PROTON_VERB", "UMU_LOG",
        "UMU_USE_STEAM", "HATBAND_GAME_ID", "STEAM_COMPAT_APP_ID", "STEAM_COMPAT_CLIENT_INSTALL_PATH",
        "STEAM_COMPAT_LIBRARY_PATHS", "STEAM_COMPAT_DATA_PATH", "STEAM_COMPAT_SHADER_PATH",
        "STEAM_COMPAT_MOUNTS", "SteamAppId", "SteamGameId"
    };

    public static bool IsValidVariableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name[0] != '_' && !char.IsAsciiLetter(name[0]))
        {
            return false;
        }

        return name.Skip(1).All(character => character == '_' || char.IsAsciiLetterOrDigit(character));
    }

    public static bool IsReservedVariableName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ReservedNames.Contains(name) || name.StartsWith("STEAM_COMPAT_", StringComparison.OrdinalIgnoreCase);
    }

    public static void ValidateUserVariables(IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, value) in variables)
        {
            if (!IsValidVariableName(name))
            {
                throw new ArgumentException("Environment variable names must start with an ASCII letter or underscore and contain only ASCII letters, digits, or underscores.", nameof(variables));
            }

            if (value is null || value.Contains('\0'))
            {
                throw new ArgumentException($"Environment variable '{name}' has an invalid value.", nameof(variables));
            }

            if (IsReservedVariableName(name))
            {
                throw new ArgumentException($"Environment variable '{name}' is reserved by the game runtime.", nameof(variables));
            }

            if (!names.Add(name))
            {
                throw new ArgumentException($"Environment variable '{name}' is specified more than once.", nameof(variables));
            }
        }
    }
}
