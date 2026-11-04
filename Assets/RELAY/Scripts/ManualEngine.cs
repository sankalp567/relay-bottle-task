using UnityEngine;

namespace Relay
{
    /// <summary>
    /// The experimenter moves the panel by hand, as a Wizard of Oz condition and for checking every panel
    /// state. The backup key works too. Also a simple working example of an engine.
    /// </summary>
    public class ManualEngine : RelayEngine
    {
        const float Width = 250f;

        public override string ConditionName => "Manual";

        string Wrong => Presenter.FindLookAlike(StepContent.TargetId(Steps[Current]));

        void OnGUI()
        {
            if (!IsRunning) return;

            var area = new Rect(Screen.width - Width - 10f, 10f, Width, 410f);
            RelayUi.Register("manual", area);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"Manual control (step {Current + 1} of {Steps.Length})");
            if (GUILayout.Button("Show ghost of next step")) PreviewNext();
            if (GUILayout.Button("Cancel ghost")) CancelPreview();
            if (GUILayout.Button("Advance")) AdvanceStep();
            if (GUILayout.Button("Undo")) UndoStep();
            if (GUILayout.Button("Slip: looking at wrong object")) Presenter.SlipLooking(Current, Wrong);
            if (GUILayout.Button("Slip: grabbed wrong object")) Presenter.SlipGrabbed(Current, Wrong);
            if (GUILayout.Button("Clear slip")) Presenter.SlipCleared();
            if (GUILayout.Button("Pause")) Presenter.Paused(Current);
            if (GUILayout.Button("Resume")) Presenter.Resumed();
            if (GUILayout.Button("Eye tracking lost")) Presenter.TrackingChanged(true);
            if (GUILayout.Button("Eye tracking back")) Presenter.TrackingChanged(false);
            GUILayout.EndArea();
        }
    }
}
