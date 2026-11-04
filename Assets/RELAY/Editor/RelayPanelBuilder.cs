using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Relay.EditorTools
{
    /// <summary>
    /// Builds the instruction panel from the Assignment 2 wireframes. Positions are in canvas pixels
    /// (1600 x 900) measured from the top-left of the parent, so they read like the wireframe.
    /// </summary>
    public static class RelayPanelBuilder
    {
        const float CanvasWidth = 1600f;
        const float CanvasHeight = 900f;
        const float WorldWidth = 0.8f; // about 35 degrees wide from where the person stands

        static readonly Color Background = Hex(0x12202B);
        static readonly Color Band = Hex(0x0C171F);
        static readonly Color Muted = Hex(0x9FB3C2);
        static readonly Color Chip = Hex(0x1F6F78);
        static readonly Color GhostBlue = Hex(0x7CC4FF);
        static readonly Color Amber = Hex(0xFFB020);
        static readonly Color Red = Hex(0xD92D34);
        static readonly Color Dark = Hex(0x12202B);

        public static InstructionPanel Build(TaskSource source, Vector3 position)
        {
            var root = new GameObject("InstructionPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            rootRect.localScale = Vector3.one * (WorldWidth / CanvasWidth);
            rootRect.position = position;

            var panel = root.AddComponent<InstructionPanel>();
            panel.source = source;
            Transform canvas = root.transform;

            Box("Background", canvas, 0, 0, CanvasWidth, CanvasHeight, Background);
            BuildCard(panel, canvas);
            BuildGhost(panel, canvas);
            BuildHeader(panel, canvas);
            BuildFooter(panel, canvas);
            BuildRollbackNote(panel, canvas);
            BuildSlipBanner(panel, canvas);
            BuildPausedOverlay(panel, canvas);
            BuildNotice(panel, canvas);
            BuildMessageOverlay(panel, canvas);
            return panel;
        }

        static void BuildHeader(InstructionPanel panel, Transform canvas)
        {
            RectTransform header = Box("Header", canvas, 0, 0, CanvasWidth, 120, Band);
            panel.titleText = Label("Title", header, 40, 20, 800, 80, "Bottle task", 48, Muted, TextAlignmentOptions.MidlineLeft);
            panel.counterText = Label("Counter", header, 1000, 14, 560, 90, "Step 1 of 5", 64, Color.white, TextAlignmentOptions.MidlineRight, true);

            RectTransform bar = Box("Progress", header, 0, 110, CanvasWidth, 10, Hex(0x2A3B48));
            RectTransform fill = Box("Fill", bar, 0, 0, 0, 0, Hex(0x2EC4B6));
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.2f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            panel.progressFill = fill;
        }

        static void BuildCard(InstructionPanel panel, Transform canvas)
        {
            RectTransform card = Box("StepCard", canvas, 0, 0, CanvasWidth, CanvasHeight, null);
            panel.card = card.gameObject.AddComponent<CanvasGroup>();

            panel.cardImage = Picture("Image", card, 40, 175, 560, 420);
            panel.cardText = Label("Text", card, 630, 170, 510, 310, "Step text", 72, Color.white, TextAlignmentOptions.TopLeft, true);

            RectTransform chip = Box("TargetChip", card, 630, 495, 510, 100, Chip);
            panel.cardTargetBackground = chip.GetComponent<Image>();
            panel.cardTarget = Label("Target", chip, 16, 8, 478, 84, "Take: bottle A", 46, Color.white, TextAlignmentOptions.Center, true);
            panel.cardTarget.enableAutoSizing = true;
            panel.cardTarget.fontSizeMin = 28f;
            panel.cardTarget.fontSizeMax = 46f;

            RectTransform pedal = Box("PedalChip", card, 40, 625, 1100, 90, Amber);
            panel.pedalChip = pedal.gameObject;
            panel.pedalChipText = Label("Text", pedal, 20, 8, 1060, 74, "Tap the pedal when done", 48, Dark, TextAlignmentOptions.Center, true);
        }

        static void BuildGhost(InstructionPanel panel, Transform canvas)
        {
            RectTransform ghost = Box("GhostCard", canvas, 1170, 150, 400, 620, null);
            panel.ghost = ghost.gameObject.AddComponent<CanvasGroup>();

            Border(ghost, 0, 0, 400, 620, 5, GhostBlue);
            RectTransform tag = Box("NextTag", ghost, 0, 0, 400, 64, GhostBlue);
            Label("Next", tag, 0, 0, 400, 64, "NEXT", 44, Dark, TextAlignmentOptions.Center, true);
            panel.ghostImage = Picture("Image", ghost, 20, 80, 360, 270);
            panel.ghostText = Label("Text", ghost, 20, 360, 360, 170, "Next step", 44, Color.white, TextAlignmentOptions.TopLeft, true);
            panel.ghostTarget = Label("Target", ghost, 20, 545, 360, 60, "Take: bottle A", 34, GhostBlue, TextAlignmentOptions.Center, true);
            panel.ghostTarget.enableAutoSizing = true;
            panel.ghostTarget.fontSizeMin = 22f;
            panel.ghostTarget.fontSizeMax = 34f;
        }

        static void BuildFooter(InstructionPanel panel, Transform canvas)
        {
            RectTransform footer = Box("Footer", canvas, 0, 790, CanvasWidth, 110, Band);
            panel.previousImage = Picture("PreviousImage", footer, 30, 12, 115, 86);
            panel.previousText = Label("Previous", footer, 160, 20, 380, 70, "", 36, Muted, TextAlignmentOptions.MidlineLeft);
            panel.gazeStatusText = Label("GazeStatus", footer, 560, 20, 340, 70, "Eyes: OK", 36, Muted, TextAlignmentOptions.Center);
            panel.fallbackText = Label("Fallback", footer, 910, 20, 660, 70, "", 34, Muted, TextAlignmentOptions.MidlineRight);
        }

        static void BuildRollbackNote(InstructionPanel panel, Transform canvas)
        {
            RectTransform note = Box("RollbackNote", canvas, 0, 0, CanvasWidth, CanvasHeight, null);
            panel.rollbackNote = note.gameObject;
            Border(note, 20, 135, 1130, 640, 10, Amber);
            RectTransform tag = Box("Tag", note, 40, 625, 460, 90, Amber);
            panel.rollbackText = Label("Text", tag, 10, 5, 440, 80, "Back to step 1", 46, Dark, TextAlignmentOptions.Center, true);
        }

        static void BuildSlipBanner(InstructionPanel panel, Transform canvas)
        {
            // Covers the header rather than the card, so the instruction stays readable.
            RectTransform banner = Box("SlipBanner", canvas, 0, 0, CanvasWidth, 122, Red);
            panel.slipBanner = banner.gameObject;
            panel.slipText = Label("Text", banner, 40, 6, 1520, 110, "Not this one.", 68, Color.white, TextAlignmentOptions.Center, true);
        }

        static void BuildPausedOverlay(InstructionPanel panel, Transform canvas)
        {
            RectTransform overlay = Box("PausedOverlay", canvas, 0, 0, CanvasWidth, 790, Background);
            panel.pausedOverlay = overlay.gameObject;
            RectTransform band = Box("Band", overlay, 0, 0, CanvasWidth, 120, Amber);
            panel.pausedTitle = Label("Title", band, 40, 10, 1520, 100, "PAUSED", 64, Dark, TextAlignmentOptions.Center, true);
            panel.pausedImage = Picture("Image", overlay, 60, 180, 560, 420);
            panel.pausedBody = Label("Body", overlay, 660, 180, 880, 420, "Look at the target to continue", 68, Color.white, TextAlignmentOptions.MidlineLeft, true);
        }

        static void BuildNotice(InstructionPanel panel, Transform canvas)
        {
            RectTransform notice = Box("TrackingNotice", canvas, 0, 0, CanvasWidth, 120, Amber);
            panel.noticeStrip = notice.gameObject;
            panel.noticeText = Label("Text", notice, 40, 10, 1520, 100, "Can't see your eyes.", 52, Dark, TextAlignmentOptions.Center, true);
        }

        static void BuildMessageOverlay(InstructionPanel panel, Transform canvas)
        {
            RectTransform overlay = Box("MessageOverlay", canvas, 0, 0, CanvasWidth, CanvasHeight, Background);
            panel.messageOverlay = overlay.gameObject;
            panel.messageTitle = Label("Title", overlay, 100, 270, 1400, 190, "Bottle task", 110, Color.white, TextAlignmentOptions.Center, true);
            panel.messageBody = Label("Body", overlay, 100, 480, 1400, 140, "", 60, Muted, TextAlignmentOptions.Center);
        }

        // Anchored to the parent's top-left corner. Pass no colour for an invisible group.
        static RectTransform Box(string name, Transform parent, float x, float y, float width, float height, Color? color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);

            if (color.HasValue)
            {
                var image = go.AddComponent<Image>();
                image.color = color.Value;
                image.raycastTarget = false;
            }

            return rect;
        }

        static TMP_Text Label(string name, Transform parent, float x, float y, float width, float height, string text, float size, Color color, TextAlignmentOptions alignment, bool bold = false)
        {
            RectTransform rect = Box(name, parent, x, y, width, height, null);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.raycastTarget = false;
            return label;
        }

        static RawImage Picture(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = Box(name, parent, x, y, width, height, null);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        static void Border(Transform parent, float x, float y, float width, float height, float thickness, Color color)
        {
            Box("Top", parent, x, y, width, thickness, color);
            Box("Bottom", parent, x, y + height - thickness, width, thickness, color);
            Box("Left", parent, x, y, thickness, height, color);
            Box("Right", parent, x + width - thickness, y, thickness, height, color);
        }

        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        }
    }
}
