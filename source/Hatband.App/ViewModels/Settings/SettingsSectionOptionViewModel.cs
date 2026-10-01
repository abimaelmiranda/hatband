using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;

namespace Hatband.App.ViewModels.Settings;

public partial class SettingsSectionOptionViewModel : ObservableObject
{
    public SettingsSectionOptionViewModel(SettingsSection section, string title, string symbol)
    {
        Section = section;
        Title = title;
        Symbol = symbol;
    }

    public SettingsSection Section { get; }

    public string Title { get; }

    public string Symbol { get; }

    public IBrush Background => IsSelected
        ? new SolidColorBrush(Color.Parse("#263640"))
        : Brushes.Transparent;

    public IBrush SelectionBorderBrush => IsSelected
        ? new SolidColorBrush(Color.Parse("#72D9FF"))
        : Brushes.Transparent;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(SelectionBorderBrush));
    }
}
