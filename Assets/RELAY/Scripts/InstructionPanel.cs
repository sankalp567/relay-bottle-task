using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Relay
{
    /// <summary>
    /// The instruction display. It only shows what it's told; RelayPresenter decides when. The ghost
    /// has a reserved space, so its appearance never moves the card the person is reading.
    /// </summary>
    public class InstructionPanel : MonoBehaviour
    {
        const float GhostAlpha = 0.4f;
        const float GhostFadeSeconds = 0.3f; // a slow onset avoids grabbing attention
        const float CardFadeSeconds = 0.25f;
        const float DimmedCardAlpha = 0.4f;
        const float RollbackNoteSeconds = 1.5f;

        public TaskSource source;
        [Tooltip("The backup action named on screen. Change this if your fallback isn't a pedal.")]
        public string fallbackAction = "Tap the pedal";

        [Header("Header")]
        public TMP_Text titleText;
        public TMP_Text counterText;
        public RectTransform progressFill;

        [Header("Step card")]
        public CanvasGroup card;
        public RawImage cardImage;
        public TMP_Text cardText;
        public TMP_Text cardTarget;
        public Image cardTargetBackground;
        public GameObject pedalChip;
        public TMP_Text pedalChipText;

        [Header("Ghost of the next step")]
        public CanvasGroup ghost;
        public RawImage ghostImage;
        public TMP_Text ghostText;
        public TMP_Text ghostTarget;

        [Header("Footer")]
        public RawImage previousImage;
        public TMP_Text previousText;
        public TMP_Text gazeStatusText;
        public TMP_Text fallbackText;

        [Header("Overlays")]
        public GameObject slipBanner;
        public TMP_Text slipText;
        public GameObject noticeStrip;
        public TMP_Text noticeText;
        public GameObject rollbackNote;
        public TMP_Text rollbackText;
        public GameObject pausedOverlay;
        public RawImage pausedImage;
        public TMP_Text pausedTitle;
        public TMP_Text pausedBody;
        public GameObject messageOverlay;
        public TMP_Text messageTitle;
        public TMP_Text messageBody;

        readonly Dictionary<string, Texture2D> _images = new Dictionary<string, Texture2D>();
        float _ghostAlphaGoal;
        float _cardAlphaGoal = 1f;
        float _rollbackHideTime;
        Color _chipColor;

        public event Action<int> StepShown;

        public int ShownStep { get; private set; } = -1;
        public int GhostStep { get; private set; } = -1;
        public bool IsSlipShown => slipBanner.activeSelf;
        public bool IsPausedShown => pausedOverlay.activeSelf;
        public bool IsTrackingNoticeShown => noticeStrip.activeSelf;
        public bool IsRollbackNoteShown => rollbackNote.activeSelf;
        public bool IsMessageShown => messageOverlay.activeSelf;

        TaskStepData[] Steps => source.Data.steps;

        void Awake()
        {
            if (source == null || source.Data == null)
            {
                enabled = false;
                return;
            }

            _chipColor = cardTargetBackground.color;
            titleText.text = source.Data.title;
            fallbackText.text = $"{fallbackAction}: next. Twice: back.";
            pedalChipText.text = $"{fallbackAction} when done";
            gazeStatusText.text = "Eyes: OK";

            ghost.alpha = 0f;
            slipBanner.SetActive(false);
            noticeStrip.SetActive(false);
            rollbackNote.SetActive(false);
            pausedOverlay.SetActive(false);
            ShowStart();
        }

        void Update()
        {
            ghost.alpha = Mathf.MoveTowards(ghost.alpha, _ghostAlphaGoal, GhostAlpha / GhostFadeSeconds * Time.deltaTime);
            card.alpha = Mathf.MoveTowards(card.alpha, _cardAlphaGoal, Time.deltaTime / CardFadeSeconds);

            if (rollbackNote.activeSelf && Time.time >= _rollbackHideTime) rollbackNote.SetActive(false);

            // During a slip, the chip naming the right object pulses green.
            cardTargetBackground.color = IsSlipShown
                ? Color.Lerp(_chipColor, new Color(0.19f, 0.64f, 0.42f), 0.5f + 0.5f * Mathf.Sin(Time.time * 9f))
                : _chipColor;
        }

        public void ShowStart()
        {
            ShownStep = -1;
            HideGhost();
            messageOverlay.SetActive(true);
            messageTitle.text = source.Data.title;
            messageBody.text = "Get ready";
            RelayLog.Event("PANEL", "start");
        }

        public void ShowStep(int index)
        {
            if (!IsValid(index)) return;

            messageOverlay.SetActive(false);
            TaskStepData step = Steps[index];
            ShownStep = index;

            counterText.text = $"Step {index + 1} of {Steps.Length}";
            progressFill.anchorMax = new Vector2((index + 1f) / Steps.Length, 1f);

            cardText.text = KeepNamesTogether(step.text);
            cardTarget.text = StepContent.TargetLine(step);
            SetImage(cardImage, StepContent.TargetId(step));
            pedalChip.SetActive(step.pedalConfirm);

            bool hasPrevious = index > 0;
            previousImage.enabled = hasPrevious;
            previousText.text = hasPrevious ? $"Done: step {index}" : "";
            if (hasPrevious) SetImage(previousImage, StepContent.TargetId(Steps[index - 1]));

            HideGhost();
            RelayLog.Event("PANEL", $"step={index + 1}");
            StepShown?.Invoke(index);
        }

        public void ShowGhost(int index)
        {
            if (!IsValid(index) || GhostStep == index) return;

            GhostStep = index;
            TaskStepData step = Steps[index];
            ghostText.text = KeepNamesTogether(step.text);
            ghostTarget.text = StepContent.TargetLine(step);
            SetImage(ghostImage, StepContent.TargetId(step));
            _ghostAlphaGoal = GhostAlpha;
            RelayLog.Event("PANEL", $"ghost={index + 1}");
        }

        public void HideGhost()
        {
            if (GhostStep >= 0) RelayLog.Event("PANEL", "ghost_hidden");
            GhostStep = -1;
            _ghostAlphaGoal = 0f;
        }

        /// <summary>The ghost becomes the full card, and the old card becomes the "done" thumbnail.</summary>
        public void Commit(int index)
        {
            ShowStep(index);
            ghost.alpha = 0f;
            card.alpha = 0f;
        }

        public void ShowRollback(int index)
        {
            ShowStep(index);
            rollbackNote.SetActive(true);
            rollbackText.text = $"Back to step {index + 1}";
            _rollbackHideTime = Time.time + RollbackNoteSeconds;
        }

        public void ShowSlip(string message)
        {
            slipBanner.SetActive(true);
            slipText.text = message;
            _cardAlphaGoal = DimmedCardAlpha;
            RelayLog.Event("PANEL", $"slip \"{message}\"");
        }

        public void ClearSlip()
        {
            if (slipBanner.activeSelf) RelayLog.Event("PANEL", "slip_cleared");
            slipBanner.SetActive(false);
            _cardAlphaGoal = 1f;
        }

        public void ShowPaused(int index)
        {
            if (!IsValid(index)) return;

            string targetId = StepContent.TargetId(Steps[index]);
            pausedOverlay.SetActive(true);
            pausedTitle.text = $"PAUSED AT STEP {index + 1}";
            pausedBody.text = $"Look at {StepContent.Name(targetId)} to continue";
            if (index > 0) pausedBody.text += $"\n<size=70%>Last done: step {index}</size>";
            SetImage(pausedImage, targetId);
            RelayLog.Event("PANEL", $"paused step={index + 1}");
        }

        public void ClearPaused()
        {
            if (pausedOverlay.activeSelf) RelayLog.Event("PANEL", "resumed");
            pausedOverlay.SetActive(false);
        }

        /// <summary>The card stays visible: the person never loses the instruction.</summary>
        public void SetTrackingLost(bool lost)
        {
            noticeStrip.SetActive(lost);
            noticeText.text = $"Can't see your eyes. {fallbackAction} to advance.";
            gazeStatusText.text = lost ? "Eyes: not found" : "Eyes: OK";
            RelayLog.Event("PANEL", lost ? "tracking_lost" : "tracking_back");
        }

        public void ShowComplete(float seconds, int warnings)
        {
            HideGhost();
            ClearSlip();
            ClearPaused();
            messageOverlay.SetActive(true);
            messageTitle.text = $"All {Steps.Length} steps done";
            string warningWord = warnings == 1 ? "warning" : "warnings";
            messageBody.text = $"{(int)seconds / 60}:{(int)seconds % 60:00}  |  {warnings} {warningWord}";
            RelayLog.Event("PANEL", "complete");
        }

        // Stops "A" wrapping onto a line by itself after "bottle".
        static string KeepNamesTogether(string text)
        {
            return text.Replace("bottle ", "bottle ").Replace("glass ", "glass ");
        }

        bool IsValid(int index)
        {
            if (index >= 0 && index < Steps.Length) return true;
            Debug.LogError($"[RELAY] The panel was asked to show step index {index}, but there are {Steps.Length} steps.");
            return false;
        }

        void SetImage(RawImage target, string id)
        {
            if (!_images.TryGetValue(id, out Texture2D image))
            {
                image = Resources.Load<Texture2D>($"StepImages/{id}");
                if (image == null) Debug.LogWarning($"[RELAY] No step image for '{id}'. Run RELAY > Build Bottle Scene.");
                _images[id] = image;
            }

            target.texture = image;
            target.enabled = image != null;
        }
    }
}
