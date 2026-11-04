using UnityEngine;

namespace Relay
{
    /// <summary>Puts a dropped object back on the table, so nobody has to bend to the floor in VR.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FallRespawn : MonoBehaviour
    {
        public string label = "object";
        public float minHeight = 0.45f;
        [Tooltip("Where to put it back. Leave empty to use its starting position.")]
        public Transform home;

        Rigidbody _rb;
        Vector3 _startPosition;
        Quaternion _startRotation;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _startPosition = transform.position;
            _startRotation = transform.rotation;
        }

        void Update()
        {
            // Kinematic means it's held or screwed on, so it isn't falling.
            if (_rb.isKinematic || transform.position.y >= minHeight) return;

            RelayLog.Event("DROP", label);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            if (home != null) transform.SetPositionAndRotation(home.position + Vector3.up * 0.03f, Quaternion.identity);
            else transform.SetPositionAndRotation(_startPosition, _startRotation);
        }
    }
}
