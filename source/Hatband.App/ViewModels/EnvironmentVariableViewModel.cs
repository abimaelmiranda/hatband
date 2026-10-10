using CommunityToolkit.Mvvm.ComponentModel;

namespace Hatband.App.ViewModels;

public partial class EnvironmentVariableViewModel : ObservableObject
{
    public EnvironmentVariableViewModel(string name = "", string value = "")
    {
        Name = name;
        Value = value;
    }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; }
}
