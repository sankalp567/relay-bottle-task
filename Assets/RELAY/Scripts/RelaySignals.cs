using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Relay
{
    /// <summary>
    /// Everything a logic engine may observe about the person, in one place: where they look, what they grab,
    /// where their hands go, and the backup input. Object ids match AoiTarget, such as "bottle_A" or "glass_1".
    /// It holds nothing about whether a step is really finished; that stays in TaskTruth, for scoring only.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class RelaySignals : MonoBehaviour
    {
        const float ArrivalMargin = 0.03f;

        public GazeObjectTracker gaze;
        [Tooltip("USB foot pedals usually send a key press. Set this to the key yours sends.")]
        public Key backupKey = Key.Space;
        public float doubleTapSeconds = 0.35f;

        /// <summary>The eyes settled on this object.</summary>
        public event Action<string> LookStarted;
        /// <summary>The eyes left this object, after this many seconds.</summary>
        public event Action<string, double> LookEnded;
        /// <summary>(object, hand) A hand closed on it. Hand is "L" or "R". A cap coming off counts as a new grab.</summary>
        public event Action<string, string> Grabbed;
        /// <summary>(object, hand) A hand let go of it.</summary>
        public event Action<string, string> Released;
        /// <summary>(mover, target) "hand_L", "hand_R" or a held "bottle_A" reached the target.</summary>
        public event Action<string, string> Arrived;
        /// <summary>(mover, target) The same mover moved away again.</summary>
        public event Action<string, string> Departed;
        /// <summary>The backup input, with 1 for a tap and 2 for a double tap.</summary>
        public event Action<int> BackupPressed;
        /// <summary>The bench button was pressed (it only exists in the touch condition).</summary>
        public event Action TouchPressed;

        readonly HashSet<string> _inside = new HashSet<string>();
        AoiTarget[] _targets;
        Bottle[] _bottles;
        NearFarInteractor[] _hands;
        int _taps;
        float _tapDeadline;

        void Start()
        {
            _targets = FindObjectsByType<AoiTarget>(FindObjectsInactive.Include);
            _bottles = FindObjectsByType<Bottle>();
            _hands = FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include);

            if (gaze != null)
            {
                gaze.LookStarted += id => LookStarted?.Invoke(id);
                gaze.LookEnded += (id, seconds) => LookEnded?.Invoke(id, seconds);
            }

            foreach (Bottle bottle in _bottles)
            {
                Watch(bottle.Grab.selectEntered, bottle.Grab.selectExited, $"bottle_{bottle.id}");
                if (bottle.cap == null) continue;
                var capGrab = bottle.cap.GetComponent<NoStealGrabInteractable>();
                Watch(capGrab.selectEntered, capGrab.selectExited, $"cap_{bottle.id}");
                Watch(bottle.cap.grip.selectEntered, bottle.cap.grip.selectExited, $"cap_{bottle.id}");
            }
        }

        void Watch(SelectEnterEvent entered, SelectExitEvent exited, string id)
        {
            entered.AddListener(args =>
            {
                string hand = HandName(args.interactorObject);
                RelayLog.Event("GRAB", $"{id} hand={hand}");
                Grabbed?.Invoke(id, hand);
            });

            exited.AddListener(args =>
            {
                string hand = HandName(args.interactorObject);
                RelayLog.Event("RELEASE", $"{id} hand={hand}");
                Released?.Invoke(id, hand);
            });
        }

        static string HandName(IXRInteractor interactor)
        {
            switch (interactor.handedness)
            {
                case InteractorHandedness.Left: return "L";
                case InteractorHandedness.Right: return "R";
                default: return "?";
            }
        }

        public void PressBackup()
        {
            _taps++;
            _tapDeadline = Time.time + doubleTapSeconds;
        }

        public void PressTouch()
        {
            RelayLog.Event("TOUCH_BUTTON");
            TouchPressed?.Invoke();
        }

        void Update()
        {
            Keyboard keys = Keyboard.current;
            if (keys != null && GUIUtility.keyboardControl == 0 && keys[backupKey].wasPressedThisFrame) PressBackup();

            if (_taps > 0 && Time.time >= _tapDeadline)
            {
                int taps = Mathf.Min(_taps, 2);
                _taps = 0;
                RelayLog.Event("BACKUP", $"taps={taps}");
                BackupPressed?.Invoke(taps);
            }

            UpdateArrivals();
        }

        void UpdateArrivals()
        {
            foreach (AoiTarget target in _targets)
            {
                if (!target.gameObject.activeInHierarchy) continue;
                Vector3 center = target.Center;
                float reach = target.radiusMeters + ArrivalMargin;

                foreach (NearFarInteractor hand in _hands)
                {
                    string mover = $"hand_{HandName(hand)}";
                    SetInside(mover, target.id, (hand.transform.position - center).magnitude < reach);
                }

                foreach (Bottle bottle in _bottles)
                {
                    string mover = $"bottle_{bottle.id}";
                    if (!bottle.IsHeld || target.id.EndsWith($"_{bottle.id}") && StepContent.Kind(target.id) != "spot")
                    {
                        SetInside(mover, target.id, false);
                        continue;
                    }

                    // A bottle reaches its spot with its base, and a glass with its mouth.
                    Vector3 point = StepContent.Kind(target.id) == "spot" ? bottle.transform.position : bottle.mouth.position;
                    Vector3 offset = point - center;
                    offset.y = 0f;
                    SetInside(mover, target.id, offset.magnitude < reach);
                }
            }
        }

        void SetInside(string mover, string target, bool inside)
        {
            string key = $"{mover}|{target}";
            if (inside)
            {
                if (!_inside.Add(key)) return;
                RelayLog.Event("ARRIVED", $"{mover} at {target}");
                Arrived?.Invoke(mover, target);
            }
            else if (_inside.Remove(key))
            {
                RelayLog.Event("DEPARTED", $"{mover} from {target}");
                Departed?.Invoke(mover, target);
            }
        }
    }
}
