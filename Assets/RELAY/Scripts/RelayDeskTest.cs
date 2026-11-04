#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Relay
{
    /// <summary>
    /// Runs a full recorded trial through desk mode, using the XR rig's real hands and real near-grab
    /// detection, then checks the data files and the reset to the next trial. Run it from RELAY > Run Full Trial Test.
    /// </summary>
    public class RelayDeskTest : MonoBehaviour
    {
        const string RunFlag = "RelayRunDeskTest";

        readonly List<string> _events = new List<string>();
        RelayDeskMode _desk;
        Camera _cam;
        DeskInput _input;
        int _passed;
        int _failed;

        public static void RequestRun() => SessionState.SetBool(RunFlag, true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            if (!SessionState.GetBool(RunFlag, false)) return;
            SessionState.SetBool(RunFlag, false);
            var go = new GameObject("RelayDeskTest");
            DontDestroyOnLoad(go);
            go.AddComponent<RelayDeskTest>();
        }

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += OnLog;

            // Run after the scene's own Start methods.
            yield return null;
            yield return null;

            _desk = FindAnyObjectByType<RelayDeskMode>();
            _cam = Camera.main;
            Check(_desk != null && _desk.enabled && _desk.ActiveHand != null, "desk mode takes over the rig's real hands");
            if (_failed > 0)
            {
                Finish();
                yield break;
            }

            _desk.readMouseAndKeyboard = false;
            RelaySession session = FindAnyObjectByType<RelaySession>();
            TaskTruth truth = FindAnyObjectByType<TaskTruth>();
            InstructionPanel panel = FindAnyObjectByType<InstructionPanel>();
            Bottle a = FindBottle("A");
            Bottle b = FindBottle("B");
            Glass glass1 = FindGlass("1");
            RelayHand capHand = _desk.RightHand;
            RelayHand bottleHand = _desk.LeftHand;

            session.Participant = "TEST";
            session.SelectCondition("Manual");
            session.StartTrial();
            Check(session.IsTrialRunning && panel.ShownStep == 0, "starting a trial shows step 1");

            // Far grabbing must be off: a click on empty table reaches nothing.
            yield return PointAt(new Vector3(0f, 0.75f, 0.68f), 5);
            yield return Click();
            yield return Frames(6);
            Check(_desk.ActiveHand.Held == null && !_desk.ActiveHand.IsGripping, "a click with nothing in reach grabs nothing, and the hand reopens");

            // Reaching for the wrong bottle is a real near grab, and is logged.
            yield return PointAt(b.transform.position + Vector3.up * 0.1f, 5);
            yield return Click();
            yield return Frames(4);
            Check(b.IsHeld, "a real hand reaching a bottle grabs it");
            Check(Saw("SLIP_WRONG_BOTTLE got=B want=A"), "grabbing the wrong bottle is logged");
            Check(Saw("GRAB bottle_B hand=R"), "the grab signal names the object and hand");
            yield return Click();
            yield return Frames(30);
            Check(!b.IsHeld, "a second click lets go");

            // Step 1: left hand takes bottle A.
            yield return SwitchHand();
            yield return PointAt(a.transform.position + Vector3.up * 0.2f, 5);
            yield return Click();
            yield return Frames(6);
            Check(a.IsHeld && !a.cap.IsBeingTwisted, "reaching for the top of a free bottle picks the bottle up");
            Check(_desk.ActiveHand == bottleHand, "the left hand holds the bottle");
            Check(truth.CurrentIndex == 1, "step 1 is truly done");
            yield return Frames(10);
            Check(a.transform.position.y > 0.8f, "grabbing lifts the bottle clear of the table");

            // Step 2: right hand twists the cap off, while the left keeps hold.
            yield return SwitchHand();
            Check(_desk.ActiveHand == capHand && a.IsHeld, "switching hands keeps the grip");
            yield return PointAt(a.cap.transform.position, 5);
            yield return Click();
            yield return Frames(6);
            Check(a.cap.IsBeingTwisted, "the free hand reaching the cap grips the handle, not the bottle");

            float deadline = Time.time + 8f;
            _input.twist = -1f;
            while (!a.cap.IsOff && Time.time < deadline) yield return Tick();
            _input.twist = 0f;
            Check(a.cap.IsOff, "twisting takes the cap off");
            yield return Frames(4);
            Check(ReferenceEquals(capHand.Held, a.cap.GetComponent<NoStealGrabInteractable>()), "the real hand ends up holding the cap");
            Check(truth.CurrentIndex == 2, "step 2 is truly done");

            // Move the cap well clear, so it can go back on later.
            yield return PointAtHeldHeight(capHand, new Vector3(0.05f, 0f, 0.3f), 20);
            Check(Vector3.Distance(a.cap.transform.position, a.cap.seat.position) > 0.08f, "the cap moves away with the hand");

            // Step 3: left hand pours into glass 1, adjusting so the stream lands inside it.
            yield return SwitchHand();
            deadline = Time.time + 16f;
            _input.tilt = 1f;
            bool savedPicture = false;
            while (glass1.Fill01 < 0.5f && Time.time < deadline)
            {
                if (!savedPicture && glass1.Fill01 > 0.3f)
                {
                    savedPicture = true;
                    string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Previews"));
                    Directory.CreateDirectory(folder);
                    RelayPanelTour.Render(Path.Combine(folder, "scene_pouring.png"), _cam.transform.position, new Vector3(0.05f, 0.85f, 0.5f), 62f, 1600, 900);
                }


                Vector3 toMouth = a.mouth.position - bottleHand.Root.position;
                toMouth.y = 0f;
                Vector3 wanted = glass1.transform.position - toMouth;
                wanted.y = bottleHand.Root.position.y;
                _input.pointer = _cam.WorldToScreenPoint(wanted);
                yield return Tick();
            }

            _input.tilt = 0f;
            Check(glass1.Fill01 >= 0.5f, "tilting over the glass fills it");
            yield return Frames(90);
            Check(truth.CurrentIndex == 3, "step 3 is truly done once the bottle is level");

            // Step 4: right hand brings the cap to the mouth and closes it.
            yield return SwitchHand();
            _input.pointer = _cam.WorldToScreenPoint(a.cap.seat.position);
            yield return Frames(10);
            Check(!a.cap.IsOff, "pointing the cap at the mouth seats it");
            Check(a.cap.IsBeingTwisted, "the same hand can twist straight away");
            deadline = Time.time + 8f;
            _input.twist = 1f;
            while (!a.cap.IsClosed && Time.time < deadline) yield return Tick();
            _input.twist = 0f;
            Check(a.cap.IsClosed, "twisting the other way closes the cap");
            yield return Click();
            yield return Frames(6);
            Check(truth.CurrentIndex == 4, "step 4 is truly done");

            // Step 5: left hand puts the bottle back on its spot.
            yield return SwitchHand();
            Vector3 toBase = bottleHand.Root.position - a.transform.position;
            toBase.y = 0f;
            Vector3 aboveSpot = a.HomePosition + toBase;
            aboveSpot.y = bottleHand.Root.position.y;
            _input.pointer = _cam.WorldToScreenPoint(aboveSpot);
            yield return Frames(10);
            yield return Click();
            yield return Frames(20);
            Check(a.IsAtHome(), "the bottle snaps onto its spot");
            Check(truth.IsFinished, "all 5 steps are truly done");

            // The panel never moved by itself, so a real engine has to earn every advance.
            Check(panel.ShownStep == 0, "the panel didn't follow the true step");

            // The experimenter advances now, after the truth: each advance is marked with its delay.
            var manual = FindAnyObjectByType<ManualEngine>();
            FindAnyObjectByType<RelaySignals>().PressBackup();
            yield return WaitSeconds(0.5f);
            Check(manual.Current == 1, "the backup key advances the step");
            Check(Saw("ADVANCE_DELAY panel_step=2"), "a late advance is marked with its delay");

            session.EndTrial("test");
            Check(session.TrialHasEnded, "ending a trial stops recording");

            string trialsPath = Path.Combine(TrialRecorder.DataFolder, "trials.csv");
            string[] rows = File.Exists(trialsPath) ? File.ReadAllLines(trialsPath) : new string[0];
            Check(rows.Length >= 2 && rows[rows.Length - 1].StartsWith("TEST,Manual,task_one_round.json,1,"), "trials.csv gets a row for this trial");
            string[] samples = Directory.GetFiles(TrialRecorder.DataFolder, "TEST_Manual_t01_*_samples.csv");
            Check(samples.Length > 0 && new FileInfo(samples[samples.Length - 1]).Length > 2000, "the per-frame samples file has data");
            string[] eventFiles = Directory.GetFiles(TrialRecorder.DataFolder, "TEST_Manual_t01_*_events.csv");
            Check(eventFiles.Length > 0 && File.ReadAllText(eventFiles[eventFiles.Length - 1]).Contains("SLIP_WRONG_BOTTLE"), "the events file has the logged events");

            session.NextTrial();
            yield return null;
            yield return null;
            yield return null;

            session = FindAnyObjectByType<RelaySession>();
            a = FindBottle("A");
            glass1 = FindGlass("1");
            Check(session.Trial == 2 && !session.TrialHasEnded, "next trial counts up and is ready to start");
            Check(a.cap.IsClosed && a.IsAtHome() && Mathf.Approximately(glass1.filledMl, 0f), "next trial resets the bench");

            // The touch condition: a real hand presses the bench button, which only exists in this condition.
            TouchButton button = FindAnyObjectByType<TouchEngine>().button;
            panel = FindAnyObjectByType<InstructionPanel>();
            Check(!button.gameObject.activeSelf, "the bench button is hidden outside the touch condition");
            _desk = FindAnyObjectByType<RelayDeskMode>();
            _desk.readMouseAndKeyboard = false;
            _cam = Camera.main;
            yield return Frames(3);
            session.SelectCondition("Touch");
            session.StartTrial();
            Check(button.gameObject.activeSelf, "the bench button appears in the touch condition");
            yield return PointAt(button.transform.position + Vector3.up * 0.03f, 6);
            yield return Click();
            yield return Frames(5);
            Check(panel.ShownStep == 1, "pressing the bench button with a real hand advances the step");
            session.EndTrial("test");
            session.SelectCondition("Manual");

            foreach (string file in Directory.GetFiles(TrialRecorder.DataFolder, "TEST_*")) File.Delete(file);
            RemoveTestRows(trialsPath);
            Finish();
        }

        static void RemoveTestRows(string path)
        {
            if (!File.Exists(path)) return;
            var kept = new List<string>();
            foreach (string row in File.ReadAllLines(path))
            {
                if (!row.StartsWith("TEST,")) kept.Add(row);
            }

            if (kept.Count <= 1) File.Delete(path);
            else File.WriteAllLines(path, kept);
        }

        void Finish()
        {
            Debug.Log($"[RELAY] FULL TRIAL RESULT passed={_passed} failed={_failed}");
            Application.logMessageReceived -= OnLog;
            if (Application.isBatchMode) EditorApplication.Exit(_failed == 0 ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        IEnumerator PointAt(Vector3 world, int frames)
        {
            _input.pointer = _cam.WorldToScreenPoint(world);
            yield return Frames(frames);
        }

        IEnumerator PointAtHeldHeight(RelayHand hand, Vector3 horizontal, int frames)
        {
            var world = new Vector3(horizontal.x, hand.Root.position.y, horizontal.z);
            _input.pointer = _cam.WorldToScreenPoint(world);
            yield return Frames(frames);
        }

        IEnumerator Click()
        {
            _input.click = true;
            yield return Tick();
            _input.click = false;
        }

        IEnumerator SwitchHand()
        {
            _input.switchHand = true;
            yield return Tick();
            _input.switchHand = false;
            yield return Tick();
        }

        IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return Tick();
        }

        IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return Tick();
        }

        object Tick()
        {
            _desk.Step(_input);
            return null;
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

        static Bottle FindBottle(string id) => Array.Find(FindObjectsByType<Bottle>(), bottle => bottle.id == id);
        static Glass FindGlass(string id) => Array.Find(FindObjectsByType<Glass>(), glass => glass.id == id);
    }
}
#endif
