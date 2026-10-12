using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Hatband.Core.Abstractions.Settings;
using Hatband.Plugins.Steam.Localization;
using Hatband.Plugins.Steam.Store;
using Hatband.SDK.UI.Authentication;
using Hatband.SDK.UI.Connectors;

namespace Hatband.Plugins.Steam.Connectors;

public sealed class SteamConnectorPanel : PluginPanel
{
    private readonly SteamConnectorViewModel viewModel;
    private readonly Button connectButton;
    private readonly Button cancelLoginButton;
    private readonly Button syncButton;
    private readonly Button disconnectButton;
    private readonly StackPanel disconnectedPanel;
    private readonly StackPanel connectedPanel;
    private readonly TextBlock accountNameText;
    private readonly TextBlock statusText;
    private readonly TextBlock settingsStatusText;
    private readonly QrCodeImage qrCodeImage;
    private readonly CheckBox silentModeCheckBox;
    private bool isApplyingSettings;
    private Control? lastFocusedControl;

    public SteamConnectorPanel(SteamStoreIntegration steamStore, ISettingsApi settingsApi, ConnectorUiContext context)
    {
        viewModel = new SteamConnectorViewModel(steamStore, settingsApi, context);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        AddHandler(InputElement.GotFocusEvent, OnDescendantGotFocus, RoutingStrategies.Bubble);

        connectButton = new Button
        {
            Content = SteamResources.ConnectSteamAccount,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        connectButton.Click += async (_, _) => await viewModel.ConnectAsync();
        cancelLoginButton = new Button
        {
            Content = SteamResources.CancelConnection,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = false
        };
        cancelLoginButton.Click += (_, _) => viewModel.CancelLogin();
        syncButton = new Button
        {
            Content = SteamResources.SyncLibrary,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        syncButton.Click += async (_, _) => await viewModel.SyncAsync();
        disconnectButton = new Button
        {
            Content = SteamResources.DisconnectAccount,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        disconnectButton.Click += async (_, _) => await viewModel.DisconnectAsync();
        qrCodeImage = new QrCodeImage
        {
            Width = 180,
            Height = 180,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        silentModeCheckBox = new CheckBox { Content = SteamResources.SilentMode };
        silentModeCheckBox.IsCheckedChanged += async (_, _) =>
        {
            if (!isApplyingSettings && silentModeCheckBox.IsChecked is bool enabled)
            {
                await viewModel.SetSilentModeAsync(enabled);
            }
        };
        disconnectedPanel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = SteamResources.ConnectDescription, TextWrapping = TextWrapping.Wrap },
                connectButton,
                new TextBlock { Text = SteamResources.ScanQrDescription },
                qrCodeImage,
                cancelLoginButton
            }
        };
        connectedPanel = new StackPanel { Spacing = 10, IsVisible = false };
        accountNameText = new TextBlock { FontWeight = FontWeight.SemiBold };
        connectedPanel.Children.Add(new TextBlock { Text = SteamResources.Connected, Foreground = Brushes.LightGreen });
        connectedPanel.Children.Add(accountNameText);
        connectedPanel.Children.Add(syncButton);
        connectedPanel.Children.Add(disconnectButton);
        statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#AEBBC5"))
        };
        settingsStatusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#E7A27D"))
        };
        Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                disconnectedPanel,
                connectedPanel,
                statusText,
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        silentModeCheckBox,
                        new TextBlock
                        {
                            Text = SteamResources.SilentModeDescription,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.Parse("#AEBBC5"))
                        },
                        settingsStatusText
                    }
                }
            }
        };

        UpdateView();
    }

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await viewModel.ActivateAsync(cancellationToken);
        UpdateView();
    }

    public override void Deactivate() => viewModel.Deactivate();

    public override bool FocusInitial()
    {
        if (lastFocusedControl is { } focusedControl && IsFocusableControl(focusedControl) && focusedControl.Focus())
        {
            return true;
        }

        var target = viewModel.IsLoginPending
            ? cancelLoginButton
            : viewModel.IsSyncPending
                ? silentModeCheckBox
                : viewModel.IsConnected
                    ? syncButton
                    : connectButton;
        return IsFocusableControl(target) && target.Focus();
    }

    public override bool TryHandleBack()
    {
        if (!viewModel.IsLoginPending)
        {
            return false;
        }

        viewModel.CancelLogin();
        return true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args) => UpdateView();

    private void OnDescendantGotFocus(object? sender, RoutedEventArgs args)
    {
        if (args.Source is Control control && IsDescendant(control))
        {
            lastFocusedControl = control;
        }
    }

    private bool IsDescendant(Control control) =>
        control.GetVisualAncestors().Any(ancestor => ReferenceEquals(ancestor, this));

    private static bool IsFocusableControl(Control? control) =>
        control is { Focusable: true, IsEffectivelyVisible: true, IsEffectivelyEnabled: true };

    private void UpdateView()
    {
        disconnectedPanel.IsVisible = !viewModel.IsConnected;
        connectedPanel.IsVisible = viewModel.IsConnected;
        connectButton.IsEnabled = viewModel.CanConnect;
        cancelLoginButton.IsVisible = viewModel.IsLoginPending;
        if (qrCodeImage.Uri != viewModel.QrCodeUri)
        {
            qrCodeImage.Uri = viewModel.QrCodeUri;
        }
        qrCodeImage.IsVisible = viewModel.QrCodeUri is not null;
        syncButton.IsEnabled = viewModel.CanSync;
        disconnectButton.IsEnabled = viewModel.CanDisconnect;
        accountNameText.Text = viewModel.ActiveAccountName;
        statusText.Text = viewModel.ConnectionStatus;
        isApplyingSettings = true;
        silentModeCheckBox.IsChecked = viewModel.IsSilentModeEnabled;
        isApplyingSettings = false;
        settingsStatusText.Text = viewModel.SettingsStatus;
    }
}
