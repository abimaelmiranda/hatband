using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Hatband.App.ViewModels;
using Hatband.App.ViewModels.Settings;
using Hatband.App.ViewModels.Settings.Fields;
using Hatband.Core.Models.Settings;
using AppResources = Hatband.App.Localization.Resources;

namespace Hatband.App.Views.Screens;

public partial class SettingsSectionEditorView : UserControl
{
    private static readonly Dictionary<Type, (decimal Minimum, decimal Maximum)> NumericBounds = new()
    {
        [typeof(byte)] = (byte.MinValue, byte.MaxValue),
        [typeof(sbyte)] = (sbyte.MinValue, sbyte.MaxValue),
        [typeof(short)] = (short.MinValue, short.MaxValue),
        [typeof(ushort)] = (ushort.MinValue, ushort.MaxValue),
        [typeof(int)] = (int.MinValue, int.MaxValue),
        [typeof(uint)] = (uint.MinValue, uint.MaxValue),
        [typeof(long)] = (long.MinValue, long.MaxValue),
        [typeof(ulong)] = (ulong.MinValue, ulong.MaxValue),
        [typeof(float)] = (decimal.MinValue, decimal.MaxValue),
        [typeof(double)] = (decimal.MinValue, decimal.MaxValue),
        [typeof(decimal)] = (decimal.MinValue, decimal.MaxValue)
    };

    private static readonly HashSet<Type> FractionalNumericTypes =
    [typeof(float), typeof(double), typeof(decimal)];

    public static readonly StyledProperty<SettingsSectionOptionViewModel?> SectionProperty =
        AvaloniaProperty.Register<SettingsSectionEditorView, SettingsSectionOptionViewModel?>(nameof(Section));

    private readonly List<PanelEditorState> panelStates = [];
    private SettingsSectionOptionViewModel? observedSection;
    private MainWindowViewModel? observedViewModel;
    private TabControl? sectionTabControl;
    private PanelEditorState? activePanel;
    private bool isApplyingTabSelection;

    public SettingsSectionEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    public SettingsSectionOptionViewModel? Section
    {
        get => GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    public Control? FocusField(int index)
    {
        if (activePanel is null || index < 0 || index >= activePanel.Fields.Count)
        {
            return null;
        }

        var control = activePanel.Fields[index];
        DirectionalFocusNavigator.Focus(control);
        return control;
    }

    public bool OpenSelectedComboBox(int index)
    {
        if (activePanel is null || index < 0 || index >= activePanel.Fields.Count ||
            activePanel.Fields[index] is not ComboBox comboBox)
        {
            return false;
        }

        comboBox.IsDropDownOpen = true;
        return true;
    }

    public bool HasOpenComboBox() => SettingsFieldsPanel.GetVisualDescendants()
        .OfType<ComboBox>()
        .Any(comboBox => comboBox.IsDropDownOpen);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SectionProperty)
        {
            ObserveSection(change.GetNewValue<SettingsSectionOptionViewModel?>());
            Rebuild();
        }
        else if (change.Property == IsVisibleProperty && IsVisible)
        {
            Rebuild();
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs args) => Rebuild();

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (observedViewModel is not null)
        {
            observedViewModel.SettingsNavigation.PropertyChanged -= OnNavigationPropertyChanged;
            observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        observedViewModel = DataContext as MainWindowViewModel;
        if (observedViewModel is not null)
        {
            observedViewModel.SettingsNavigation.PropertyChanged += OnNavigationPropertyChanged;
            observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        Rebuild();
    }

    private void ObserveSection(SettingsSectionOptionViewModel? section)
    {
        if (observedSection is not null)
        {
            observedSection.PropertyChanged -= OnSectionPropertyChanged;
        }

        observedSection = section;
        if (observedSection is not null)
        {
            observedSection.PropertyChanged += OnSectionPropertyChanged;
        }
    }

    private void OnSectionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SettingsSectionOptionViewModel.Settings))
        {
            Rebuild();
        }
    }

    private void OnNavigationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SettingsNavigationViewModel.SelectedSettingsTabIndex))
        {
            SelectTabFromNavigation();
        }
        else if (args.PropertyName == nameof(SettingsNavigationViewModel.SelectedFieldIndex) ||
                 args.PropertyName == nameof(SettingsNavigationViewModel.IsContentActive))
        {
            UpdateFieldSelectionBorders();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainWindowViewModel.IsSettingsLoading) &&
            observedViewModel?.IsSettingsLoading == false)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        if (!IsLoaded)
        {
            return;
        }

        SettingsFieldsPanel.Children.Clear();
        panelStates.Clear();
        sectionTabControl = null;
        activePanel = null;
        if (Section?.Descriptor is not { } descriptor || Section.Settings is not { } settings ||
            Section.EditorDefinition is not { } definition)
        {
            return;
        }

        SettingsFieldsPanel.Children.Add(new TextBlock
        {
            Text = descriptor.Id switch
            {
                SettingsSectionOptionViewModel.GeneralSectionId => AppResources.General,
                SettingsSectionOptionViewModel.ConnectorsSectionId => AppResources.ConnectorsFallback,
                _ => descriptor.DisplayName
            },
            FontSize = 19,
            FontWeight = FontWeight.SemiBold
        });
        SettingsFieldsPanel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#39454F")) });
        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            SettingsFieldsPanel.Children.Add(new TextBlock
            {
                Text = descriptor.Description,
                Foreground = new SolidColorBrush(Color.Parse("#AEBBC5")),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (definition.HasTabs)
        {
            BuildTabbedSection(settings, definition);
        }
        else if (definition.SinglePanel is { } singlePanel)
        {
            var state = new PanelEditorState();
            panelStates.Add(state);
            SettingsFieldsPanel.Children.Add(BuildPanelEditor(settings, singlePanel, state));
            activePanel = state;
        }

        UpdateFieldSelectionBorders();
    }

    private void BuildTabbedSection(object settings, SettingsEditorDefinition definition)
    {
        sectionTabControl = new TabControl();
        var tabItems = new List<TabItem>();
        foreach (var tab in definition.Tabs)
        {
            if (tab.Property is not null)
            {
                GetOrCreateNestedSettings(settings, [tab.Property]);
            }

            var panelState = new PanelEditorState();
            panelStates.Add(panelState);
            var item = new TabItem
            {
                Header = tab.Property is null
                    ? AppResources.General
                    : SettingsFieldConvention.GetDisplayName(tab.Property),
                Content = BuildPanelEditor(settings, tab.Content, panelState)
            };
            tabItems.Add(item);
        }

        sectionTabControl.ItemsSource = tabItems;
        sectionTabControl.SelectedIndex = observedViewModel?.SettingsNavigation.SelectedSettingsTabIndex ?? 0;
        sectionTabControl.SelectionChanged += OnSectionTabSelectionChanged;
        SettingsFieldsPanel.Children.Add(sectionTabControl);
        ActivatePanel(sectionTabControl.SelectedIndex);
    }

    private Control BuildPanelEditor(object settings, SettingsPanelDefinition definition, PanelEditorState state)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var block in definition.Blocks)
        {
            switch (block)
            {
                case SettingsPanelDefinition.Field field:
                    AddField(panel, settings, field, state);
                    break;
                case SettingsPanelDefinition.Group group:
                    AddGroup(panel, settings, group, state);
                    break;
            }
        }

        return panel;
    }

    private void AddField(
        Panel panel,
        object settings,
        SettingsPanelDefinition.Field field,
        PanelEditorState state)
    {
        var editor = CreateEditor(settings, field);
        var index = state.Fields.Count;
        state.Fields.Add(editor);
        editor.GotFocus += (_, _) => observedViewModel?.SelectSettingsField(index);
        var row = CreateFieldRow(field.Property, editor, state == activePanel ? index : -1);
        state.Borders.Add(row);
        panel.Children.Add(row);
    }

    private void AddGroup(
        Panel panel,
        object settings,
        SettingsPanelDefinition.Group group,
        PanelEditorState state)
    {
        GetOrCreateNestedSettings(settings, group.PropertyPath);
        var groupContent = BuildPanelEditor(settings, group.Content, state);
        panel.Children.Add(new Border
        {
            Padding = new Thickness(9, 8),
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.Parse("#151C21")),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = SettingsFieldConvention.GetDisplayName(group.Property),
                        FontSize = 15,
                        FontWeight = FontWeight.SemiBold
                    },
                    new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#39454F")) },
                    groupContent
                }
            }
        });
    }

    private void OnSectionTabSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (isApplyingTabSelection || sectionTabControl is null || sectionTabControl.SelectedIndex < 0)
        {
            return;
        }

        observedViewModel?.SelectSettingsTab(sectionTabControl.SelectedIndex);
        ActivatePanel(sectionTabControl.SelectedIndex);
    }

    private void SelectTabFromNavigation()
    {
        if (sectionTabControl is null || observedViewModel is null)
        {
            return;
        }

        var selectedTabIndex = observedViewModel.SelectedSettingsTabIndex;
        if (sectionTabControl.SelectedIndex == selectedTabIndex)
        {
            ActivatePanel(selectedTabIndex);
            return;
        }

        isApplyingTabSelection = true;
        sectionTabControl.SelectedIndex = selectedTabIndex;
        isApplyingTabSelection = false;
        ActivatePanel(selectedTabIndex);
    }

    private void ActivatePanel(int index)
    {
        activePanel = index >= 0 && index < panelStates.Count ? panelStates[index] : null;
        UpdateFieldSelectionBorders();
    }

    private object GetOrCreateNestedSettings(object settings, IReadOnlyList<PropertyInfo> propertyPath)
    {
        object current = settings;
        foreach (var property in propertyPath)
        {
            var nested = property.GetValue(current);
            if (nested is null)
            {
                var nestedType = SettingsFieldConvention.GetValueType(property.PropertyType);
                nested = Activator.CreateInstance(nestedType)
                    ?? throw new InvalidOperationException($"Settings object '{nestedType.Name}' could not be created.");
                property.SetValue(current, nested);
            }

            current = nested;
        }

        return current;
    }

    private Control CreateEditor(object settings, SettingsPanelDefinition.Field field)
    {
        var property = field.Property;
        var type = SettingsFieldConvention.GetValueType(property.PropertyType);
        var propertyOwner = GetPropertyOwner(settings, field.PropertyPath);
        var currentValue = property.GetValue(propertyOwner);

        if (type == typeof(bool))
        {
            var checkBox = new CheckBox
            {
                IsChecked = (bool?)currentValue,
                MinHeight = 38,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            checkBox.IsCheckedChanged += (_, _) => SaveValue(field.PropertyPath, checkBox.IsChecked);
            return checkBox;
        }

        if (type.IsEnum)
        {
            var comboBox = new ComboBox
            {
                ItemsSource = Enum.GetValues(type),
                SelectedItem = currentValue,
                MinHeight = 38
            };
            comboBox.SelectionChanged += (_, _) => SaveValue(field.PropertyPath, comboBox.SelectedItem);
            return comboBox;
        }

        if (type == typeof(string) && IsGeneralSetting(field, nameof(GeneralSettings.LanguageTag)))
        {
            var comboBox = CreateOptionsComboBox(observedViewModel!.LanguageOptions, (string?)currentValue);
            comboBox.SelectionChanged += (_, _) =>
            {
                if (comboBox.SelectedItem is SettingsOptionViewModel option)
                {
                    SaveValue(field.PropertyPath, option.Value);
                }
            };
            return comboBox;
        }

        if (type == typeof(string) && IsGeneralSetting(field, nameof(GeneralSettings.TimeZoneId)))
        {
            var comboBox = CreateOptionsComboBox(observedViewModel!.TimeZoneOptions, (string?)currentValue);
            comboBox.SelectionChanged += (_, _) =>
            {
                if (comboBox.SelectedItem is SettingsOptionViewModel option)
                {
                    SaveValue(field.PropertyPath, option.Value);
                }
            };
            return comboBox;
        }

        if (type == typeof(string))
        {
            var textBox = new TextBox
            {
                Text = (string?)currentValue,
                MinHeight = 38,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            textBox.LostFocus += (_, _) => SaveValue(field.PropertyPath, textBox.Text);
            return textBox;
        }

        var (minimum, maximum) = GetNumericBounds(type);
        var isFractional = FractionalNumericTypes.Contains(type);
        var numericEditor = new NumericUpDown
        {
            Value = currentValue is null ? null : Convert.ToDecimal(currentValue, CultureInfo.InvariantCulture),
            Minimum = minimum,
            Maximum = maximum,
            Increment = isFractional ? 0.1m : 1m,
            NumberFormat = new NumberFormatInfo { NumberDecimalDigits = isFractional ? 2 : 0 },
            MinHeight = 38,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        numericEditor.ValueChanged += (_, _) => SaveNumericValue(field.PropertyPath, type, numericEditor.Value);
        return numericEditor;
    }

    private bool IsGeneralSetting(SettingsPanelDefinition.Field field, string propertyName) =>
        field.PropertyPath.Count == 1 &&
        Section?.Descriptor?.SettingsType == typeof(GeneralSettings) &&
        field.Property.Name == propertyName;

    private static object GetPropertyOwner(object settings, IReadOnlyList<PropertyInfo> propertyPath)
    {
        object current = settings;
        for (var index = 0; index < propertyPath.Count - 1; index++)
        {
            current = propertyPath[index].GetValue(current)
                ?? throw new InvalidOperationException($"Nested settings '{propertyPath[index].Name}' are not initialized.");
        }

        return current;
    }

    private static ComboBox CreateOptionsComboBox(
        IReadOnlyList<SettingsOptionViewModel> options,
        string? selectedValue) => new()
    {
        ItemsSource = options,
        SelectedItem = options.FirstOrDefault(option => option.Value == selectedValue),
        MinHeight = 38,
        ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SettingsOptionViewModel>(
            (option, _) => new TextBlock { Text = option.Label }, supportsRecycling: true)
    };

    private Border CreateFieldRow(PropertyInfo property, Control editor, int fieldIndex)
    {
        var fieldLabel = property.DeclaringType == typeof(GeneralSettings)
            ? property.Name switch
            {
                nameof(GeneralSettings.LanguageTag) => AppResources.InterfaceLanguage,
                nameof(GeneralSettings.TimeZoneId) => AppResources.TimeZone,
                _ => SettingsFieldConvention.GetDisplayName(property)
            }
            : SettingsFieldConvention.GetDisplayName(property);
        Control editorContent = editor;
        if (property.DeclaringType == typeof(GeneralSettings) && property.Name == nameof(GeneralSettings.LanguageTag))
        {
            editorContent = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    editor,
                    new TextBlock
                    {
                        Text = AppResources.LanguageRestartHint,
                        Foreground = new SolidColorBrush(Color.Parse("#9EABB5")),
                        FontSize = 10
                    }
                }
            };
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("170,*"),
            ColumnSpacing = 12
        };
        grid.Children.Add(new TextBlock
        {
            Text = fieldLabel,
            FontSize = 13,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });
        Grid.SetColumn(editorContent, 1);
        grid.Children.Add(editorContent);
        if (property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>() is { Description: { Length: > 0 } description })
        {
            ToolTip.SetTip(editor, description);
        }

        return new Border
        {
            Padding = new Thickness(9, 7),
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            BorderBrush = fieldIndex >= 0 ? observedViewModel?.GetSettingsFieldBorderBrush(fieldIndex) ?? Brushes.Transparent : Brushes.Transparent,
            Child = grid
        };
    }

    private void SaveValue(IReadOnlyList<PropertyInfo> propertyPath, object? value)
    {
        if (Section is { } section && observedViewModel is { } viewModel && section.Settings is { } settings &&
            propertyPath.Count > 0 && propertyPath[0].DeclaringType == settings.GetType())
        {
            viewModel.SetSettingsField(section, propertyPath, value);
        }
    }

    private static (decimal Minimum, decimal Maximum) GetNumericBounds(Type type) =>
        NumericBounds.TryGetValue(type, out var bounds)
            ? bounds
            : throw new InvalidOperationException($"Settings field type '{type.Name}' is not numeric.");

    private void SaveNumericValue(IReadOnlyList<PropertyInfo> propertyPath, Type type, decimal? value)
    {
        try
        {
            SaveValue(propertyPath, value is null ? null : Convert.ChangeType(value.Value, type, CultureInfo.InvariantCulture));
        }
        catch (OverflowException)
        {
            Rebuild();
        }
    }

    private void UpdateFieldSelectionBorders()
    {
        if (activePanel is null)
        {
            return;
        }

        for (var index = 0; index < activePanel.Borders.Count; index++)
        {
            activePanel.Borders[index].BorderBrush = observedViewModel?.GetSettingsFieldBorderBrush(index) ?? Brushes.Transparent;
        }
    }

    private sealed class PanelEditorState
    {
        public List<Control> Fields { get; } = [];

        public List<Border> Borders { get; } = [];
    }
}
