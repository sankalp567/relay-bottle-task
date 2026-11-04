using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Relay
{
    /// <summary>Holds the bottle's state and snaps it onto its marked spot when put back.</summary>
    [RequireComponent(typeof(NoStealGrabInteractable))]
    public class Bottle : MonoBehaviour
    {
        public string id = "A";
        public float capacityMl = 500f;
        public float volumeMl = 500f;
        public Transform mouth;
        public BottleCap cap;
        [Tooltip("Released this close to its spot and nearly upright, the bottle snaps into place.")]
        public float snapDistance = 0.06f;

        NoStealGrabInteractable _grab;
        Rigidbody _rb;
        bool _snapPending;

        public NoStealGrabInteractable Grab => _grab != null ? _grab : _grab = GetComponent<NoStealGrabInteractable>();
        public Vector3 HomePosition { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public bool IsHeld => Grab.isSelected;
        public float Fill01 => capacityMl > 0f ? volumeMl / capacityMl : 0f;
        public float TiltDegrees => Vector3.Angle(transform.up, Vector3.up);

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
        }

        void OnEnable() => Grab.selectExited.AddListener(OnReleased);
        void OnDisable() => Grab.selectExited.RemoveListener(OnReleased);

        void OnReleased(SelectExitEventArgs args)
        {
            _snapPending = !IsHeld && HorizontalDistanceToHome() < snapDistance && TiltDegrees < 25f;
        }

        void Update()
        {
            if (!_snapPending || IsHeld) return;
            _snapPending = false;
            transform.SetPositionAndRotation(HomePosition, HomeRotation);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        public bool IsAtHome()
        {
            return !IsHeld
                   && HorizontalDistanceToHome() < 0.03f
                   && TiltDegrees < 15f
                   && _rb.linearVelocity.sqrMagnitude < 0.01f;
        }

        float HorizontalDistanceToHome()
        {
            Vector3 d = transform.position - HomePosition;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
