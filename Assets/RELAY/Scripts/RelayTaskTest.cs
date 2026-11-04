#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Relay
{
    /// <summary>A hand the test can move and close by script, with no headset.</summary>
    public class ScriptedHand : XRDirectInteractor
    {
        public bool holding;
        public override bool isSelectActive => holding;
    }

    /// <summary>
    /// Plays the whole 5-step task with two scripted hands in the real scene, including the common
    /// mistakes. Run it from RELAY > Run Task Test. Selection is forced, so this checks the task logic,
    /// not how reaching feels: use the simulator or headset for that.
    /// </summary>
    public class RelayTaskTest : MonoBehaviour
    {
        const string RunFlag = "RelayRunTaskTest";

        readonly List<string> _events = new List<string>();
        XRInteractionManager _manager;
        ScriptedHand _hand1;
        ScriptedHand _hand2;
        TaskTruth _truth;
        int _passed;
        int _failed;

        public static void RequestRun() => SessionState.SetBool(RunFlag, true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            if (!SessionState.GetBool(RunFlag, false)) return;
            SessionState.SetBool(RunFlag, false);
            new GameObject("RelayTaskTest").AddComponent<RelayTaskTest>();
        }

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += OnLog;

            _manager = FindAnyObjectByType<XRInteractionManager>();
            _truth = FindAnyObjectByType<TaskTruth>();
            _hand1 = MakeHand("Hand1");
            _hand2 = MakeHand("Hand2");
            yield return Wait(0.3f);

            Bottle a = FindBottle("A");
            Bottle b = FindBottle("B");
            Bottle c = FindBottle("C");
            Glass glass1 = FindGlass("1");
            Glass glass2 = FindGlass("2");
            BottlePour pour = a.GetComponent<BottlePour>();
            var capGrab = a.cap.GetComponent<NoStealGrabInteractable>();

            Check(_truth.enabled && _truth.CurrentIndex == 0, "the task loads and starts at step 1");
            Check(a.cap != null && !a.cap.IsOff && a.cap.IsClosed, "caps start closed");
            Check(a.IsAtHome(), "bottles start on their spots");

            // --- Mistake: grabbing the wrong bottle ---
            PlaceHandOn(_hand2, b);
            yield return Grab(_hand2, b.Grab);
            Check(Saw("SLIP_WRONG_BOTTLE got=B want=A"), "grabbing bottle B logs a slip");
            Check(_truth.CurrentIndex == 0, "the wrong bottle doesn't complete step 1");
            yield return Release(_hand2);
            yield return Wait(0.3f);
            Check(b.IsAtHome(), "bottle B snaps back onto its spot");

            // --- Mistake: dropping a bottle off the table ---
            PlaceHandOn(_hand2, b);
            yield return Grab(_hand2, b.Grab);
            _hand2.transform.position = new Vector3(0f, 0.65f, -0.3f);
            yield return Wait(0.2f);
            yield return Release(_hand2);
            yield return Wait(1f);
            Check(Saw("DROP bottle_B"), "a bottle dropped off the table is logged");
            Check(b.transform.position.y > 0.7f, "a dropped bottle returns to the table");

            // --- The cap needs two hands: its handle only works while the other hand holds the bottle ---
            Check(!c.cap.grip.IsSelectableBy((IXRSelectInteractor)_hand2), "a cap can't be gripped on a bottle nobody holds");
            Check(c.cap.IsClosed, "the cap stays closed");

            // --- Step 1: pick up the bottle ---
            PlaceHandOn(_hand1, a);
            yield return Grab(_hand1, a.Grab);
            yield return Wait(0.3f);
            Check(_truth.CurrentIndex == 1, "step 1 finishes when bottle A is held");
            Check(!a.Grab.IsSelectableBy((IXRSelectInteractor)_hand2), "the other hand can't take the bottle");
            Check(a.Grab.IsSelectableBy((IXRSelectInteractor)_hand1), "the holding hand keeps the bottle");

            // Lift it, and record how the bottle sits in the hand so its pose can be commanded.
            _hand1.transform.position += Vector3.up * 0.2f;
            yield return Wait(0.3f);
            Check(a.transform.position.y > 0.9f, "the bottle follows the hand");
            Vector3 gripPos = _hand1.transform.InverseTransformPoint(a.transform.position);
            Quaternion gripRot = Quaternion.Inverse(_hand1.transform.rotation) * a.transform.rotation;

            // --- Pouring with the cap on does nothing ---
            MoveBottleMouthOver(a, glass1.transform.position, gripPos, gripRot, 110f);
            yield return Wait(0.5f);
            Check(!pour.IsFlowing && Mathf.Approximately(glass1.filledMl, 0f), "a closed bottle doesn't pour");
            MoveBottleUpright(new Vector3(-0.1f, 1.0f, 0.45f), gripPos, gripRot);
            yield return Wait(0.2f);

            // --- Step 2: open the cap, with a re-grip part way ---
            PoseForTwist(_hand2, a, 0f);
            yield return Grab(_hand2, a.cap.grip);
            yield return Twist(_hand2, a, -300f);
            Check(!a.cap.IsOff && !a.cap.IsClosed, "part way, the cap is loose but still on");
            yield return Release(_hand2);
            Check(!a.cap.IsClosed, "letting go keeps the cap's progress");
            PoseForTwist(_hand2, a, 0f);
            yield return Grab(_hand2, a.cap.grip);
            yield return Twist(_hand2, a, -400f);
            Check(a.cap.IsOff, "the cap comes off after about 1.5 turns");
            Check(Saw("CAP_OFF bottle=A"), "the cap coming off is logged");
            Check(capGrab.interactorsSelecting.Contains(_hand2), "the twisting hand ends up holding the cap");
            yield return Wait(0.2f);
            Check(_truth.CurrentIndex == 2, "step 2 finishes when the cap is off");

            // The cap must follow the hand, and be clear of the bottle before it can go back on.
            Vector3 capBefore = a.cap.transform.position;
            _hand2.transform.position += new Vector3(0.1f, 0.05f, -0.1f);
            yield return Wait(0.3f);
            Check(Vector3.Distance(capBefore, a.cap.transform.position) > 0.1f, "the free cap follows the hand");

            // --- Mistake: pouring into the wrong glass ---
            MoveBottleMouthOver(a, glass2.transform.position, gripPos, gripRot, 110f);
            yield return Wait(0.3f);
            Check(pour.IsFlowing && glass2.filledMl > 0f, "an open tipped bottle pours into the glass below");
            Check(Saw("SLIP_WRONG_GLASS got=2 want=1"), "pouring into the wrong glass logs a slip");
            MoveBottleUpright(new Vector3(-0.1f, 1.0f, 0.45f), gripPos, gripRot);
            yield return Wait(0.8f);
            Check(_truth.CurrentIndex == 2, "the wrong glass doesn't complete the pour step");

            // --- Step 3: pour to the line ---
            MoveBottleMouthOver(a, glass1.transform.position, gripPos, gripRot, 110f);
            float deadline = Time.time + 6f;
            while (glass1.Fill01 < 0.5f && Time.time < deadline) yield return null;
            Check(glass1.Fill01 >= 0.5f, "glass 1 fills while pouring");
            MoveBottleUpright(new Vector3(-0.1f, 1.0f, 0.45f), gripPos, gripRot);
            yield return null;
            yield return null;
            Check(!pour.IsFlowing, "levelling the bottle stops the water");
            yield return Wait(0.8f);
            Check(_truth.CurrentIndex == 3, "step 3 finishes after pouring to the line and stopping");
            float fill = glass1.filledMl;
            float pouredTotal = fill + glass2.filledMl;
            Check(Mathf.Abs(a.capacityMl - a.volumeMl - pouredTotal) < 1f, "the bottle loses exactly the water it poured");

            // --- Mistake: spilling ---
            MoveBottleMouthOver(a, new Vector3(0f, 0.75f, 0.7f), gripPos, gripRot, 110f);
            yield return Wait(0.2f);
            Check(Saw("SPILL bottle=A"), "pouring with no glass below logs a spill");
            Check(Mathf.Approximately(glass1.filledMl, fill), "spilled water doesn't reach the glass");
            MoveBottleUpright(new Vector3(-0.1f, 1.0f, 0.45f), gripPos, gripRot);
            yield return Wait(0.2f);

            // --- Step 4: put the cap back on and close it ---
            _hand2.transform.position += a.cap.seat.position - a.cap.transform.position;
            yield return Wait(0.2f);
            Check(!a.cap.IsOff, "bringing the cap to the mouth seats it");
            Check(a.cap.grip.interactorsSelecting.Contains(_hand2), "the hand can twist straight away after seating");
            PoseForTwist(_hand2, a, 0f);
            yield return null;
            yield return Twist(_hand2, a, 560f);
            Check(a.cap.IsClosed, "twisting clockwise closes the cap");
            yield return Release(_hand2);
            yield return Wait(0.2f);
            Check(_truth.CurrentIndex == 4, "step 4 finishes when the cap is closed");

            // --- Step 5: put the bottle back ---
            MoveBottleUpright(a.HomePosition + new Vector3(0.03f, 0.02f, 0.02f), gripPos, gripRot);
            yield return Wait(0.2f);
            yield return Release(_hand1);
            yield return Wait(0.5f);
            Check(a.IsAtHome(), "the bottle snaps onto its spot when released nearby");
            Check(_truth.IsFinished, "step 5 finishes and the task ends");
            Check(Saw("TASK_FINISHED"), "the end of the task is logged");

            // The whole point of RELAY: finishing steps must not move the panel by itself.
            var panel = FindAnyObjectByType<InstructionPanel>();
            Check(panel.ShownStep == -1 && panel.GhostStep == -1, "the panel doesn't follow the true step");

            // --- Gaze ---
            var gaze = FindAnyObjectByType<GazeInput>();
            gaze.mode = GazeInput.Mode.Head; // no eye tracker here
            Camera cam = Camera.main;
            Check(cam != null, "the scene has a main camera");
            if (cam != null)
            {
                yield return LookAt(cam, glass1.transform.position + Vector3.up * 0.06f, 0.3f);
                Check(Saw("LOOK_START glass_1"), "looking at glass 1 is detected");
                yield return LookAt(cam, a.cap.transform.position, 0.3f);
                Check(Saw("LOOK_START cap_A"), "a small cap on a big bottle resolves to the cap");
                Check(Saw("LOOK_END glass_1"), "looking away ends the earlier look");
                yield return LookAt(cam, panel.transform.position, 0.3f);
                Check(Saw("LOOK_START display"), "looking at the panel is detected");
            }

            Debug.Log($"[RELAY] TEST RESULT passed={_passed} failed={_failed}");
            Application.logMessageReceived -= OnLog;

            if (Application.isBatchMode) EditorApplication.Exit(_failed == 0 ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (message.StartsWith("[RELAY ")) _events.Add(message);
        }

        bool Saw(string text) => _events.Exists(e => e.Contains(text));

        void Check(bool condition, string what)
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"[RELAY] PASS {what}");
            }
            else
            {
                _failed++;
                Debug.LogError($"[RELAY] FAIL {what}");
            }
        }

        static ScriptedHand MakeHand(string handName)
        {
            var go = new GameObject(handName);
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.001f;
            go.transform.position = new Vector3(0f, 2f, 0f);
            return go.AddComponent<ScriptedHand>();
        }

        static WaitForSeconds Wait(float seconds) => new WaitForSeconds(seconds);

        IEnumerator Grab(ScriptedHand hand, IXRSelectInteractable target)
        {
            hand.holding = true;
            _manager.SelectEnter(hand, target);
            yield return null;
            yield return null;
        }

        static IEnumerator Release(ScriptedHand hand)
        {
            hand.holding = false;
            yield return null;
            yield return null;
        }

        // A grab pulls the object to the hand, so the hand must already be at the bottle, as a real one is.
        static void PlaceHandOn(ScriptedHand hand, Bottle bottle)
        {
            hand.transform.SetPositionAndRotation(bottle.transform.position + Vector3.up * 0.1f, Quaternion.identity);
        }

        static void PoseForTwist(ScriptedHand hand, Bottle bottle, float angle)
        {
            hand.transform.SetPositionAndRotation(
                bottle.cap.seat.position + Vector3.up * 0.03f,
                bottle.transform.rotation * Quaternion.Euler(0f, angle, 0f));
        }

        static IEnumerator Twist(ScriptedHand hand, Bottle bottle, float degrees)
        {
            const float DegreesPerSecond = 300f;
            float turned = 0f;
            while (turned < Mathf.Abs(degrees))
            {
                turned += DegreesPerSecond * Time.deltaTime;
                float angle = Mathf.Sign(degrees) * Mathf.Min(turned, Mathf.Abs(degrees));
                hand.transform.rotation = bottle.transform.rotation * Quaternion.Euler(0f, angle, 0f);
                yield return null;
            }
        }

        void MoveBottleUpright(Vector3 position, Vector3 gripPos, Quaternion gripRot)
        {
            SetBottlePose(position, Quaternion.identity, gripPos, gripRot);
        }

        void MoveBottleMouthOver(Bottle bottle, Vector3 point, Vector3 gripPos, Quaternion gripRot, float tilt)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, tilt);
            var mouthWorld = new Vector3(point.x, point.y + 0.2f, point.z);
            SetBottlePose(mouthWorld - rotation * bottle.mouth.localPosition, rotation, gripPos, gripRot);
        }

        void SetBottlePose(Vector3 position, Quaternion rotation, Vector3 gripPos, Quaternion gripRot)
        {
            Quaternion handRotation = rotation * Quaternion.Inverse(gripRot);
            _hand1.transform.SetPositionAndRotation(position - handRotation * gripPos, handRotation);
        }

        static IEnumerator LookAt(Camera cam, Vector3 point, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                cam.transform.rotation = Quaternion.LookRotation(point - cam.transform.position);
                yield return null;
            }
        }

        static Bottle FindBottle(string id)
        {
            foreach (Bottle bottle in FindObjectsByType<Bottle>())
            {
                if (bottle.id == id) return bottle;
            }

            return null;
        }

        static Glass FindGlass(string id)
        {
            foreach (Glass glass in FindObjectsByType<Glass>())
            {
                if (glass.id == id) return glass;
            }

            return null;
        }
    }
}
#endif
