using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Hatband.App.ViewModels;
using AppResources = Hatband.App.Localization.Resources;

namespace Hatband.App.Views.Screens;

public partial class AddGameScreenView : UserControl
{
    public AddGameScreenView()
    {
        InitializeComponent();
    }

    public TextBox GameNameInput => GameNameBox;

    private async void OnImportExecutableClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            viewModel.StatusMessage = AppResources.FilePickerError;
            return;
        }

        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = AppResources.SelectExecutableTitle,
                AllowMultiple = false
            });

            if (files.Count == 0)
            {
                return;
            }

            var executablePath = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                viewModel.StatusMessage = AppResources.ChooseLocalFile;
                return;
            }

            viewModel.PrepareGameFromExecutable(executablePath);
            GameNameBox.Focus();
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = string.Format(AppResources.SelectExecutableError, exception.Message);
        }
    }
}
