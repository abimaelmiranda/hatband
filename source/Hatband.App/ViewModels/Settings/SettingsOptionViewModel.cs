namespace Hatband.App.ViewModels.Settings;

public sealed class SettingsOptionViewModel
{
    public SettingsOptionViewModel(string value, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Value = value;
        Label = label;
    }

    public string Value { get; }

    public string Label { get; }
}
