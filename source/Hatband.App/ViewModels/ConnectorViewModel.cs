using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Hatband.Core.Enums.Stores;

namespace Hatband.App.ViewModels;

public partial class ConnectorViewModel : ObservableObject
{
    public ConnectorViewModel(GameSourceId sourceId, string displayName, bool supportsQrLogin)
    {
        SourceId = sourceId;
        DisplayName = displayName;
        SupportsQrLogin = supportsQrLogin;
        LogoText = displayName.Length > 0 ? displayName[..1].ToUpperInvariant() : "?";
    }

    public GameSourceId SourceId { get; }

    public string DisplayName { get; }

    public bool SupportsQrLogin { get; }

    public string LogoText { get; }

    public bool IsSteam => SourceId == GameSourceId.Steam;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public IBrush LogoBrush => SourceId switch
    {
        GameSourceId.Steam => new SolidColorBrush(Color.Parse("#17212B")),
        _ => new SolidColorBrush(Color.Parse("#34424F"))
    };

    public IBrush SelectionBrush => IsSelected
        ? new SolidColorBrush(Color.Parse("#72D9FF"))
        : new SolidColorBrush(Color.Parse("#3C4A56"));

    public IBrush SelectionBackground => IsSelected
        ? new SolidColorBrush(Color.Parse("#263640"))
        : Brushes.Transparent;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectionBrush));
        OnPropertyChanged(nameof(SelectionBackground));
    }
}
