using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Relay
{
    /// <summary>One frame of desk-mode input. The test fills this in directly instead of using a real mouse.</summary>
    public struct DeskInput
    {
        public Vector2 pointer;
        public bool click;
        public bool switchHand;
        public float twist;  // -1 opens, +1 closes
        public float tilt;   // -1 tilts left, +1 tilts right
        public float height; // -1 lowers, +1 raises
        public bool looking;
        public Vector2 lookDelta;
    }

    /// <summary>
    /// The whole task with a mouse and a few keys, for working without a headset. A click grabs and a second
    /// click lets go, so a hand keeps hold while you use the other. It switches itself off on a headset.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class RelayDeskMode : MonoBehaviour
    {
        const float SeatMagnetRadius = 0.06f;
        const float MinHoldHeight = 0.78f;
        const float MaxHoldHeight = 1.5f;

        public Vector3 eyePosition = new Vector3(0f, 1.45f, -0.05f);
        public Vector3 lookAtPoint = new Vector3(0f, 0.92f, 0.6f);
        public float fieldOfView = 70f;
        public float twistDegreesPerSecond = 200f;
        public float tiltDegreesPerSecond = 90f;
        public float tiltReturnDegreesPerSecond = 220f;
        public float maxTiltDegrees = 130f;
        public float heightMetresPerSecond = 0.4f;
        public float lookSensitivity = 0.12f;
        [Tooltip("The test turns this off and calls Step directly.")]
        public bool readMouseAndKeyboard = true;

        static readonly Quaternion RestingHand = Quaternion.Euler(20f, 0f, 0f);
        readonly RaycastHit[] _hits = new RaycastHit[16];

        RelayHand[] _hands;
        Camera _camera;
        Vector3 _lookAngles;
        Vector2 _lastPointer;
        int _active = 1;
        readonly float[] _yaw = new float[2];
        readonly float[] _tilt = new float[2];
        readonly float[] _holdHeight = { 1f, 1f };
        readonly int[] _gripFrame = new int[2];
        readonly object[] _lastHeld = new object[2];

        public RelayHand ActiveHand => _hands != null && _hands.Length == 2 ? _hands[_active] : null;
        public RelayHand LeftHand => _hands != null && _hands.Length == 2 ? _hands[0] : null;
        public RelayHand RightHand => _hands != null && _hands.Length == 2 ? _hands[1] : null;

        void Start()
        {
            if (XRSettings.isDeviceActive)
            {
                RelayLog.Event("DESK_MODE", "off: a headset is active");
                enabled = false;
                return;
            }

            // Both can't drive the hands at once.
            XRInteractionSimulator simulator = FindAnyObjectByType<XRInteractionSimulator>();
            if (simulator != null) simulator.gameObject.SetActive(false);

            _camera = Camera.main;
            _hands = RelayHand.TakeOverBoth();
            if (_camera == null || _hands.Length != 2)
            {
                Debug.LogWarning("[RELAY] Desk mode needs the XR rig in the scene. Run RELAY > Build Bottle Scene.");
                enabled = false;
                return;
            }

            var headDriver = _camera.GetComponent<TrackedPoseDriver>();
            if (headDriver != null) headDriver.enabled = false;
            _camera.fieldOfView = fieldOfView;
            _camera.transform.SetPositionAndRotation(eyePosition, Quaternion.LookRotation(lookAtPoint - eyePosition));
            _lookAngles = _camera.transform.eulerAngles;

            _hands[0].SetPose(new Vector3(-0.3f, 0.95f, 0.2f), RestingHand);
            _hands[1].SetPose(new Vector3(0.3f, 0.95f, 0.2f), RestingHand);
            RelayLog.Event("DESK_MODE", "on");
        }

        void Update()
        {
            if (readMouseAndKeyboard) Step(ReadMouseAndKeyboard());
        }

        DeskInput ReadMouseAndKeyboard()
        {
            var input = new DeskInput { pointer = _lastPointer };
            Mouse mouse = Mouse.current;
            if (mouse == null) return input;

            input.pointer = mouse.position.ReadValue();
            input.click = mouse.leftButton.wasPressedThisFrame;
            input.looking = mouse.rightButton.isPressed;
            input.lookDelta = mouse.delta.ReadValue();

            // Don't act on keys while someone is typing in the session panel.
            Keyboard keys = Keyboard.current;
            if (keys == null || GUIUtility.keyboardControl != 0) return input;

            input.switchHand = keys.tabKey.wasPressedThisFrame;
            input.twist = (keys.eKey.isPressed ? 1f : 0f) - (keys.qKey.isPressed ? 1f : 0f);
            input.tilt = (keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f);
            input.height = (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f);
            return input;
        }

        /// <summary>Applies one frame of input.</summary>
        public void Step(DeskInput input)
        {
            _lastPointer = input.pointer;
            if (input.switchHand) _active = 1 - _active;

            if (input.looking)
            {
                _lookAngles.y += input.lookDelta.x * lookSensitivity;
                _lookAngles.x -= input.lookDelta.y * lookSensitivity;
                _camera.transform.rotation = Quaternion.Euler(_lookAngles.x, _lookAngles.y, 0f);
            }

            if (input.click && !RelayUi.IsOver(input.pointer))
            {
                _hands[_active].SetGrip(!_hands[_active].IsGripping);
                _gripFrame[_active] = Time.frameCount;
            }

            for (int i = 0; i < 2; i++) UpdateGripState(i);

            _yaw[_active] += input.twist * twistDegreesPerSecond * Time.deltaTime;
            _holdHeight[_active] = Mathf.Clamp(
                _holdHeight[_active] + input.height * heightMetresPerSecond * Time.deltaTime, MinHoldHeight, MaxHoldHeight);

            // Tilting lasts only while the key is held, so the water stops when you let go.
            _tilt[_active] = input.tilt != 0f
                ? Mathf.Clamp(_tilt[_active] + input.tilt * tiltDegreesPerSecond * Time.deltaTime, -maxTiltDegrees, maxTiltDegrees)
                : Mathf.MoveTowards(_tilt[_active], 0f, tiltReturnDegreesPerSecond * Time.deltaTime);

            for (int i = 0; i < 2; i++)
            {
                Vector3 position = _hands[i].Root.position;
                if (TryGetTarget(i, input.pointer, out Vector3 target)) position = target;
                _hands[i].SetPose(position, HandRotation(i));
            }
        }

        void UpdateGripState(int index)
        {
            RelayHand hand = _hands[index];
            object held = hand.Held;

            // A grip that closed on nothing opens again, so a missed click doesn't leave a closed fist.
            if (hand.IsGripping && held == null && Time.frameCount - _gripFrame[index] > 3) hand.SetGrip(false);

            if (held != null && !ReferenceEquals(held, _lastHeld[index]))
            {
                // Lift a little on grabbing, so a bottle clears the table.
                bool isBottle = HeldBottle(index) != null;
                _holdHeight[index] = Mathf.Clamp(hand.Root.position.y + (isBottle ? 0.12f : 0.03f), MinHoldHeight, MaxHoldHeight);
            }

            _lastHeld[index] = held;
        }

        Quaternion HandRotation(int index)
        {
            return Quaternion.AngleAxis(-_tilt[index], Vector3.forward) * Quaternion.Euler(0f, _yaw[index], 0f) * RestingHand;
        }

        bool TryGetTarget(int index, Vector2 pointer, out Vector3 target)
        {
            target = default;

            // A hand twisting a cap stays on the cap, wherever the bottle goes.
            BottleCap twisted = TwistedCap(index);
            if (twisted != null)
            {
                target = twisted.seat.position + Vector3.up * 0.03f;
                return true;
            }

            if (index != _active) return false;

            Ray ray = _camera.ScreenPointToRay(pointer);
            BottleCap heldCap = HeldCap(index);
            if (heldCap != null && TryFindSeatNear(ray, heldCap, out Vector3 seat))
            {
                // Pointing near the bottle's mouth brings the cap onto it.
                target = seat + (_hands[index].Root.position - heldCap.transform.position);
                return true;
            }

            if (_hands[index].Held != null) return TryIntersectHoldHeight(ray, _holdHeight[index], out target);

            if (!TryRaycastScene(ray, out RaycastHit hit)) return false;
            target = hit.point + hit.normal * 0.02f;
            return true;
        }

        static bool TryFindSeatNear(Ray ray, BottleCap cap, out Vector3 seat)
        {
            seat = cap.seat.position;
            Vector3 toSeat = seat - ray.origin;
            float along = Vector3.Dot(toSeat, ray.direction);
            return cap.bottle.IsHeld && (toSeat - ray.direction * along).magnitude < SeatMagnetRadius;
        }

        static bool TryIntersectHoldHeight(Ray ray, float height, out Vector3 point)
        {
            point = default;
            if (Mathf.Abs(ray.direction.y) < 0.001f) return false;
            float distance = (height - ray.origin.y) / ray.direction.y;
            if (distance <= 0f) return false;
            point = ray.origin + ray.direction * distance;
            return true;
        }

        bool TryRaycastScene(Ray ray, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            int count = Physics.RaycastNonAlloc(ray, _hits, 6f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider is CharacterController) continue; // the rig's own body
                if (!found || _hits[i].distance < nearest.distance)
                {
                    nearest = _hits[i];
                    found = true;
                }
            }

            return found;
        }

        Bottle HeldBottle(int index)
        {
            object held = _hands[index].Held;
            return held is Component component ? component.GetComponent<Bottle>() : null;
        }

        BottleCap HeldCap(int index)
        {
            object held = _hands[index].Held;
            return held is Component component ? component.GetComponent<BottleCap>() : null;
        }

        BottleCap TwistedCap(int index)
        {
            if (!(_hands[index].Held is CapGripInteractable grip)) return null;
            Bottle bottle = grip.GetComponentInParent<Bottle>();
            return bottle != null ? bottle.cap : null;
        }

        string Describe(int index)
        {
            object held = _hands[index].Held;
            if (held == null) return "empty";
            if (TwistedCap(index) != null) return "twisting the cap";
            if (HeldCap(index) != null) return "holding a cap";
            Bottle bottle = HeldBottle(index);
            return bottle != null ? $"holding bottle {bottle.id}" : "holding something";
        }

        void OnGUI()
        {
            if (_hands == null || _hands.Length != 2) return;

            var area = new Rect(10f, Screen.height - 215f, 330f, 205f);
            RelayUi.Register("desk", area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("<b>Desk mode</b>", RelayUi.RichLabel);
            GUILayout.Label($"Using: <b>{_hands[_active].Name} hand</b>  ({Describe(_active)})", RelayUi.RichLabel);
            GUILayout.Label($"Other hand: {Describe(1 - _active)}");
            GUILayout.Space(4f);
            GUILayout.Label("Move the mouse: move this hand");
            GUILayout.Label("Click: grab, click again: let go");
            GUILayout.Label("Tab: switch hands");
            GUILayout.Label("Q / E: twist to open / close a cap");
            GUILayout.Label("A / D: tilt left / right to pour");
            GUILayout.Label("W / S: raise / lower  |  Right mouse: look");
            GUILayout.EndArea();
        }
    }

    /// <summary>Lets clicks on the on-screen panels stay on the panels, instead of also grabbing things.</summary>
    public static class RelayUi
    {
        static readonly Dictionary<string, Rect> Areas = new Dictionary<string, Rect>();
        static GUIStyle _richLabel;

        public static GUIStyle RichLabel => _richLabel ?? (_richLabel = new GUIStyle(GUI.skin.label) { richText = true });

        public static void Register(string key, Rect area) => Areas[key] = area;

        /// <summary>Takes a mouse position, whose y runs upward. On-screen panels run downward.</summary>
        public static bool IsOver(Vector2 pointer)
        {
            var flipped = new Vector2(pointer.x, Screen.height - pointer.y);
            foreach (Rect area in Areas.Values)
            {
                if (area.Contains(flipped)) return true;
            }

            return false;
        }
    }
}
