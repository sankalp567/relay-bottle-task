using UnityEngine;

namespace Relay
{
    /// <summary>An object people can look at. It moves with the object, so a held bottle stays trackable.</summary>
    public class AoiTarget : MonoBehaviour
    {
        public string id = "object";
        public float radiusMeters = 0.05f;
        public Vector3 localCenter;

        public Vector3 Center => transform.TransformPoint(localCenter);
    }
}
