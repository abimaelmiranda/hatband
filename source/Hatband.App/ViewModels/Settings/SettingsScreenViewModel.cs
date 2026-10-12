using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.App.Services;
using Hatband.App.Navigation;
using Hatband.App.ViewModels.Navigation;
using Hatband.Core.Abstractions.Authentication;
using Hatband.Core.Abstractions.Games;
using Hatband.Core.Abstractions.Services;
using Hatband.Core.Abstractions.Settings;
using Hatband.Core.Models.Settings;
using Hatband.Infrastructure.Persistence;
using Hatband.SDK.UI.Connectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Hatband.App.ViewModels.Settings;

/// <summary>
/// Owns loaded settings, section navigation, field editing, and connector account state.
/// </summary>
public partial class SettingsScreenViewModel : ScreenViewModel
{
    private readonly ISettingsApi _settingsApi;
    private readonly IGameSourceCatalog _gameSourceCatalog;
    private readonly IDbContextFactory<HatbandDbContext> _dbContextFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly DateTimeDisplayFormatter _dateTimeDisplayFormatter;
    private readonly IReadOnlyList<IConnectorUiProvider> _connectorUiProviders;
    private readonly Dictionary<string, PluginPanel> _connectorPanels = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, IConnectorSessionProvider> _connectorSessionProviders;
    private readonly Dictionary<Type, object> _settingsByType = [];
    private readonly SemaphoreSlim _settingsSaveGate = new(1, 1);
    private ICommand? _refreshMetadataCommand;
    private bool _settingsLoaded;
    private bool _isUpdatingSelectedOptions;
    private bool _canRefreshMetadata = true;
    private bool _shouldFocusConnectorsPrimaryAction;
    private Func<string, CancellationToken, Task<int>>? _synchronizeLibrary;
    private bool _isLibrarySyncRunning;

    /// <summary>Indicates that settings sections are still loading or a failed load left them unavailable.</summary>
    [ObservableProperty]
    public partial bool IsSettingsLoading { get; set; } = true;

    /// <summary>Current language choice; changing it updates cached general settings and raises <see cref="LanguageChanged"/>.</summary>
    [ObservableProperty]
    public partial SettingsOptionViewModel? SelectedLanguageOption { get; set; }

    /// <summary>Current time-zone choice; changing it updates the shared formatter and raises <see cref="TimeZoneChanged"/>.</summary>
    [ObservableProperty]
    public partial SettingsOptionViewModel? SelectedTimeZoneOption { get; set; }

    [ObservableProperty]
    public partial bool IsClearingCache { get; set; }

    [ObservableProperty]
    public partial string? CacheCleanupMessage { get; set; }

    public bool HasCacheCleanupMessage =>
        IsGeneralSettingsSection && !string.IsNullOrWhiteSpace(CacheCleanupMessage);

    /// <summary>
    /// Creates the settings screen and its plugin supplied connector panels.
    /// </summary>
    public SettingsScreenViewModel(
        ISettingsApi settingsApi,
        IDbContextFactory<HatbandDbContext> dbContextFactory,
        IMemoryCache memoryCache,
        ProtonManagementViewModel protonManagement,
        DateTimeDisplayFormatter dateTimeDisplayFormatter,
        IEnumerable<IConnectorUiProvider> connectorUiProviders,
        IEnumerable<IConnectorSessionProvider> connectorSessionProviders,
        IGameSourceCatalog gameSourceCatalog)
    {
        ArgumentNullException.ThrowIfNull(settingsApi);
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(protonManagement);
        ArgumentNullException.ThrowIfNull(dateTimeDisplayFormatter);
        ArgumentNullException.ThrowIfNull(connectorUiProviders);
        ArgumentNullException.ThrowIfNull(connectorSessionProviders);
        ArgumentNullException.ThrowIfNull(gameSourceCatalog);

        _settingsApi = settingsApi;
        _gameSourceCatalog = gameSourceCatalog;
        _dbContextFactory = dbContextFactory;
        _memoryCache = memoryCache;
        _dateTimeDisplayFormatter = dateTimeDisplayFormatter;
        ProtonManagement = protonManagement;
        _connectorUiProviders = connectorUiProviders.OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        _connectorSessionProviders = connectorSessionProviders.ToDictionary(provider => provider.SourceId, StringComparer.Ordinal);
        Navigation = new SettingsNavigationViewModel(settingsApi.GetSections(), _connectorUiProviders.Count);
        Navigation.PropertyChanged += OnSettingsNavigationPropertyChanged;
        ProtonManagement.PropertyChanged += OnProtonManagementPropertyChanged;

        LanguageOptions = CreateLanguageOptions();
        TimeZoneOptions = CreateTimeZoneOptions();
    }

    /// <summary>Raised after settings have been persisted successfully.</summary>
    public event EventHandler? SettingsSaved;

    /// <summary>Raised with a user-facing message when loading, saving, login, or sync fails.</summary>
    public event Action<string>? SettingsError;

    /// <summary>Raised after the selected language has updated the cached general settings.</summary>
    public event Action<string>? LanguageChanged;

    /// <summary>Raised after the shared date formatter has changed to the selected time zone.</summary>
    public event Action<string>? TimeZoneChanged;

    /// <summary>Raised with the saved controller legend style, including after settings initialize.</summary>
    public event Action<ControllerDisplayMode>? ControllerDisplayModeChanged;

    /// <summary>Raised with validated appearance settings after loading or editing, before saving.</summary>
    public event Action<AppearanceSettings>? AppearanceSettingsChanged;

    /// <summary>Owns section, field, tab, and focus-selection state for this screen.</summary>
    public SettingsNavigationViewModel Navigation { get; }

    /// <summary>Returns semantic navigation guidance while settings is the active screen.</summary>
    public override IReadOnlyList<InputHint> InputHints =>
    [
        new(NavigationAction.Up, Resources.InputHintNavigate),
        new(NavigationAction.Confirm, Resources.InputHintOpen),
        new(NavigationAction.Back, Resources.InputHintBack),
        new(NavigationAction.OpenMenu, Resources.InputHintMenu)
    ];

    /// <summary>Manages compatibility tool catalogs and installations for the compatibility section.</summary>
    public ProtonManagementViewModel ProtonManagement { get; }

    /// <summary>Available language choices, including any saved language not in the built-in list.</summary>
    public ObservableCollection<SettingsOptionViewModel> LanguageOptions { get; }

    /// <summary>Available time zones, including any saved time zone not on the current system.</summary>
    public ObservableCollection<SettingsOptionViewModel> TimeZoneOptions { get; }

    /// <summary>Settings sections shown in the navigation list.</summary>
    public ObservableCollection<SettingsSectionOptionViewModel> SettingsSections => Navigation.Sections;

    /// <summary>Index of the selected section in the settings navigation list.</summary>
    public int SelectedSettingsSectionIndex => Navigation.SelectedSectionIndex;

    /// <summary>Whether focus currently belongs to the selected section's content.</summary>
    public bool IsSettingsContentActive => Navigation.IsContentActive;

    /// <summary>Index of the selected editor field or compatibility action.</summary>
    public int SelectedSettingsFieldIndex => Navigation.SelectedFieldIndex;

    /// <summary>Number of navigable fields in the selected section.</summary>
    public int SelectedSettingsFieldCount => Navigation.SelectedFieldCount;

    /// <summary>Selected tab index for a tabbed settings section.</summary>
    public int SelectedSettingsTabIndex => Navigation.SelectedSettingsTabIndex;

    /// <summary>Whether the connector settings section is selected.</summary>
    public bool IsConnectorsSettingsSection => Navigation.IsConnectorsSection;

    /// <summary>Whether the Proton compatibility section is selected.</summary>
    public bool IsCompatibilitySettingsSection => Navigation.IsCompatibilitySection;

    /// <summary>Whether the selected section is backed by a registered settings object.</summary>
    public bool IsSettingsDataSectionSelected => Navigation.IsSettingsDataSection;

    /// <summary>Whether the general settings section is selected.</summary>
    public bool IsGeneralSettingsSection => Navigation.SelectedSection.Id == SettingsSectionOptionViewModel.GeneralSectionId;

    /// <summary>Whether the shared session currently permits metadata refresh.</summary>
    public bool CanRefreshMetadata => _canRefreshMetadata;

    /// <summary>Session-owned metadata refresh action configured during composition.</summary>
    public ICommand RefreshMetadataCommand => _refreshMetadataCommand
        ?? throw new InvalidOperationException("The metadata refresh command has not been configured.");

    /// <summary>The currently selected settings section.</summary>
    public SettingsSectionOptionViewModel SelectedSettingsSection => Navigation.SelectedSection;

    /// <summary>Gets the focus border for a settings field by its navigation index.</summary>
    public IBrush GetSettingsFieldBorderBrush(int index) => Navigation.GetFieldBorderBrush(index);

    /// <summary>Connector panels supplied by registered plugins.</summary>
    public IReadOnlyList<IConnectorUiProvider> ConnectorUiProviders => _connectorUiProviders;

    public bool IsLibrarySyncRunning => _isLibrarySyncRunning;

    /// <summary>
    /// Connects each registered connector to the shared library synchronization workflow.
    /// </summary>
    public void SetLibrarySyncCallback(Func<string, CancellationToken, Task<int>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _synchronizeLibrary = callback;
    }

    /// <summary>Updates connector synchronization availability from the shared library session.</summary>
    public void SetLibrarySyncRunning(bool value)
    {
        _isLibrarySyncRunning = value;
        OnPropertyChanged(nameof(IsLibrarySyncRunning));
    }

    public async Task<IReadOnlyList<string>> RestoreConnectorSessionsAsync(CancellationToken cancellationToken = default)
    {
        var restoredSourceIds = new List<string>();
        foreach (var connector in _connectorSessionProviders.Values.OrderBy(provider => provider.SourceId, StringComparer.Ordinal))
        {
            try
            {
                if (await connector.RestoreAsync(cancellationToken))
                {
                    restoredSourceIds.Add(connector.SourceId);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ReportConnectorRestoreError(connector.SourceId, exception);
            }
        }

        return restoredSourceIds;
    }

    public string GetConnectorDisplayName(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        return _gameSourceCatalog.GetDisplayName(sourceId);
    }

    public void ReportConnectorRestoreError(string sourceId, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentNullException.ThrowIfNull(exception);
        SettingsError?.Invoke(string.Format(
            CultureInfo.CurrentCulture,
            Resources.ConnectorLoginError,
            GetConnectorDisplayName(sourceId),
            exception.Message));
    }

    public PluginPanel CreateConnectorPanel(IConnectorUiProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (_connectorPanels.TryGetValue(provider.SourceId, out var existingPanel))
        {
            return existingPanel;
        }

        var panel = provider.CreatePanel(new ConnectorUiContext(
            provider.SourceId,
            async cancellationToken =>
            {
                var synchronize = _synchronizeLibrary
                    ?? throw new InvalidOperationException("The library synchronization callback has not been configured.");
                await synchronize(provider.SourceId, cancellationToken);
            }));
        _connectorPanels.Add(provider.SourceId, panel);
        return panel;
    }

    public void ReportConnectorPanelError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        SettingsError?.Invoke(exception.Message);
    }

    /// <summary>Supplies the session-owned metadata refresh command used by the general settings view.</summary>
    public void SetRefreshMetadataCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _refreshMetadataCommand = command;
        OnPropertyChanged(nameof(RefreshMetadataCommand));
    }

    /// <summary>Updates the metadata refresh button's enabled state from the shared library session.</summary>
    public void SetCanRefreshMetadata(bool value)
    {
        if (_canRefreshMetadata == value)
        {
            return;
        }

        _canRefreshMetadata = value;
        OnPropertyChanged(nameof(CanRefreshMetadata));
    }

    [RelayCommand]
    private async Task ClearCacheAsync(CancellationToken cancellationToken)
    {
        IsClearingCache = true;
        CacheCleanupMessage = null;

        try
        {
            // Accessing the dbContext here is intentional, as ICacheService should not provide a public way to clear the cache.

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var cacheKeys = await dbContext.CacheEntries
                .Select(entry => entry.Key)
                .ToArrayAsync(cancellationToken);
            var removedEntries = await dbContext.CacheEntries.ExecuteDeleteAsync(cancellationToken);
            foreach (var cacheKey in cacheKeys)
            {
                _memoryCache.Remove(cacheKey);
            }

            CacheCleanupMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resources.CacheCleanupComplete,
                removedEntries);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SettingsError?.Invoke(string.Format(
                CultureInfo.CurrentCulture,
                Resources.CacheCleanupError,
                exception.Message));
        }
        finally
        {
            IsClearingCache = false;
        }
    }

    partial void OnCacheCleanupMessageChanged(string? value) =>
        OnPropertyChanged(nameof(HasCacheCleanupMessage));

    /// <summary>
    /// Loads each registered settings section once, applies the saved time zone, and initializes
    /// the language, time zone, and connector controls. A load failure is reported through
    /// <see cref="SettingsError"/> and leaves settings unavailable through <see cref="GetSettings{TSettings}"/>.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_settingsLoaded)
        {
            return;
        }

        IsSettingsLoading = true;
        try
        {
            _settingsByType.Clear();
            foreach (var settingsSection in Navigation.Sections)
            {
                if (settingsSection.Descriptor is not { } descriptor)
                {
                    continue;
                }

                var settings = await _settingsApi.GetSectionAsync(descriptor, cancellationToken);
                if (settings is AppearanceSettings appearanceSettings)
                {
                    ValidateAppearanceSettings(appearanceSettings);
                }

                settingsSection.SetSettings(settings);
                _settingsByType.Add(descriptor.SettingsType, settings);
            }

            var generalSettings = GetSettings<GeneralSettings>();
            if (!Enum.IsDefined(generalSettings.ControllerDisplayMode))
            {
                throw new InvalidOperationException("The saved controller display mode is not supported.");
            }

            _dateTimeDisplayFormatter.SetTimeZone(generalSettings.TimeZoneId);
            TypographyScale.Apply(generalSettings.TextScalePercent);
            ControllerDisplayModeChanged?.Invoke(generalSettings.ControllerDisplayMode);
            AppearanceSettingsChanged?.Invoke(GetSettings<AppearanceSettings>());
            UpdateSelectedSettingsOptions();
            _settingsLoaded = true;
        }
        catch (Exception exception)
        {
            SettingsError?.Invoke(string.Format(CultureInfo.CurrentCulture, Resources.LoadSettingsError, exception.Message));
        }
        finally
        {
            IsSettingsLoading = false;
        }
    }

    /// <summary>
    /// Returns the cached object for a registered settings section after successful initialization.
    /// Throws when the section is missing or settings have not loaded.
    /// </summary>
    public TSettings GetSettings<TSettings>() where TSettings : class
    {
        if (_settingsByType.TryGetValue(typeof(TSettings), out var settings))
        {
            return (TSettings)settings;
        }

        throw new InvalidOperationException($"Settings section for '{typeof(TSettings).Name}' is not registered or has not been loaded.");
    }

    /// <summary>Selects a section in this screen's navigation model.</summary>
    public void SelectSection(SettingsSectionOptionViewModel section)
    {
        ArgumentNullException.ThrowIfNull(section);
        Navigation.SelectSection(section);
    }

    /// <summary>Enters the selected section and starts compatibility catalog loading when needed.</summary>
    public void ActivateSection()
    {
        Navigation.ActivateSection();
        if (Navigation.IsCompatibilitySection &&
            ProtonManagement.CanBrowseCatalogs &&
            !ProtonManagement.HasLoadedProviders &&
            !ProtonManagement.IsLoading)
        {
            ProtonManagement.RefreshCommand.Execute(null);
        }
    }

    /// <summary>Selects the connector section, failing explicitly if it was not registered.</summary>
    public void SelectConnectorsSection()
    {
        var section = Navigation.Sections.SingleOrDefault(option => option.IsConnectorsSection)
            ?? throw new InvalidOperationException("The connectors settings section is not registered.");
        Navigation.SelectSection(section);
    }

    /// <summary>Requests that the next initial focus for this screen land on a connector's primary action.</summary>
    public void RequestConnectorsPrimaryActionFocus()
    {
        _shouldFocusConnectorsPrimaryAction = true;
    }

    /// <summary>Consumes the pending connector action focus request once the view has a focusable target.</summary>
    public bool ConsumeConnectorsPrimaryActionFocusRequest()
    {
        if (!_shouldFocusConnectorsPrimaryAction)
        {
            return false;
        }

        _shouldFocusConnectorsPrimaryAction = false;
        return true;
    }

    /// <summary>Activates the selected settings section.</summary>
    public void ActivateSettingsSection()
    {
        ActivateSection();
    }

    /// <summary>Returns focus navigation from section fields to the section list.</summary>
    public void DeactivateSettingsContent() => Navigation.DeactivateContent();

    /// <summary>Selects a field in the active settings section.</summary>
    public void SelectSettingsField(int index) => Navigation.SelectField(index);

    /// <summary>Selects a tab in the active data section.</summary>
    public void SelectSettingsTab(int index) => Navigation.SelectSettingsTab(index);

    /// <summary>Convenience entry point used by the settings section list.</summary>
    public void SelectSettingsSection(SettingsSectionOptionViewModel section) => SelectSection(section);

    /// <summary>
    /// Applies an editor value to the cached settings object. Language and time-zone edits flow
    /// through their option properties; other edits persist only their owning section.
    /// </summary>
    public void SetSettingsField(
        SettingsSectionOptionViewModel section,
        IReadOnlyList<PropertyInfo> propertyPath,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(propertyPath);
        if (section.Descriptor is not { } descriptor || section.Settings is not { } settings ||
            propertyPath.Count == 0 || propertyPath[0].DeclaringType != descriptor.SettingsType ||
            propertyPath.Any(property => !property.CanRead || !property.CanWrite))
        {
            throw new InvalidOperationException("The selected settings field is not writable.");
        }

        var property = propertyPath[^1];
        if (settings is AppearanceSettings appearanceSettings)
        {
            try
            {
                ValidateAppearanceField(appearanceSettings, propertyPath, value);
            }
            catch (InvalidOperationException exception)
            {
                SettingsError?.Invoke(string.Format(CultureInfo.CurrentCulture, Resources.SaveSettingsError, exception.Message));
                return;
            }
        }

        if (propertyPath.Count == 1 && descriptor.SettingsType == typeof(GeneralSettings) &&
            property.Name == nameof(GeneralSettings.LanguageTag))
        {
            SelectedLanguageOption = FindOrAddLanguageOption(GetRequiredString(value, property.Name));
            return;
        }

        if (propertyPath.Count == 1 && descriptor.SettingsType == typeof(GeneralSettings) &&
            property.Name == nameof(GeneralSettings.TimeZoneId))
        {
            SelectedTimeZoneOption = FindOrAddTimeZoneOption(GetRequiredString(value, property.Name));
            return;
        }

        ControllerDisplayMode? controllerDisplayModeToNotify = null;
        if (propertyPath.Count == 1 && descriptor.SettingsType == typeof(GeneralSettings) &&
            property.Name == nameof(GeneralSettings.ControllerDisplayMode))
        {
            if (value is not ControllerDisplayMode selectedDisplayMode || !Enum.IsDefined(selectedDisplayMode))
            {
                throw new InvalidOperationException("The controller display mode must be a supported value.");
            }

            controllerDisplayModeToNotify = selectedDisplayMode;
        }

        object target = settings;
        for (var index = 0; index < propertyPath.Count - 1; index++)
        {
            var nested = propertyPath[index].GetValue(target);
            if (nested is null)
            {
                nested = Activator.CreateInstance(propertyPath[index].PropertyType)
                    ?? throw new InvalidOperationException($"Settings object '{propertyPath[index].PropertyType.Name}' could not be created.");
                propertyPath[index].SetValue(target, nested);
            }

            target = nested;
        }

        property.SetValue(target, value);
        if (settings is AppearanceSettings updatedAppearanceSettings)
        {
            AppearanceSettingsChanged?.Invoke(updatedAppearanceSettings);
        }

        if (descriptor.SettingsType == typeof(GeneralSettings) && property.Name == nameof(GeneralSettings.TextScalePercent))
        {
            if (value is not int textScalePercent)
            {
                throw new InvalidOperationException("The interface text scale must be an integer percentage.");
            }

            TypographyScale.Apply(textScalePercent);
        }

        if (controllerDisplayModeToNotify is { } controllerDisplayMode)
        {
            ControllerDisplayModeChanged?.Invoke(controllerDisplayMode);
        }

        _ = SaveSettingsSectionAsync(section);
    }

    /// <summary>Returns section focus to the navigation list.</summary>
    protected override void OnDeactivated()
    {
        foreach (var panel in _connectorPanels.Values)
        {
            panel.Deactivate();
        }

        Navigation.DeactivateContent();
    }

    protected override void OnDiscarded()
    {
        foreach (var panel in _connectorPanels.Values)
        {
            panel.Deactivate();
        }
    }

    /// <summary>Persists the field's owning section after a non-general setting changes.</summary>
    private async Task SaveSettingsSectionAsync(SettingsSectionOptionViewModel section)
    {
        await _settingsSaveGate.WaitAsync();
        try
        {
            if (section.Descriptor is not { } descriptor || section.Settings is not { } settings)
            {
                throw new InvalidOperationException($"Settings section '{section.Id}' has not been loaded.");
            }

            await _settingsApi.SaveSectionAsync(descriptor, settings);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            SettingsError?.Invoke(string.Format(CultureInfo.CurrentCulture, Resources.SaveSettingsError, exception.Message));
        }
        finally
        {
            _settingsSaveGate.Release();
        }
    }

    /// <summary>Persists every loaded section after language or time-zone changes.</summary>
    private async Task SaveSettingsAsync()
    {
        await _settingsSaveGate.WaitAsync();
        try
        {
            foreach (var settingsSection in Navigation.Sections)
            {
                if (settingsSection.Descriptor is { } descriptor && settingsSection.Settings is { } settings)
                {
                    await _settingsApi.SaveSectionAsync(descriptor, settings);
                }
            }

            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            SettingsError?.Invoke(string.Format(CultureInfo.CurrentCulture, Resources.SaveSettingsError, exception.Message));
        }
        finally
        {
            _settingsSaveGate.Release();
        }
    }

    private void UpdateSelectedSettingsOptions()
    {
        var generalSettings = GetSettings<GeneralSettings>();
        _isUpdatingSelectedOptions = true;
        SelectedLanguageOption = FindOrAddLanguageOption(generalSettings.LanguageTag);
        SelectedTimeZoneOption = FindOrAddTimeZoneOption(generalSettings.TimeZoneId);
        _isUpdatingSelectedOptions = false;
    }

    private SettingsOptionViewModel FindOrAddLanguageOption(string languageTag)
    {
        var option = LanguageOptions.FirstOrDefault(item => item.Value == languageTag);
        if (option is not null)
        {
            return option;
        }

        string label;
        try
        {
            label = CultureInfo.GetCultureInfo(languageTag).NativeName;
        }
        catch (CultureNotFoundException)
        {
            label = languageTag;
        }

        option = new SettingsOptionViewModel(languageTag, label);
        LanguageOptions.Add(option);
        return option;
    }

    private SettingsOptionViewModel FindOrAddTimeZoneOption(string timeZoneId)
    {
        var option = TimeZoneOptions.FirstOrDefault(item => item.Value == timeZoneId);
        if (option is not null)
        {
            return option;
        }

        option = new SettingsOptionViewModel(timeZoneId, string.Format(CultureInfo.CurrentCulture, Resources.Unavailable, timeZoneId));
        TimeZoneOptions.Add(option);
        return option;
    }

    private static ObservableCollection<SettingsOptionViewModel> CreateLanguageOptions()
    {
        var languageTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "en-US",
            "pt-BR",
            "es-ES"
        };

        return new ObservableCollection<SettingsOptionViewModel>(
            languageTags
                .Select(languageTag => new SettingsOptionViewModel(
                    languageTag,
                    CultureInfo.GetCultureInfo(languageTag).NativeName))
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase));
    }

    private static ObservableCollection<SettingsOptionViewModel> CreateTimeZoneOptions()
    {
        return new ObservableCollection<SettingsOptionViewModel>(
            TimeZoneInfo.GetSystemTimeZones()
                .Select(timeZone => new SettingsOptionViewModel(
                    timeZone.Id,
                    $"{timeZone.DisplayName} ({timeZone.Id})"))
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase));
    }

    private static void ValidateAppearanceSettings(AppearanceSettings settings)
    {
        if (!Enum.IsDefined(settings.TitleFont))
        {
            throw new InvalidOperationException("The library title font must be a supported value.");
        }

        if (!Enum.IsDefined(settings.LibraryPosition))
        {
            throw new InvalidOperationException("The library position must be a supported value.");
        }

        if (settings.BackgroundDimmingPercent is < 0 or > 100)
        {
            throw new InvalidOperationException("The background dimming must be an integer percentage between 0 and 100.");
        }
    }

    private static void ValidateAppearanceField(
        AppearanceSettings settings,
        IReadOnlyList<PropertyInfo> propertyPath,
        object? value)
    {
        if (propertyPath.Count != 1)
        {
            throw new InvalidOperationException("The selected appearance field is not writable.");
        }

        ValidateAppearanceSettings(settings);
        switch (propertyPath[0].Name)
        {
            case nameof(AppearanceSettings.TitleFont):
                if (value is not LibraryTitleFont titleFont || !Enum.IsDefined(titleFont))
                {
                    throw new InvalidOperationException("The library title font must be a supported value.");
                }

                break;
            case nameof(AppearanceSettings.LibraryPosition):
                if (value is not LibraryPosition position || !Enum.IsDefined(position))
                {
                    throw new InvalidOperationException("The library position must be a supported value.");
                }

                break;
            case nameof(AppearanceSettings.BackgroundDimmingPercent):
                if (value is not int percentage || percentage is < 0 or > 100)
                {
                    throw new InvalidOperationException("The background dimming must be an integer percentage between 0 and 100.");
                }

                break;
            case nameof(AppearanceSettings.ShowCoverTitles):
            case nameof(AppearanceSettings.ShowSelectedGameTitle):
                GetRequiredBoolean(value, propertyPath[0].Name);
                break;
            default:
                throw new InvalidOperationException("The selected appearance field is not writable.");
        }
    }

    private static string GetRequiredString(object? value, string propertyName) => value is string text
        ? text
        : throw new InvalidOperationException($"Settings field '{propertyName}' requires a string value.");

    private static bool GetRequiredBoolean(object? value, string propertyName) => value is bool flag
        ? flag
        : throw new InvalidOperationException($"Settings field '{propertyName}' requires a boolean value.");

    private void OnSettingsNavigationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(SettingsNavigationViewModel.Sections):
                OnPropertyChanged(nameof(SettingsSections));
                break;
            case nameof(SettingsNavigationViewModel.SelectedSectionIndex):
                OnPropertyChanged(nameof(SelectedSettingsSectionIndex));
                break;
            case nameof(SettingsNavigationViewModel.IsContentActive):
                OnPropertyChanged(nameof(IsSettingsContentActive));
                break;
            case nameof(SettingsNavigationViewModel.SelectedFieldIndex):
                OnPropertyChanged(nameof(SelectedSettingsFieldIndex));
                break;
            case nameof(SettingsNavigationViewModel.IsCompatibilitySection):
                OnPropertyChanged(nameof(IsCompatibilitySettingsSection));
                break;
            case nameof(SettingsNavigationViewModel.IsConnectorsSection):
                OnPropertyChanged(nameof(IsConnectorsSettingsSection));
                break;
            case nameof(SettingsNavigationViewModel.IsSettingsDataSection):
                OnPropertyChanged(nameof(IsSettingsDataSectionSelected));
                break;
            case nameof(SettingsNavigationViewModel.SelectedSection):
                OnPropertyChanged(nameof(SelectedSettingsSection));
                OnPropertyChanged(nameof(IsGeneralSettingsSection));
                OnPropertyChanged(nameof(HasCacheCleanupMessage));
                break;
            case nameof(SettingsNavigationViewModel.SelectedFieldCount):
                OnPropertyChanged(nameof(SelectedSettingsFieldCount));
                break;
            case nameof(SettingsNavigationViewModel.SelectedSettingsTabIndex):
                OnPropertyChanged(nameof(SelectedSettingsTabIndex));
                break;
        }
    }

    private void OnProtonManagementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ProtonManagementViewModel.NavigationFieldCount))
        {
            Navigation.SetCompatibilityFieldCount(ProtonManagement.NavigationFieldCount);
        }
    }

    partial void OnSelectedLanguageOptionChanged(SettingsOptionViewModel? value)
    {
        if (_isUpdatingSelectedOptions || value is null || !_settingsLoaded)
        {
            return;
        }

        var generalSettings = GetSettings<GeneralSettings>();
        if (generalSettings.LanguageTag == value.Value)
        {
            return;
        }

        generalSettings.LanguageTag = value.Value;
        LanguageChanged?.Invoke(value.Value);
        _ = SaveSettingsAsync();
    }

    partial void OnSelectedTimeZoneOptionChanged(SettingsOptionViewModel? value)
    {
        if (_isUpdatingSelectedOptions || value is null || !_settingsLoaded)
        {
            return;
        }

        var generalSettings = GetSettings<GeneralSettings>();
        if (generalSettings.TimeZoneId == value.Value)
        {
            return;
        }

        _dateTimeDisplayFormatter.SetTimeZone(value.Value);
        generalSettings.TimeZoneId = value.Value;
        TimeZoneChanged?.Invoke(value.Value);
        _ = SaveSettingsAsync();
    }
}
