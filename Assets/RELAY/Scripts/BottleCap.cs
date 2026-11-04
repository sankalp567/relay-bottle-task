using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Relay
{
    /// <summary>
    /// A cap that is either screwed on (hand grips a handle on the bottle and twists) or free (a normal
    /// grabbable object). Threads are modelled, not simulated: hand twist drives the cap directly.
    /// </summary>
    [RequireComponent(typeof(NoStealGrabInteractable))]
    public class BottleCap : MonoBehaviour
    {
        public Bottle bottle;
        [Tooltip("Direct child of the bottle: where the fully closed cap sits.")]
        public Transform seat;
        [Tooltip("Handle on the bottle that the hand grips while the cap is screwed on.")]
        public CapGripInteractable grip;
        public float turnsToClose = 1.5f;
        [Tooltip("How much higher the cap sits when loose than when tight.")]
        public float riseWhenLoose = 0.012f;
        public float seatDistance = 0.04f;
        [Tooltip("After coming off, the cap must move this far away before it can be put back on.")]
        public float clearDistance = 0.08f;

        NoStealGrabInteractable _grab;
        Rigidbody _rb;
        Collider _gripCollider;
        CapThreadModel _thread;
        readonly TwistTracker _twist = new TwistTracker();
        IXRSelectInteractor _twistHand;
        Vector3 _handAxis;
        bool _free;
        bool _mustClearSeat;

        public bool IsOff => _free;
        public bool IsBeingTwisted => _twistHand != null;
        public bool IsClosed => !_free && _thread != null && _thread.IsClosed;

        float TotalDegrees => turnsToClose * 360f;

        void Awake()
        {
            _grab = GetComponent<NoStealGrabInteractable>();
            _rb = GetComponent<Rigidbody>();
            _gripCollider = grip.GetComponent<Collider>();
            _thread = new CapThreadModel(TotalDegrees, TotalDegrees);
        }

        void Start()
        {
            grip.selectEntered.AddListener(OnGripStart);
            grip.selectExited.AddListener(OnGripEnd);
            Seat(null, TotalDegrees);
        }

        // The cap sits inside the bottle's collision shape, so they must never push each other.
        void IgnoreBottleCollisions()
        {
            foreach (Collider capCollider in GetComponents<Collider>())
            foreach (Collider bottleCollider in bottle.GetComponentsInChildren<Collider>())
            {
                if (capCollider != bottleCollider) Physics.IgnoreCollision(capCollider, bottleCollider, true);
            }
        }

        void OnDestroy()
        {
            if (grip == null) return;
            grip.selectEntered.RemoveListener(OnGripStart);
            grip.selectExited.RemoveListener(OnGripEnd);
        }

        void Update()
        {
            if (_free) UpdateFree();
            else UpdateTwist();
        }

        void OnGripStart(SelectEnterEventArgs args)
        {
            _twistHand = args.interactorObject;
            _twist.Reset();
            _handAxis = PickHandAxis();
        }

        void OnGripEnd(SelectExitEventArgs args)
        {
            _twistHand = null; // tightness is kept, so people can re-grip mid-turn
        }

        // Twist is measured on whichever hand axis lies flattest against the bottle's axis at grip time.
        Vector3 PickHandAxis()
        {
            Quaternion handInBottle = HandInBottleSpace();
            Vector3 best = Vector3.forward;
            float flattest = float.MaxValue;
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                float height = Mathf.Abs((handInBottle * axis).y);
                if (height < flattest)
                {
                    flattest = height;
                    best = axis;
                }
            }

            return best;
        }

        Quaternion HandInBottleSpace()
        {
            return Quaternion.Inverse(bottle.transform.rotation) * _twistHand.transform.rotation;
        }

        void UpdateTwist()
        {
            if (_twistHand == null) return;

            Vector3 v = HandInBottleSpace() * _handAxis;
            float angle = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg; // grows clockwise seen from above

            bool wasClosed = _thread.IsClosed;
            bool cameOff = _thread.ApplyTwist(_twist.Step(angle));
            ApplySeatedPose();

            if (!wasClosed && _thread.IsClosed) RelayLog.Event("CAP_CLOSED", $"bottle={bottle.id}");
            if (cameOff) ComeOff();
        }

        void ComeOff()
        {
            IXRSelectInteractor hand = _twistHand;
            XRInteractionManager manager = grip.interactionManager;

            manager.SelectExit(hand, grip);
            grip.enabled = false;
            _gripCollider.enabled = false;

            transform.SetParent(null, true);
            _rb.isKinematic = false;
            _grab.enabled = true;
            _free = true;
            _mustClearSeat = true;

            // The hand that unscrewed it keeps hold of it.
            manager.SelectEnter(hand, _grab);
            RelayLog.Event("CAP_OFF", $"bottle={bottle.id}");
        }

        void UpdateFree()
        {
            float distance = Vector3.Distance(transform.position, seat.position);
            if (_mustClearSeat)
            {
                if (distance > clearDistance) _mustClearSeat = false;
                return;
            }

            if (distance > seatDistance) return;

            if (_grab.isSelected)
            {
                if (bottle.IsHeld) Seat(_grab.interactorsSelecting[0], 0f);
            }
            else if (bottle.TiltDegrees < 30f)
            {
                Seat(null, 0f);
            }
        }

        void Seat(IXRSelectInteractor handHoldingCap, float tightDegrees)
        {
            XRInteractionManager manager = _grab.interactionManager;
            if (handHoldingCap != null) manager.SelectExit(handHoldingCap, _grab);

            _grab.enabled = false;
            _rb.isKinematic = true;
            transform.SetParent(bottle.transform, true);
            _free = false;
            _thread = new CapThreadModel(TotalDegrees, tightDegrees);

            grip.enabled = true;
            _gripCollider.enabled = true;
            IgnoreBottleCollisions(); // re-enabling a collider can reset this
            ApplySeatedPose();

            // The hand holding the cap now grips the handle, ready to twist.
            if (handHoldingCap != null) manager.SelectEnter(handHoldingCap, grip);
            if (tightDegrees <= 0f) RelayLog.Event("CAP_SEATED", $"bottle={bottle.id}");
        }

        void ApplySeatedPose()
        {
            float looseness = 1f - _thread.Tightness;
            transform.localPosition = seat.localPosition + Vector3.up * (riseWhenLoose * looseness);
            transform.localRotation = seat.localRotation * Quaternion.Euler(0f, _thread.TightDegrees, 0f);
        }
    }
}
