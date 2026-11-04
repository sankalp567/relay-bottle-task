using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Relay
{
    /// <summary>
    /// Saves each trial's data. This is the scorer, so unlike a logic engine it may read TaskTruth: comparing
    /// what the panel showed with when each step was really finished is how an engine gets marked.
    /// Files: an events file and a per-frame samples file for each trial, and one row per trial in trials.csv.
    /// </summary>
    public class TrialRecorder : MonoBehaviour
    {
        const float SlowFrameSeconds = 1f / 72f; // a frame this slow is visible as judder on a 90 Hz headset

        static readonly string[] CountedEvents =
        {
            "ROLLBACK", "SLIP_WARNING", "SLIP_WRONG_BOTTLE", "SLIP_WRONG_GLASS", "SLIP_POUR_AT_WRONG_STEP",
            "SPILL", "DROP", "GLASS_OVERFLOW", "BACKUP", "TOUCH_BUTTON", "GAZE_LOST"
        };

        public TaskTruth truth;
        public TaskSource source;
        public InstructionPanel panel;
        public GazeObjectTracker gaze;

        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        readonly List<float> _advanceDelays = new List<float>();
        readonly List<float> _stepDoneTimes = new List<float>();
        StreamWriter _events;
        StreamWriter _samples;
        Transform _leftHand;
        Transform _rightHand;
        string _participant;
        string _condition;
        string _task;
        int _trial;
        float _startTime;
        int _lastShown;
        int _falseAdvances;
        int _frames;
        int _slowFrames;
        double _frameSeconds;

        public static string DataFolder => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "RELAY_Data"))
            : Path.Combine(Application.persistentDataPath, "RELAY_Data");

        public bool IsRecording => _events != null;
        public int FalseAdvances => _falseAdvances;

        public void Begin(string participant, string condition, string task, int trial)
        {
            Directory.CreateDirectory(DataFolder);
            _participant = participant;
            _condition = condition;
            _task = task;
            _trial = trial;
            _startTime = Time.time;
            _lastShown = 0;
            _falseAdvances = 0;
            _frames = 0;
            _slowFrames = 0;
            _frameSeconds = 0;
            _counts.Clear();
            _advanceDelays.Clear();
            _stepDoneTimes.Clear();

            foreach (NearFarInteractor hand in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include))
            {
                if (hand.handedness == InteractorHandedness.Left) _leftHand = hand.transform;
                else if (hand.handedness == InteractorHandedness.Right) _rightHand = hand.transform;
            }

            string stem = Path.Combine(DataFolder, $"{Safe(participant)}_{Safe(condition)}_t{trial:00}_{DateTime.Now:yyyyMMdd-HHmmss}");
            _events = new StreamWriter(stem + "_events.csv", false, Encoding.UTF8);
            _events.WriteLine("time_s,event,detail,true_step,shown_step");
            _samples = new StreamWriter(stem + "_samples.csv", false, Encoding.UTF8);
            _samples.WriteLine("time_s,head_x,head_y,head_z,gaze_valid,gaze_dx,gaze_dy,gaze_dz,looking_at,left_x,left_y,left_z,right_x,right_y,right_z,true_step,shown_step");

            RelayLog.Logged += OnLogged;
            truth.StepCompleted += OnTrueStepDone;
            panel.StepShown += OnStepShown;
        }

        public void End(string reason)
        {
            if (_events == null) return;

            RelayLog.Logged -= OnLogged;
            truth.StepCompleted -= OnTrueStepDone;
            panel.StepShown -= OnStepShown;

            WriteTrialRow(reason);
            _events.Dispose();
            _samples.Dispose();
            _events = null;
            _samples = null;
        }

        void OnDestroy() => End("scene_closed");

        void OnLogged(string code, string detail)
        {
            if (Array.IndexOf(CountedEvents, code) >= 0)
            {
                _counts.TryGetValue(code, out int count);
                _counts[code] = count + 1;
            }

            string escaped = detail.Replace("\"", "\"\"");
            _events.WriteLine(Invariant($"{Time.time - _startTime:F3},{code},\"{escaped}\",{truth.CurrentIndex + 1},{panel.ShownStep + 1}"));
        }

        void OnTrueStepDone(int index, TaskStepData step) => _stepDoneTimes.Add(Time.time);

        // Marks each forward move of the panel against the truth: too early, or how late.
        void OnStepShown(int index)
        {
            if (index <= _lastShown)
            {
                _lastShown = index;
                return;
            }

            _lastShown = index;
            int previousStep = index - 1;
            if (previousStep >= _stepDoneTimes.Count)
            {
                _falseAdvances++;
                RelayLog.Event("FALSE_ADVANCE", $"panel_step={index + 1} true_step={truth.CurrentIndex + 1}");
                return;
            }

            float delay = Time.time - _stepDoneTimes[previousStep];
            _advanceDelays.Add(delay);
            RelayLog.Event("ADVANCE_DELAY", Invariant($"panel_step={index + 1} seconds={delay:F3}"));
        }

        void LateUpdate()
        {
            if (_samples == null) return;

            _frames++;
            _frameSeconds += Time.unscaledDeltaTime;
            if (Time.unscaledDeltaTime > SlowFrameSeconds) _slowFrames++;

            Camera cam = Camera.main;
            Vector3 head = cam != null ? cam.transform.position : Vector3.zero;
            Vector3 direction = gaze != null && gaze.HasGaze ? gaze.LastRay.direction : Vector3.zero;
            Vector3 left = _leftHand != null ? _leftHand.position : Vector3.zero;
            Vector3 right = _rightHand != null ? _rightHand.position : Vector3.zero;
            int valid = gaze != null && gaze.HasGaze ? 1 : 0;
            string looking = gaze != null ? gaze.CurrentHit : "";

            _samples.WriteLine(Invariant(
                $"{Time.time - _startTime:F4},{head.x:F4},{head.y:F4},{head.z:F4},{valid},{direction.x:F4},{direction.y:F4},{direction.z:F4},{looking},{left.x:F4},{left.y:F4},{left.z:F4},{right.x:F4},{right.y:F4},{right.z:F4},{truth.CurrentIndex + 1},{panel.ShownStep + 1}"));
        }

        void WriteTrialRow(string reason)
        {
            string path = Path.Combine(DataFolder, "trials.csv");
            bool isNew = !File.Exists(path);
            float duration = Time.time - _startTime;
            float meanDelay = _advanceDelays.Count > 0 ? _advanceDelays.Average() : 0f;
            float meanFps = _frameSeconds > 0 ? (float)(_frames / _frameSeconds) : 0f;

            using (var trials = new StreamWriter(path, true, Encoding.UTF8))
            {
                if (isNew)
                {
                    trials.WriteLine("participant,condition,task,trial,date_time,end_reason,duration_s,true_steps_done,total_steps,false_advances,mean_advance_delay_s,mean_pour_error," +
                                     string.Join(",", CountedEvents.Select(e => e.ToLowerInvariant())) + ",mean_fps,slow_frames");
                }

                string counted = string.Join(",", CountedEvents.Select(e => _counts.TryGetValue(e, out int n) ? n : 0));
                trials.WriteLine(Invariant(
                    $"{_participant},{_condition},{_task},{_trial},{DateTime.Now:yyyy-MM-dd HH:mm:ss},{reason},{duration:F2},{truth.CurrentIndex},{source.Data.steps.Length},{_falseAdvances},{meanDelay:F3},{MeanPourError():F3},{counted},{meanFps:F1},{_slowFrames}"));
            }
        }

        // How far each poured glass ended from its line, as a fraction of the glass.
        float MeanPourError()
        {
            float total = 0f;
            int count = 0;
            Glass[] glasses = FindObjectsByType<Glass>();
            foreach (TaskStepData step in source.Data.steps)
            {
                if (step.kind != "Pour") continue;
                Glass glass = Array.Find(glasses, g => g.id == step.glass);
                if (glass == null) continue;
                total += Mathf.Abs(glass.Fill01 - step.pourFraction);
                count++;
            }

            return count > 0 ? total / count : 0f;
        }

        static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

        static string Safe(string text)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) text = text.Replace(c, '_');
            return text.Replace(' ', '_');
        }
    }
}
