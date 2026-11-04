using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Relay
{
    /// <summary>
    /// The "next" button on the bench for the touch condition. A hand has to be free and reach it, which is
    /// the real cost of touch that a thumb button on a controller would hide.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class TouchButton : MonoBehaviour
    {
        const float PressedDepth = 0.008f;

        public RelaySignals signals;
        public Transform movingPart;

        XRSimpleInteractable _interactable;
        Vector3 _restPosition;

        void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            _restPosition = movingPart.localPosition;
        }

        void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnPressed);
            _interactable.selectExited.AddListener(OnLifted);
        }

        void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnPressed);
            _interactable.selectExited.RemoveListener(OnLifted);
        }

        void OnPressed(SelectEnterEventArgs args)
        {
            movingPart.localPosition = _restPosition + Vector3.down * PressedDepth;
            signals.PressTouch();
        }

        void OnLifted(SelectExitEventArgs args) => movingPart.localPosition = _restPosition;
    }
}
