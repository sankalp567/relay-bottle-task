using System;
using System.IO;
using UnityEngine;

namespace Relay
{
    [Serializable]
    public class TaskStepData
    {
        public string kind; // PickBottle, OpenCap, Pour, CloseCap or PutBack
        public string text;
        public string bottle;
        public string glass;
        public float pourFraction;
        public bool pedalConfirm; // gaze can't be trusted on this step, so it shows "tap the pedal"
    }

    [Serializable]
    public class TaskData
    {
        public string title;
        public TaskStepData[] steps;
    }

    /// <summary>
    /// Loads the step list. This is content only: it says nothing about how far the person has got,
    /// so the panel can read it without seeing the true step.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TaskSource : MonoBehaviour
    {
        [Tooltip("File in Assets/StreamingAssets/RELAY")]
        public string taskFile = "task_one_round.json";

        public TaskData Data { get; private set; }

        void Awake()
        {
            if (!string.IsNullOrEmpty(RelaySession.RequestedTaskFile)) taskFile = RelaySession.RequestedTaskFile;

            string path = Path.Combine(Application.streamingAssetsPath, "RELAY", taskFile);
            if (!File.Exists(path))
            {
                Debug.LogError($"[RELAY] Task file not found: {path}");
                return;
            }

            TaskData data = JsonUtility.FromJson<TaskData>(File.ReadAllText(path));
            if (data == null || data.steps == null || data.steps.Length == 0)
            {
                Debug.LogError($"[RELAY] Task file has no steps: {path}");
                return;
            }

            Data = data;
            RelayLog.Event("TASK_LOADED", $"file={taskFile} steps={data.steps.Length}");
        }
    }

    /// <summary>What each step is about. Target ids match AoiTarget, ObjectHighlight and the step images.</summary>
    public static class StepContent
    {
        public static string TargetId(TaskStepData step)
        {
            switch (step.kind)
            {
                case "PickBottle": return $"bottle_{step.bottle}";
                case "OpenCap":
                case "CloseCap": return $"cap_{step.bottle}";
                case "Pour": return $"glass_{step.glass}";
                case "PutBack": return $"spot_{step.bottle}";
                default: return null;
            }
        }

        /// <summary>The line on the target chip, such as "Take: bottle A".</summary>
        public static string TargetLine(TaskStepData step)
        {
            switch (step.kind)
            {
                case "PickBottle": return $"Take: bottle {step.bottle}";
                case "OpenCap":
                case "CloseCap": return $"Twist: cap of bottle {step.bottle}";
                case "Pour": return $"Pour into: glass {step.glass}";
                case "PutBack": return $"Place on: spot {step.bottle}";
                default: return "";
            }
        }

        /// <summary>"bottle_A" becomes "bottle A".</summary>
        public static string Name(string targetId) => targetId == null ? "" : targetId.Replace('_', ' ');

        /// <summary>"bottle_A" becomes "bottle".</summary>
        public static string Kind(string targetId)
        {
            if (targetId == null) return "";
            int split = targetId.IndexOf('_');
            return split < 0 ? targetId : targetId.Substring(0, split);
        }
    }
}
