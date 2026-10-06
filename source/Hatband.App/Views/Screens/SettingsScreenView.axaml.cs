using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Hatband.App.Navigation;
using Hatband.App.ViewModels.Navigation;
using Hatband.App.ViewModels.Settings;
using Hatband.App.Views.Components;
using Hatband.App.Views.Navigation;

namespace Hatband.App.Views.Screens;

public partial class SettingsScreenView : FullScreenView
{
    public SettingsScreenView()
    {
        InitializeComponent();
    }

    /// <summary>Reports whether keyboard focus is on a settings section item.</summary>
    public bool IsSectionNavigationFocused => SettingsSectionList.GetVisualDescendants()
        .OfType<Button>()
        .Any(button => button.IsFocused);

    /// <summary>Moves focus to the selected editor field or compatibility action when settings are loaded.</summary>
    public void FocusSelectedSettingField()
    {
        if (DataContext is not SettingsScreenViewModel viewModel || viewModel.IsSettingsLoading)
        {
            return;
        }

        if (viewModel.IsCompatibilitySettingsSection)
        {
            FocusSelectedCompatibilityField(viewModel.SelectedSettingsFieldIndex);
            return;
        }

        SettingsSectionEditor.FocusField(viewModel.SelectedSettingsFieldIndex);
    }

    /// <summary>Moves focus to the available Steam connect or sync action.</summary>
    public void FocusConnectorsPrimaryAction() => SteamConnectorSettings.FocusPrimaryAction();

    /// <summary>Selects and focuses the compatibility catalog refresh action.</summary>
    public void FocusCompatibilityRefreshButton()
    {
        if (DataContext is not SettingsScreenViewModel viewModel)
        {
            return;
        }

        var protonManagement = viewModel.ProtonManagement;
        var refreshButtonIndex = protonManagement.Providers.Count;
        viewModel.SelectSettingsField(refreshButtonIndex);
        FocusSelectedCompatibilityField(refreshButtonIndex);
    }

    /// <summary>Opens the selected settings field when it is a combo box.</summary>
    public bool OpenSelectedComboBox()
    {
        if (DataContext is not SettingsScreenViewModel viewModel || viewModel.IsCompatibilitySettingsSection)
        {
            return false;
        }

        return SettingsSectionEditor.OpenSelectedComboBox(viewModel.SelectedSettingsFieldIndex);
    }

    /// <summary>Reports whether a settings editor combo box is currently expanded.</summary>
    public bool HasOpenComboBox() => SettingsSectionEditor.HasOpenComboBox();

    /// <summary>Closes the expanded settings combo box, if there is one.</summary>
    public bool CloseOpenComboBox()
    {
        var comboBox = SettingsSectionEditor.GetVisualDescendants()
            .OfType<ComboBox>()
            .FirstOrDefault(item => item.IsDropDownOpen);
        if (comboBox is null)
        {
            return false;
        }

        comboBox.IsDropDownOpen = false;
        return true;
    }

    /// <summary>Moves keyboard focus back to the selected section in the navigation list.</summary>
    public void FocusSelectedSection()
    {
        FindSelectedSectionItem()?.FocusItem();
    }

    /// <summary>Uses a pending Steam action request or the selected section as the initial focus target.</summary>
    protected override Control? GetInitialFocusTarget()
    {
        var steamAction = SteamConnectorSettings.GetPrimaryActionControl();
        if (steamAction is not null &&
            DataContext is SettingsScreenViewModel viewModel &&
            viewModel.ConsumeConnectorsPrimaryActionFocusRequest())
        {
            return steamAction;
        }

        return FindSelectedSectionItem()?
            .GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault();
    }

    private ConsoleNavigationItemView? FindSelectedSectionItem()
    {
        if (DataContext is not SettingsScreenViewModel viewModel)
        {
            return null;
        }

        return SettingsSectionList.GetVisualDescendants()
            .OfType<ConsoleNavigationItemView>()
            .FirstOrDefault(item => ReferenceEquals(item.DataContext, viewModel.SelectedSettingsSection));
    }

    /// <summary>
    /// Handles settings confirmation and delegates directional movement to the shared full-screen
    /// focus navigator.
    /// </summary>
    public override NavigationActionHandling HandleNavigationAction(NavigationAction action, KeyEventArgs originalEvent)
    {
        if (action == NavigationAction.Back)
        {
            return TryHandleBack(originalEvent)
                ? NavigationActionHandling.Handled
                : NavigationActionHandling.Unhandled;
        }

        var viewModel = DataContext as SettingsScreenViewModel;
        if (viewModel is null)
        {
            return NavigationActionHandling.Unhandled;
        }

        if (action == NavigationAction.Confirm &&
            viewModel.IsSettingsContentActive &&
            viewModel.IsCompatibilitySettingsSection)
        {
            var focusedProviderButton = FindFocusedProviderButton();
            var focusedProvider = focusedProviderButton?.DataContext as ProtonReleaseProviderViewModel;
            if (focusedProvider is not null)
            {
                originalEvent.Handled = true;
                focusedProvider.OpenVersionsCommand.Execute(null);
                return NavigationActionHandling.Handled;
            }
        }

        if (action != NavigationAction.Confirm)
        {
            return base.HandleNavigationAction(action, originalEvent);
        }

        if (viewModel.IsSettingsContentActive &&
            viewModel.IsSettingsDataSectionSelected &&
            !HasOpenComboBox() &&
            OpenSelectedComboBox())
        {
            originalEvent.Handled = true;
            return NavigationActionHandling.Handled;
        }

        if (!viewModel.IsSettingsContentActive && IsSectionNavigationFocused)
        {
            viewModel.ActivateSettingsSection();
            originalEvent.Handled = true;
            Dispatcher.UIThread.Post(() =>
            {
                if (!viewModel.IsSettingsContentActive ||
                    !ReferenceEquals(DataContext, viewModel))
                {
                    return;
                }

                if (viewModel.IsSettingsDataSectionSelected)
                {
                    FocusSelectedSettingField();
                }
                else if (viewModel.IsCompatibilitySettingsSection)
                {
                    FocusCompatibilityRefreshButton();
                }
            }, DispatcherPriority.Loaded);
            return NavigationActionHandling.Handled;
        }

        return NavigationActionHandling.Native;
    }

    /// <summary>Closes an expanded combo box or returns focus from content to the section list.</summary>
    public override bool TryHandleBack(KeyEventArgs originalEvent)
    {
        if (CloseOpenComboBox())
        {
            originalEvent.Handled = true;
            return true;
        }

        if (DataContext is not SettingsScreenViewModel viewModel || !viewModel.IsSettingsContentActive)
        {
            return false;
        }

        viewModel.DeactivateSettingsContent();
        FocusSelectedSection();
        originalEvent.Handled = true;
        return true;
    }

    private void OnSettingsSectionClick(object? sender, EventArgs e)
    {
        if (DataContext is not SettingsScreenViewModel viewModel ||
            sender is not ConsoleNavigationItemView { DataContext: SettingsSectionOptionViewModel section })
        {
            return;
        }

        viewModel.SelectSettingsSection(section);
    }

    private void OnSettingsSectionFocusEntered(object? sender, EventArgs e)
    {
        if (DataContext is SettingsScreenViewModel viewModel &&
            sender is ConsoleNavigationItemView { DataContext: SettingsSectionOptionViewModel section })
        {
            viewModel.SelectSettingsSection(section);
        }
    }

    private void OnProtonRefreshGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsScreenViewModel viewModel && viewModel.IsCompatibilitySettingsSection)
        {
            var protonManagement = viewModel.ProtonManagement;
            viewModel.SelectSettingsField(protonManagement.Providers.Count);
        }
    }

    private void OnProtonProviderGotFocus(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as SettingsScreenViewModel;
        var button = sender as Button;
        var provider = button?.DataContext as ProtonReleaseProviderViewModel;
        if (viewModel is null || provider is null || !viewModel.IsCompatibilitySettingsSection)
        {
            return;
        }

        var providerIndex = viewModel.ProtonManagement.Providers.IndexOf(provider);
        if (providerIndex < 0)
        {
            return;
        }

        viewModel.ProtonManagement.SelectProvider(provider);
        viewModel.SelectSettingsField(providerIndex);
    }

    private void FocusSelectedCompatibilityField(int fieldIndex)
    {
        if (DataContext is not SettingsScreenViewModel viewModel)
        {
            return;
        }

        var protonManagement = viewModel.ProtonManagement;
        var controls = ProtonManagementPanel.GetVisualDescendants().OfType<Control>();
        var providerButtons = new Dictionary<ProtonReleaseProviderViewModel, Button>();

        foreach (var control in controls)
        {
            var button = control as Button;
            var provider = button?.DataContext as ProtonReleaseProviderViewModel;
            if (button is not null && provider is not null && IsFocusable(button))
            {
                providerButtons.TryAdd(provider, button);
            }
        }

        var focusableControls = new List<Control>();
        foreach (var provider in protonManagement.Providers)
        {
            if (providerButtons.TryGetValue(provider, out var providerButton))
            {
                focusableControls.Add(providerButton);
            }
        }

        if (IsFocusable(ProtonRefreshButton))
        {
            focusableControls.Add(ProtonRefreshButton);
        }

        if ((uint)fieldIndex >= (uint)focusableControls.Count)
        {
            return;
        }

        DirectionalFocusNavigator.Focus(focusableControls[fieldIndex]);
    }

    private Button? FindFocusedProviderButton()
    {
        foreach (var button in ProtonManagementPanel.GetVisualDescendants().OfType<Button>())
        {
            var provider = button.DataContext as ProtonReleaseProviderViewModel;
            if (provider is not null && button.IsFocused)
            {
                return button;
            }
        }

        return null;
    }

    private static bool IsFocusable(Control control) =>
        control.IsVisible && control.IsEnabled && control.Focusable && control.IsTabStop;
}
