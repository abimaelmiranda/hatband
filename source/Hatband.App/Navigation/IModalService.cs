using Hatband.App.ViewModels.Navigation;

namespace Hatband.App.Navigation;

/// <summary>Shows owned modal chains and completes each modal invocation with an explicit outcome.</summary>
public interface IModalService
{
    /// <summary>Whether a modal currently blocks screen navigation.</summary>
    bool HasOpenModals { get; }

    /// <summary>The modal currently displayed above its owner, if any.</summary>
    IModalViewModel? ActiveModal { get; }

    /// <summary>Shows a typed modal owned by the active modal or screen and returns its confirmed or cancelled result.</summary>
    Task<ModalCompletion<T>> ShowAsync<T>(ModalViewModel<T> modal, object? owner = null);

    /// <summary>Shows a modal that completes without a result value.</summary>
    Task<ModalCompletion> ShowAsync(ModalViewModel modal, object? owner = null);

    /// <summary>Cancels the top modal and any modal descendants it owns.</summary>
    bool DismissTopModal();

    /// <summary>Cancels every modal from the top down so a screen reset can safely follow.</summary>
    void DismissModalChain();
}
