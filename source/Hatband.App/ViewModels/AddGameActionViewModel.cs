using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hatband.App.Localization;
using Hatband.Core.Models.Games;

namespace Hatband.App.ViewModels;

public sealed partial class AddGameActionViewModel : ObservableObject
{
    private readonly Action<AddGameActionViewModel> _setPrimary;
    private readonly Action<AddGameActionViewModel> _remove;

    public AddGameActionViewModel(
        string name,
        bool isPrimary,
        Action<AddGameActionViewModel> setPrimary,
        Action<AddGameActionViewModel> remove)
    {
        Name = name;
        IsPrimary = isPrimary;
        _setPrimary = setPrimary;
        _remove = remove;
    }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial GameActionType Type { get; set; }

    [ObservableProperty]
    public partial string Target { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Arguments { get; set; }

    [ObservableProperty]
    public partial string? WorkingDirectory { get; set; }

    [ObservableProperty]
    public partial bool IsPrimary { get; set; }

    public bool IsExecutable => Type == GameActionType.Executable;

    public bool IsUri => Type == GameActionType.Uri;

    public string PrimaryActionLabel => IsPrimary ? Resources.PrimaryAction : Resources.SetPrimaryAction;

    public IBrush ExecutableTypeBackground => IsExecutable
        ? new SolidColorBrush(Color.Parse("#40535F"))
        : new SolidColorBrush(Color.Parse("#2E3842"));

    public IBrush UriTypeBackground => IsUri
        ? new SolidColorBrush(Color.Parse("#40535F"))
        : new SolidColorBrush(Color.Parse("#2E3842"));

    [RelayCommand]
    private void SetExecutableType() => Type = GameActionType.Executable;

    [RelayCommand]
    private void SetUriType() => Type = GameActionType.Uri;

    [RelayCommand]
    private void MakePrimary() => _setPrimary(this);

    [RelayCommand]
    private void Remove() => _remove(this);

    public void SetPrimary(bool value) => IsPrimary = value;

    partial void OnTypeChanged(GameActionType value)
    {
        OnPropertyChanged(nameof(IsExecutable));
        OnPropertyChanged(nameof(IsUri));
        OnPropertyChanged(nameof(ExecutableTypeBackground));
        OnPropertyChanged(nameof(UriTypeBackground));
    }

    partial void OnIsPrimaryChanged(bool value) => OnPropertyChanged(nameof(PrimaryActionLabel));
}
