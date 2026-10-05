using System.Reflection;

namespace Hatband.App.ViewModels.Settings.Fields;

internal sealed class SettingsPanelDefinition
{
    public SettingsPanelDefinition(IReadOnlyList<Block> blocks)
    {
        Blocks = blocks;
        FieldCount = blocks.Sum(block => block.FieldCount);
    }

    public IReadOnlyList<Block> Blocks { get; }

    public int FieldCount { get; }

    internal abstract record Block
    {
        public abstract int FieldCount { get; }
    }

    internal sealed record Field(PropertyInfo Property, IReadOnlyList<PropertyInfo> PropertyPath) : Block
    {
        public override int FieldCount => 1;
    }

    internal sealed record Group(
        PropertyInfo Property,
        IReadOnlyList<PropertyInfo> PropertyPath,
        SettingsPanelDefinition Content) : Block
    {
        public override int FieldCount => Content.FieldCount;
    }
}
