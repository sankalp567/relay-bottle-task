using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Relay
{
    /// <summary>Turns off ray grabbing, so people must physically reach. RELAY depends on real reaches.</summary>
    public class RelayRigSetup : MonoBehaviour
    {
        void Start()
        {
            NearFarInteractor[] hands = FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include);
            foreach (NearFarInteractor hand in hands) hand.enableFarCasting = false;

            if (hands.Length == 0) Debug.LogWarning("[RELAY] No Near-Far Interactor found. Is the XR Origin in the scene?");
            RelayLog.Event("RIG_READY", $"hands={hands.Length}");
        }
    }
}
