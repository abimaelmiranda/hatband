using Avalonia.Controls;

namespace Hatband.SDK.UI.Connectors;

/// <summary>A plugin-owned panel hosted by the application.</summary>
public abstract class PluginPanel : UserControl
{
    public virtual Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public virtual void Deactivate()
    {
    }

    public virtual bool FocusInitial() => false;

    public virtual bool TryHandleBack() => false;
}
