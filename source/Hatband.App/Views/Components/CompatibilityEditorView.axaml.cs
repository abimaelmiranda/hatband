using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AppResources = Hatband.App.Localization.Resources;
using Hatband.App.ViewModels;
using Hatband.App.Views;

namespace Hatband.App.Views.Components;

public partial class CompatibilityEditorView : UserControl
{
    public CompatibilityEditorView()
    {
        InitializeComponent();
    }

    private void OnNativeClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as CompatibilityEditorViewModel;
        if (viewModel is not null)
        {
            viewModel.UseProton = false;
            viewModel.ErrorMessage = null;
        }
    }

    private void OnProtonClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as CompatibilityEditorViewModel;
        if (viewModel is not null)
        {
            viewModel.UseProton = true;
            viewModel.ErrorMessage = null;
        }
    }

    private void OnManagedPrefixClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as CompatibilityEditorViewModel;
        if (viewModel is not null)
        {
            viewModel.UseManagedPrefix = true;
            viewModel.ErrorMessage = null;
        }
    }

    private void OnCustomPrefixClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as CompatibilityEditorViewModel;
        if (viewModel is not null)
        {
            viewModel.UseManagedPrefix = false;
            viewModel.ErrorMessage = null;
        }
    }

    private async void OnChoosePrefixFolderClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as CompatibilityEditorViewModel;
        if (viewModel is null)
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            viewModel.ErrorMessage = AppResources.FilePickerError;
            return;
        }

        try
        {
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = AppResources.SelectGamePrefixFolder,
                AllowMultiple = false
            });
            if (folders.Count == 0)
            {
                Dispatcher.UIThread.Post(() => DirectionalFocusNavigator.Focus(PrefixPathTextBox));
                return;
            }

            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                viewModel.ErrorMessage = AppResources.ChooseLocalFolder;
                return;
            }

            viewModel.CustomPrefixPath = path;
            viewModel.ErrorMessage = null;
            Dispatcher.UIThread.Post(() => DirectionalFocusNavigator.Focus(PrefixPathTextBox));
        }
        catch (Exception exception)
        {
            viewModel.ErrorMessage = string.Format(CultureInfo.CurrentCulture, AppResources.SelectFolderError, exception.Message);
        }
    }
}
