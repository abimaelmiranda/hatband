using System.Windows.Input;
using Avalonia.Controls;

namespace Hatband.App.Views.Navigation;

internal static class NavigationCommandExecutor
{
    public static bool TryExecute(Control target, ICommand command)
    {
        if (!target.IsEffectivelyEnabled || !command.CanExecute(null))
        {
            return false;
        }

        command.Execute(null);
        return true;
    }
}
