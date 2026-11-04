using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Relay
{
    /// <summary>
    /// A grab that the other hand can't take over. Without this, reaching for the cap with the free
    /// hand could pull the bottle out of the hand holding it.
    /// </summary>
    public class NoStealGrabInteractable : XRGrabInteractable
    {
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            return base.IsSelectableBy(interactor) && (!isSelected || interactorsSelecting.Contains(interactor));
        }
    }
}
