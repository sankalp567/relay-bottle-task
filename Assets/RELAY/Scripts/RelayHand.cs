using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Relay
{
    /// <summary>
    /// One of the XR rig's real hands, moved and gripped by script instead of a controller. It uses the same
    /// near-grab detection as a headset, so what works here is what a person's real reach would do.
    /// </summary>
    public sealed class RelayHand
    {
        public readonly NearFarInteractor Interactor;
        public readonly Transform Root;

        RelayHand(NearFarInteractor interactor)
        {
            Interactor = interactor;
            var driver = interactor.GetComponentInParent<TrackedPoseDriver>();
            if (driver != null) driver.enabled = false;
            Root = driver != null ? driver.transform : interactor.transform;
            interactor.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
        }

        public bool IsGripping { get; private set; }
        public string Name => Interactor.handedness == InteractorHandedness.Left ? "Left" : "Right";
        public IXRSelectInteractable Held => Interactor.interactablesSelected.Count > 0 ? Interactor.interactablesSelected[0] : null;

        /// <summary>Takes over both hands, left first. Returns an empty array if the rig is missing.</summary>
        public static RelayHand[] TakeOverBoth()
        {
            // The rig hides its controllers until it detects real ones, so show them ourselves.
            XRInputModalityManager modality = Object.FindAnyObjectByType<XRInputModalityManager>();
            if (modality != null)
            {
                modality.enabled = false;
                if (modality.leftController != null) modality.leftController.SetActive(true);
                if (modality.rightController != null) modality.rightController.SetActive(true);
            }

            NearFarInteractor left = null;
            NearFarInteractor right = null;
            foreach (NearFarInteractor interactor in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include))
            {
                if (interactor.handedness == InteractorHandedness.Left) left = interactor;
                else if (interactor.handedness == InteractorHandedness.Right) right = interactor;
            }

            if (left == null || right == null) return new RelayHand[0];
            return new[] { new RelayHand(left), new RelayHand(right) };
        }

        public void SetPose(Vector3 position, Quaternion rotation) => Root.SetPositionAndRotation(position, rotation);

        public void SetGrip(bool grip)
        {
            if (IsGripping == grip) return;
            IsGripping = grip;
            Interactor.selectInput.QueueManualState(grip, grip ? 1f : 0f, grip, !grip);
        }
    }
}
