using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Relay
{
    /// <summary>
    /// The experimenter's panel on the desktop window, which the person in the headset can't see. It sets the
    /// participant, condition and task, runs each trial, and resets the scene between trials.
    /// </summary>
    public class RelaySession : MonoBehaviour
    {
        const float Width = 300f;

        // Kept across scene reloads, so they survive moving to the next trial.
        static string _participant = "P01";
        static int _conditionIndex;
        static int _trial = 1;
        static string _taskFile;

        public static string RequestedTaskFile => _taskFile;

        public TaskSource source;
        public TaskTruth truth;
        public RelayPresenter presenter;
        public RelaySignals signals;
        public TrialRecorder recorder;

        RelayEngine[] _engines;
        string[] _conditionNames;
        string[] _taskFiles;
        RelayEngine _engine;

        public bool IsTrialRunning { get; private set; }
        public int Trial => _trial;
        public string Participant { get => _participant; set => _participant = value; }
        public string ConditionName => _conditionNames[_conditionIndex];
        public bool TrialHasEnded { get; private set; }

        void Awake()
        {
            _engines = GetComponents<RelayEngine>();
            _conditionNames = new string[_engines.Length];
            for (int i = 0; i < _engines.Length; i++) _conditionNames[i] = _engines[i].ConditionName;
            _conditionIndex = Mathf.Clamp(_conditionIndex, 0, Mathf.Max(0, _engines.Length - 1));

            string folder = Path.Combine(Application.streamingAssetsPath, "RELAY");
            var names = new List<string>();
            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, "*.json")) names.Add(Path.GetFileName(file));
            }

            names.Sort();
            _taskFiles = names.ToArray();
        }

        void OnEnable() => presenter.TaskFinished += OnTaskFinished;
        void OnDisable() => presenter.TaskFinished -= OnTaskFinished;

        void OnTaskFinished() => EndTrial("finished");

        public void SelectCondition(string conditionName)
        {
            int index = System.Array.IndexOf(_conditionNames, conditionName);
            if (index >= 0) _conditionIndex = index;
        }

        public void StartTrial()
        {
            if (IsTrialRunning || TrialHasEnded || source.Data == null || _engines.Length == 0) return;

            _engine = _engines[_conditionIndex];
            IsTrialRunning = true;
            recorder.Begin(_participant, ConditionName, source.taskFile, _trial);
            RelayLog.Event("TRIAL_START", $"participant={_participant} condition={ConditionName} task={source.taskFile} trial={_trial}");
            _engine.BeginTrial(presenter, signals);
        }

        public void EndTrial(string reason)
        {
            if (!IsTrialRunning) return;

            IsTrialRunning = false;
            TrialHasEnded = true;
            _engine.EndTrial();
            RelayLog.Event("TRIAL_END", $"reason={reason}");
            recorder.End(reason);
        }

        /// <summary>Puts every object back and counts up to the next trial.</summary>
        public void NextTrial()
        {
            if (IsTrialRunning) EndTrial("stopped");
            if (TrialHasEnded) _trial++;
            ReloadScene();
        }

        static void ReloadScene()
        {
#if UNITY_EDITOR
            // Works in the Editor even if the scene isn't listed in the build settings.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
        }

        void OnGUI()
        {
            var area = new Rect(10f, 10f, Width, 340f);
            RelayUi.Register("session", area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("<b>Session</b>", RelayUi.RichLabel);

            GUI.enabled = !IsTrialRunning && !TrialHasEnded;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Participant", GUILayout.Width(80f));
            _participant = GUILayout.TextField(_participant);
            GUILayout.EndHorizontal();

            GUILayout.Label("Condition");
            if (_conditionNames.Length > 0) _conditionIndex = GUILayout.Toolbar(_conditionIndex, _conditionNames);

            GUILayout.Label("Task");
            int taskIndex = System.Array.IndexOf(_taskFiles, source.taskFile);
            int chosen = GUILayout.SelectionGrid(taskIndex, _taskFiles, 1);
            if (chosen != taskIndex && chosen >= 0)
            {
                _taskFile = _taskFiles[chosen];
                ReloadScene();
            }

            GUI.enabled = true;
            GUILayout.Space(4f);
            GUILayout.Label($"Trial {_trial}: {Status()}");

            GUI.enabled = !IsTrialRunning && !TrialHasEnded;
            if (GUILayout.Button("Start trial")) StartTrial();
            GUI.enabled = IsTrialRunning;
            if (GUILayout.Button("End trial")) EndTrial("stopped");
            GUI.enabled = true;
            if (GUILayout.Button("Next trial (resets the bench)")) NextTrial();

            if (GUI.enabled && (IsTrialRunning || TrialHasEnded))
            {
                GUILayout.Label($"True step: {Mathf.Min(truth.CurrentIndex + 1, source.Data.steps.Length)} of {source.Data.steps.Length}");
            }

            GUILayout.EndArea();
        }

        string Status()
        {
            if (IsTrialRunning) return "running";
            return TrialHasEnded ? "ended, data saved" : "ready";
        }
    }
}
