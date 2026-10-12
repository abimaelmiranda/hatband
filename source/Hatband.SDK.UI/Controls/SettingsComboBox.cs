using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Hatband.SDK.UI.Controls;

/// <summary>A settings picker that commits only after the user confirms its selection.</summary>
public sealed class SettingsComboBox : ComboBox
{
    private object? selectionBeforeOpening;
    private bool processingSelection;

    public SettingsComboBox()
    {
        DropDownOpened += OnDropDownOpened;
        DropDownClosed += OnDropDownClosed;
    }

    protected override Type StyleKeyOverride => typeof(ComboBox);

    public event EventHandler? SelectionCommitted;

    public void ConfirmSelection()
    {
        if (!IsDropDownOpen)
        {
            return;
        }

        CommitSelection();
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    public override bool UpdateSelectionFromEvent(Control container, RoutedEventArgs eventArgs)
    {
        processingSelection = true;
        try
        {
            var selected = base.UpdateSelectionFromEvent(container, eventArgs);
            if (selected)
            {
                CommitSelection();
            }
            else if (!IsDropDownOpen)
            {
                SetCurrentValue(SelectedItemProperty, selectionBeforeOpening);
            }

            return selected;
        }
        finally
        {
            processingSelection = false;
        }
    }

    private void OnDropDownOpened(object? sender, EventArgs args) => selectionBeforeOpening = SelectedItem;

    private void OnDropDownClosed(object? sender, EventArgs args)
    {
        if (!processingSelection)
        {
            SetCurrentValue(SelectedItemProperty, selectionBeforeOpening);
        }
    }

    private void CommitSelection()
    {
        var changed = !Equals(selectionBeforeOpening, SelectedItem);
        selectionBeforeOpening = SelectedItem;
        if (changed)
        {
            SelectionCommitted?.Invoke(this, EventArgs.Empty);
        }
    }
}
