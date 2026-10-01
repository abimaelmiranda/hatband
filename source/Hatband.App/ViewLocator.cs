using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Hatband.App.ViewModels;

namespace Hatband.App;

/// <summary>
/// Locates a view by matching its folder namespace and name to a view model.
/// </summary>
[RequiresUnreferencedCode(
    "The view locator discovers views using reflection, which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public sealed class ViewLocator : IDataTemplate
{
    private static readonly Type[] Views = typeof(ViewLocator).Assembly.GetTypes()
        .Where(type =>
            !type.IsAbstract &&
            typeof(Control).IsAssignableFrom(type) &&
            type.Namespace is not null &&
            (type.Namespace == "Hatband.App.Views" ||
             type.Namespace.StartsWith("Hatband.App.Views.", StringComparison.Ordinal)))
        .ToArray();

    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var viewModelName = param.GetType().Name;
        var viewName = viewModelName.EndsWith("ViewModel", StringComparison.Ordinal)
            ? viewModelName[..^"ViewModel".Length]
            : viewModelName;
        var viewType = Views.FirstOrDefault(type => type.Name == viewName);

        if (viewType is not null && Activator.CreateInstance(viewType) is Control view)
        {
            return view;
        }

        return new TextBlock { Text = $"View not found for {param.GetType().Name}" };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
