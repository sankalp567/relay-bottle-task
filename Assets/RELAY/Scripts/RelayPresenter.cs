using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Relay
{
    /// <summary>
    /// Everything the person sees, hears and feels, one method per moment in the RELAY flow. RELAY's
    /// advance logic will call these. Nothing here reads TaskTruth, so the true step can't leak out.
    /// </summary>
    public class RelayPresenter : MonoBehaviour
    {
        static readonly Color Blue = new Color(0.25f, 0.6f, 1f);
        static readonly Color Green = new Color(0.1f, 0.85f, 0.35f);
        static readonly Color Red = new Color(1f, 0.1f, 0.1f);
        static readonly Color Amber = new Color(1f, 0.65f, 0.05f);

        public InstructionPanel panel;
        public TaskSource source;
        [Tooltip("Error tones made some workers anxious in earlier studies, so sound can be switched off.")]
        public bool muted;
        [Range(0f, 1f)] public float volume = 0.5f;
        [Range(0f, 1f)] public float vibrationStrength = 0.5f;

        readonly Dictionary<string, ObjectHighlight> _highlights = new Dictionary<string, ObjectHighlight>();
        AudioSource _audio;
        AudioClip _tick;
        AudioClip _slipTone;
        AudioClip _wrongTone;
        AudioClip _doubleBeep;
        AudioClip _completeTone;
        NearFarInteractor[] _hands;
        string _armedId;
        string _slipWrongId;
        string _slipRightId;
        string _pausedId;
        float _startTime;
        int _warnings;

        public event Action TaskFinished;

        public int WarningCount => _warnings;
        TaskStepData[] Steps => source.Data.steps;

        void Awake()
        {
            foreach (ObjectHighlight highlight in FindObjectsByType<ObjectHighlight>()) _highlights[highlight.id] = highlight;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _tick = MakeTone("Tick", (660f, 0.06f));
            _slipTone = MakeTone("Slip", (440f, 0.1f), (660f, 0.1f));
            _wrongTone = MakeTone("Wrong", (300f, 0.4f));
            _doubleBeep = MakeTone("Beep", (440f, 0.08f), (0f, 0.06f), (440f, 0.08f));
            _completeTone = MakeTone("Complete", (523f, 0.12f), (659f, 0.12f), (784f, 0.18f));
        }

        void Start() => _hands = FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include);

        public void TaskStarted()
        {
            _startTime = Time.time;
            _warnings = 0;
            panel.ShowStep(0);
        }

        /// <summary>The next step's ghost appears, and its object glows softly. Nothing is committed.</summary>
        public void Armed(int nextIndex)
        {
            panel.ShowGhost(nextIndex);
            HighlightOff(ref _armedId);
            _armedId = StepContent.TargetId(Steps[nextIndex]);
            Highlight(_armedId, Blue, ObjectHighlight.Mode.Solid);
        }

        public void Disarmed()
        {
            panel.HideGhost();
            HighlightOff(ref _armedId);
        }

        public void Committed(int index)
        {
            HighlightOff(ref _armedId);
            panel.Commit(index);
            Highlight(StepContent.TargetId(Steps[index]), Green, ObjectHighlight.Mode.FlashOnce);
            Play(_tick);
            Vibrate(1);
        }

        /// <summary>Two pulses, so an undo feels different from an advance without looking.</summary>
        public void RolledBack(int index)
        {
            HighlightOff(ref _armedId);
            RelayLog.Event("ROLLBACK", $"to_step={index + 1}");
            panel.ShowRollback(index);
            Vibrate(2);
        }

        /// <summary>The eyes have settled on the wrong object: warn before the hand gets there.</summary>
        public void SlipLooking(int currentIndex, string wrongId)
        {
            string right = StepContent.TargetId(Steps[currentIndex]);
            string verb = StepContent.Kind(right) == "bottle" ? "Take" : "Use";
            ShowSlip($"Not {StepContent.Name(wrongId)}. {verb} {StepContent.Name(right)}.", wrongId, right, ObjectHighlight.Mode.Pulse);
            Play(_slipTone);
            Vibrate(3);
        }

        /// <summary>The hand got there anyway.</summary>
        public void SlipGrabbed(int currentIndex, string wrongId)
        {
            string right = StepContent.TargetId(Steps[currentIndex]);
            string message = $"Wrong {StepContent.Kind(wrongId)}. Put it back, take {StepContent.Name(right)}.";
            ShowSlip(message, wrongId, right, ObjectHighlight.Mode.Solid);
            Play(_wrongTone);
            Vibrate(3);
        }

        /// <summary>Clears quietly: the person has already corrected themselves.</summary>
        public void SlipCleared()
        {
            panel.ClearSlip();
            HighlightOff(ref _slipWrongId);
            HighlightOff(ref _slipRightId);
        }

        public void Paused(int index)
        {
            panel.ShowPaused(index);
            HighlightOff(ref _pausedId);
            _pausedId = StepContent.TargetId(Steps[index]);
            Highlight(_pausedId, Amber, ObjectHighlight.Mode.Pulse);
        }

        public void Resumed()
        {
            panel.ClearPaused();
            HighlightOff(ref _pausedId);
        }

        public void TrackingChanged(bool lost)
        {
            panel.SetTrackingLost(lost);
            if (lost) Play(_doubleBeep);
        }

        public void Completed()
        {
            HighlightOff(ref _armedId);
            SlipCleared();
            Resumed();
            panel.ShowComplete(Time.time - _startTime, _warnings);
            foreach (ObjectHighlight highlight in _highlights.Values) highlight.Show(Green, ObjectHighlight.Mode.FlashOnce);
            Play(_completeTone);
            TaskFinished?.Invoke();
        }

        /// <summary>Another object of the same kind as this target, for testing slips.</summary>
        public string FindLookAlike(string targetId)
        {
            foreach (string id in _highlights.Keys)
            {
                if (id != targetId && StepContent.Kind(id) == StepContent.Kind(targetId)) return id;
            }

            return null;
        }

        void ShowSlip(string message, string wrongId, string rightId, ObjectHighlight.Mode wrongMode)
        {
            _warnings++;
            RelayLog.Event("SLIP_WARNING", $"wrong={wrongId} right={rightId}");
            HighlightOff(ref _slipWrongId);
            HighlightOff(ref _slipRightId);
            _slipWrongId = wrongId;
            _slipRightId = rightId;
            panel.ShowSlip(message);
            Highlight(wrongId, Red, wrongMode);
            Highlight(rightId, Green, ObjectHighlight.Mode.Pulse);
        }

        void Highlight(string id, Color color, ObjectHighlight.Mode mode)
        {
            if (id != null && _highlights.TryGetValue(id, out ObjectHighlight highlight)) highlight.Show(color, mode);
        }

        void HighlightOff(ref string id)
        {
            if (id != null && _highlights.TryGetValue(id, out ObjectHighlight highlight)) highlight.Hide();
            id = null;
        }

        void Play(AudioClip clip)
        {
            if (!muted) _audio.PlayOneShot(clip, volume);
        }

        void Vibrate(int pulses) => StartCoroutine(VibrateRoutine(pulses));

        IEnumerator VibrateRoutine(int pulses)
        {
            for (int i = 0; i < pulses; i++)
            {
                foreach (NearFarInteractor hand in _hands) hand.SendHapticImpulse(vibrationStrength, 0.04f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        // Tones are generated in code, so the project needs no sound files. A frequency of 0 is silence.
        static AudioClip MakeTone(string clipName, params (float frequency, float seconds)[] notes)
        {
            const int SampleRate = 44100;
            const float FadeSeconds = 0.005f; // avoids clicks

            var samples = new List<float>();
            foreach ((float frequency, float seconds) in notes)
            {
                int count = Mathf.RoundToInt(seconds * SampleRate);
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / SampleRate;
                    float fade = Mathf.Min(1f, Mathf.Min(t, seconds - t) / FadeSeconds);
                    samples.Add(frequency > 0f ? 0.5f * fade * Mathf.Sin(2f * Mathf.PI * frequency * t) : 0f);
                }
            }

            AudioClip clip = AudioClip.Create(clipName, samples.Count, 1, SampleRate, false);
            clip.SetData(samples.ToArray(), 0);
            return clip;
        }
    }
}
