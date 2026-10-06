using Hatband.App.Navigation;

namespace Hatband.App.ViewModels.Navigation;

/// <summary>A semantic navigation command and its localized description for the active view.</summary>
public sealed record InputHint(NavigationAction Action, string Description);
