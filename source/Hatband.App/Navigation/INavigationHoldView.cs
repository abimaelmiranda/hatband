namespace Hatband.App.Navigation;

/// <summary>Receives the start and end of a sustained navigation direction.</summary>
public interface INavigationHoldView
{
    void SetNavigationHeld(bool held);
}
