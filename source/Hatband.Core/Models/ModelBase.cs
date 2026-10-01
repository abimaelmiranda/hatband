namespace Hatband.Core.Models;

/// <summary>
/// Base for persisted Hatband models with a time-ordered identifier.
/// </summary>
public abstract class ModelBase
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}
