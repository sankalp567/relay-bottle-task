using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Relay
{
    /// <summary>
    /// The handle a hand grips to twist a cap. It only works while the other hand holds the bottle, which
    /// keeps the task two-handed. Until then, reaching for the top of a bottle simply picks the bottle up.
    /// </summary>
    public class CapGripInteractable : XRSimpleInteractable
    {
        Bottle _bottle;

        Bottle Bottle => _bottle != null ? _bottle : _bottle = GetComponentInParent<Bottle>();

        bool OtherHandHoldsBottle(IXRInteractor interactor)
        {
            if (!Bottle.IsHeld) return false;
            foreach (IXRSelectInteractor holder in Bottle.Grab.interactorsSelecting)
            {
                if (ReferenceEquals(holder, interactor)) return false;
            }

            return true;
        }

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            return base.IsSelectableBy(interactor) && OtherHandHoldsBottle(interactor);
        }

        public override bool IsHoverableBy(IXRHoverInteractor interactor)
        {
            return base.IsHoverableBy(interactor) && OtherHandHoldsBottle(interactor);
        }
    }
}
