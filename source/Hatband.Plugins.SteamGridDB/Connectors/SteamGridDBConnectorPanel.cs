using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Hatband.SDK.UI.Controls;
using Hatband.Core.Abstractions.Settings;
using Hatband.Plugins.SteamGridDB.Authentication;
using Hatband.Plugins.SteamGridDB.Localization;
using Hatband.Plugins.SteamGridDB.Settings;
using Hatband.SDK.UI.Connectors;

namespace Hatband.Plugins.SteamGridDB.Connectors;

public sealed class SteamGridDBConnectorPanel : PluginPanel
{
    private readonly SteamGridDBConnectorViewModel viewModel;
    private readonly SettingsComboBox modePicker;
    private readonly TextBox apiKeyBox;
    private readonly Button saveKeyButton;
    private readonly Button deleteKeyButton;
    private readonly TextBlock keyStateText;
    private readonly TextBlock statusText;
    private bool isUpdating;
    private Control? lastFocusedControl;

    public SteamGridDBConnectorPanel(ISettingsApi settingsApi, SteamGridDBCredentialService credentials)
    {
        viewModel = new SteamGridDBConnectorViewModel(settingsApi, credentials);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        AddHandler(InputElement.GotFocusEvent, OnDescendantGotFocus, RoutingStrategies.Bubble);

        modePicker = new SettingsComboBox
        {
            ItemsSource = new[] { SteamGridDBResources.ScraperMode, SteamGridDBResources.ApiMode },
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 180
        };
        modePicker.SelectionCommitted += async (_, _) =>
        {
            if (!isUpdating && modePicker.SelectedIndex is 0 or 1)
            {
                viewModel.SelectedMode = modePicker.SelectedIndex == 0 ? SteamGridDBMode.Scraper : SteamGridDBMode.ApiKey;
                await viewModel.SaveModeAsync();
            }
        };

        apiKeyBox = new TextBox
        {
            PasswordChar = '●',
            PlaceholderText = SteamGridDBResources.KeyWatermark,
            MinWidth = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        apiKeyBox.TextChanged += (_, _) =>
        {
            if (!isUpdating)
            {
                viewModel.ApiKey = apiKeyBox.Text;
            }
        };
        saveKeyButton = new Button { Content = SteamGridDBResources.SaveKey, HorizontalAlignment = HorizontalAlignment.Left };
        saveKeyButton.Click += async (_, _) => await viewModel.SaveApiKeyAsync();
        deleteKeyButton = new Button { Content = SteamGridDBResources.DeleteKey, HorizontalAlignment = HorizontalAlignment.Left };
        deleteKeyButton.Click += async (_, _) => await viewModel.DeleteApiKeyAsync();

        keyStateText = new TextBlock { TextWrapping = TextWrapping.Wrap };
        statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#AEBBC5"))
        };
        Content = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = SteamGridDBResources.ModeTitle, FontWeight = FontWeight.SemiBold },
                modePicker,
                new TextBlock
                {
                    Text = SteamGridDBResources.ModeDescription,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#AEBBC5"))
                },
                new TextBlock { Text = SteamGridDBResources.KeyTitle, FontWeight = FontWeight.SemiBold },
                keyStateText,
                apiKeyBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { saveKeyButton, deleteKeyButton }
                },
                statusText
            }
        };
        UpdateView();
    }

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        await viewModel.ActivateAsync(cancellationToken);
        UpdateView();
    }

    public override bool FocusInitial()
    {
        if (lastFocusedControl is { } focused && IsFocusable(focused) && focused.Focus())
        {
            return true;
        }

        Control target = viewModel.Mode == SteamGridDBMode.ApiKey ? apiKeyBox : modePicker;
        return IsFocusable(target) && target.Focus();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args) => UpdateView();

    private void OnDescendantGotFocus(object? sender, RoutedEventArgs args)
    {
        if (args.Source is Control control && control.GetVisualAncestors().Any(ancestor => ReferenceEquals(ancestor, this)))
        {
            lastFocusedControl = control;
        }
    }

    private static bool IsFocusable(Control control) =>
        control is { Focusable: true, IsEffectivelyVisible: true, IsEffectivelyEnabled: true };

    private void UpdateView()
    {
        isUpdating = true;
        modePicker.SelectedIndex = viewModel.SelectedMode == SteamGridDBMode.Scraper ? 0 : 1;
        apiKeyBox.Text = viewModel.ApiKey;
        keyStateText.Text = viewModel.IsKeyConfigured
            ? SteamGridDBResources.KeyConfigured
            : SteamGridDBResources.KeyMissing;
        statusText.Text = viewModel.Status;
        modePicker.IsEnabled = !viewModel.IsBusy;
        apiKeyBox.IsEnabled = !viewModel.IsBusy;
        saveKeyButton.IsEnabled = !viewModel.IsBusy;
        deleteKeyButton.IsEnabled = !viewModel.IsBusy && viewModel.IsKeyConfigured;
        isUpdating = false;
    }
}
