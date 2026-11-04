using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Relay
{
    /// <summary>
    /// Works out from the scene when each step is really finished, and logs mistakes.
    /// This is the true answer used to score RELAY. RELAY's gaze logic must never read it, or the
    /// interface becomes auto-advance that only works because VR knows everything.
    /// </summary>
    public class TaskTruth : MonoBehaviour
    {
        public TaskSource source;
        public float pourTolerance = 0.1f;
        public float pourSettleSeconds = 0.5f;
        [Tooltip("Build-time helper showing the true step. Turn it off for participants.")]
        public TextMesh debugLabel;

        public event Action<int, TaskStepData> StepCompleted;
        public int CurrentIndex { get; private set; }
        public bool IsFinished => _steps != null && CurrentIndex >= _steps.Length;

        readonly Dictionary<string, Bottle> _bottles = new Dictionary<string, Bottle>();
        readonly Dictionary<string, Glass> _glasses = new Dictionary<string, Glass>();
        TaskStepData[] _steps;

        TaskStepData Current => IsFinished ? null : _steps[CurrentIndex];

        void Start()
        {
            foreach (Bottle b in FindObjectsByType<Bottle>()) _bottles[b.id] = b;
            foreach (Glass g in FindObjectsByType<Glass>()) _glasses[g.id] = g;

            if (!ValidateTask())
            {
                enabled = false;
                return;
            }

            foreach (Bottle b in _bottles.Values)
            {
                Bottle bottle = b;
                bottle.Grab.selectEntered.AddListener(_ => OnBottleGrabbed(bottle));
                BottlePour pour = bottle.GetComponent<BottlePour>();
                pour.PouredInto += glass => OnPouredInto(bottle, glass);
                pour.Spilled += () => RelayLog.Event("SPILL", $"bottle={bottle.id} step={CurrentIndex + 1}");
            }

            ShowCurrentStep();
        }

        bool ValidateTask()
        {
            if (source == null || source.Data == null) return false;

            foreach (TaskStepData step in source.Data.steps)
            {
                if (!_bottles.ContainsKey(step.bottle))
                {
                    Debug.LogError($"[RELAY] Step '{step.text}' uses unknown bottle '{step.bottle}'");
                    return false;
                }

                if (step.kind == "Pour" && !_glasses.ContainsKey(step.glass))
                {
                    Debug.LogError($"[RELAY] Step '{step.text}' uses unknown glass '{step.glass}'");
                    return false;
                }
            }

            _steps = source.Data.steps;

            // Set once at load: nothing in the world may change when the true step changes, or
            // participants would be following the truth rather than the interface.
            foreach (TaskStepData step in _steps)
            {
                if (step.kind == "Pour") _glasses[step.glass].SetFillLine(step.pourFraction);
            }

            return true;
        }

        void Update()
        {
            if (IsFinished || !IsStepDone(Current)) return;

            RelayLog.Event("TRUE_STEP_DONE", $"step={CurrentIndex + 1}/{_steps.Length} kind={Current.kind} text=\"{Current.text}\"");
            StepCompleted?.Invoke(CurrentIndex, Current);
            CurrentIndex++;
            ShowCurrentStep();
        }

        bool IsStepDone(TaskStepData step)
        {
            Bottle bottle = _bottles[step.bottle];
            switch (step.kind)
            {
                case "PickBottle": return bottle.IsHeld;
                case "OpenCap": return bottle.cap.IsOff;
                case "CloseCap": return bottle.cap.IsClosed;
                case "PutBack": return bottle.IsAtHome();
                case "Pour":
                    BottlePour pour = bottle.GetComponent<BottlePour>();
                    return !pour.IsFlowing
                           && pour.SecondsSinceFlow >= pourSettleSeconds
                           && _glasses[step.glass].Fill01 >= step.pourFraction - pourTolerance;
                default:
                    Debug.LogError($"[RELAY] Unknown step kind '{step.kind}'");
                    enabled = false;
                    return false;
            }
        }

        void ShowCurrentStep()
        {
            TaskStepData step = Current;
            if (step == null)
            {
                RelayLog.Event("TASK_FINISHED");
                if (debugLabel != null) debugLabel.text = "Task finished";
                return;
            }

            if (debugLabel != null) debugLabel.text = $"Step {CurrentIndex + 1}/{_steps.Length}: {step.text}";
        }

        void OnBottleGrabbed(Bottle bottle)
        {
            if (Current != null && bottle.id != Current.bottle)
            {
                RelayLog.Event("SLIP_WRONG_BOTTLE", $"got={bottle.id} want={Current.bottle} step={CurrentIndex + 1}");
            }
        }

        void OnPouredInto(Bottle bottle, Glass glass)
        {
            if (Current == null) return;
            if (Current.kind != "Pour")
            {
                RelayLog.Event("SLIP_POUR_AT_WRONG_STEP", $"glass={glass.id} step={CurrentIndex + 1}");
            }
            else if (glass.id != Current.glass)
            {
                RelayLog.Event("SLIP_WRONG_GLASS", $"got={glass.id} want={Current.glass} step={CurrentIndex + 1}");
            }
        }
    }
}
