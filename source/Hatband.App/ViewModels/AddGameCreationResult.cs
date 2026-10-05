namespace Hatband.App.ViewModels;

public abstract record AddGameCreationResult
{
    private AddGameCreationResult()
    {
    }

    public sealed record Saved(Game Game) : AddGameCreationResult;

    public sealed record InvalidName : AddGameCreationResult;

    public sealed record Failed(Exception Exception) : AddGameCreationResult;
}
