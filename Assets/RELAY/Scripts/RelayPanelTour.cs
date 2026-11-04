#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Relay
{
    /// <summary>
    /// Shows every panel state in the real scene, checks it, and saves a picture of each to Previews,
    /// for the report. Run it from RELAY > Save Panel Images.
    /// </summary>
    public class RelayPanelTour : MonoBehaviour
    {
        const string RunFlag = "RelayRunPanelTour";
        const float Settle = 0.45f; // long enough for every fade to finish

        InstructionPanel _panel;
        RelayPresenter _presenter;
        string _folder;
        int _passed;
        int _failed;

        public static void RequestRun() => SessionState.SetBool(RunFlag, true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            if (!SessionState.GetBool(RunFlag, false)) return;
            SessionState.SetBool(RunFlag, false);
            new GameObject("RelayPanelTour").AddComponent<RelayPanelTour>();
        }

        /// <summary>Saves what a camera at this position sees.</summary>
        public static void Render(string path, Vector3 position, Vector3 lookAt, float fieldOfView, int width, int height)
        {
            var go = new GameObject("PreviewCamera");
            var cam = go.AddComponent<Camera>();
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(lookAt - position);
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = 0.01f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.82f, 0.86f, 0.9f);

            var texture = new RenderTexture(width, height, 24);
            cam.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            cam.Render(); // the first frame of a session renders before lighting is ready
            cam.Render();

            RenderTexture.active = texture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());

            RenderTexture.active = null;
            cam.targetTexture = null;
            DestroyImmediate(image);
            DestroyImmediate(texture);
            DestroyImmediate(go);
        }

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            _panel = FindAnyObjectByType<InstructionPanel>();
            _presenter = FindAnyObjectByType<RelayPresenter>();
            _folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Previews"));
            Directory.CreateDirectory(_folder);
            yield return new WaitForSeconds(Settle);

            Check(_panel.IsMessageShown && _panel.ShownStep == -1, "the panel opens on the start screen");
            SavePanel("panel_0_start");

            _presenter.TaskStarted();
            yield return new WaitForSeconds(Settle);
            Check(_panel.ShownStep == 0 && !_panel.IsMessageShown, "starting shows step 1");
            SavePanel("panel_1_step_card");

            _presenter.SlipLooking(0, "bottle_B");
            yield return new WaitForSeconds(Settle);
            Check(_panel.IsSlipShown && _panel.slipText.text == "Not bottle B. Take bottle A.", "looking at the wrong bottle warns");
            Check(Highlight("bottle_B").CurrentMode == ObjectHighlight.Mode.Pulse, "the wrong bottle flashes");
            Check(Highlight("bottle_A").CurrentMode == ObjectHighlight.Mode.Pulse, "the right bottle pulses");
            Check(_panel.card.alpha < 0.45f, "the card dims during a slip");
            SavePanel("panel_4_slip_looking");
            SaveParticipantView("scene_slip");

            _presenter.SlipGrabbed(0, "bottle_B");
            yield return new WaitForSeconds(Settle);
            Check(_panel.slipText.text == "Wrong bottle. Put it back, take bottle A.", "grabbing the wrong bottle changes the message");
            Check(Highlight("bottle_B").CurrentMode == ObjectHighlight.Mode.Solid, "the wrong bottle stays red");
            SavePanel("panel_5_slip_grabbed");

            _presenter.SlipCleared();
            yield return new WaitForSeconds(Settle);
            Check(!_panel.IsSlipShown && _panel.card.alpha > 0.95f, "clearing the slip restores the card");
            Check(Highlight("bottle_B").CurrentMode == ObjectHighlight.Mode.Off, "clearing the slip turns the warning off");
            Check(_presenter.WarningCount == 2, "warnings are counted");

            _presenter.Armed(1);
            yield return new WaitForSeconds(Settle);
            Check(_panel.ShownStep == 0 && _panel.GhostStep == 1, "the ghost appears and the card stays on step 1");
            Check(_panel.ghost.alpha > 0.35f && _panel.ghost.alpha < 0.45f, "the ghost is faint");
            Check(Highlight("cap_A").CurrentMode == ObjectHighlight.Mode.Solid, "the next object glows while armed");
            SavePanel("panel_2_ghost");
            SaveParticipantView("scene_armed");

            _presenter.Disarmed();
            yield return new WaitForSeconds(Settle);
            Check(_panel.GhostStep == -1 && Mathf.Approximately(_panel.ghost.alpha, 0f), "cancelling hides the ghost");
            Check(Highlight("cap_A").CurrentMode == ObjectHighlight.Mode.Off, "cancelling turns the glow off");

            _presenter.Armed(1);
            yield return null;
            _presenter.Committed(1);
            Check(_panel.card.alpha < 0.1f, "committing starts the cross-fade");
            Check(Highlight("cap_A").CurrentMode == ObjectHighlight.Mode.FlashOnce, "committing flashes the object");
            yield return new WaitForSeconds(Settle);
            Check(_panel.ShownStep == 1 && _panel.GhostStep == -1, "committing moves to the next step");
            Check(Highlight("cap_A").CurrentMode == ObjectHighlight.Mode.Off, "the flash ends by itself");
            SavePanel("panel_3_committed");

            _presenter.RolledBack(0);
            yield return new WaitForSeconds(Settle);
            Check(_panel.ShownStep == 0 && _panel.IsRollbackNoteShown, "undo goes back and says so");
            SavePanel("panel_6_rollback");
            yield return new WaitForSeconds(1.2f);
            Check(!_panel.IsRollbackNoteShown, "the undo note disappears by itself");

            _presenter.Committed(2);
            _presenter.Paused(2);
            yield return new WaitForSeconds(Settle);
            Check(_panel.IsPausedShown, "pausing shows the resume card");
            Check(Highlight("glass_1").CurrentMode == ObjectHighlight.Mode.Pulse, "pausing marks where to look");
            SavePanel("panel_7_paused");

            _presenter.Resumed();
            yield return new WaitForSeconds(Settle);
            Check(!_panel.IsPausedShown && Highlight("glass_1").CurrentMode == ObjectHighlight.Mode.Off, "resuming clears the pause");

            _presenter.TrackingChanged(true);
            yield return new WaitForSeconds(Settle);
            Check(_panel.IsTrackingNoticeShown && _panel.ShownStep == 2, "losing the eyes warns but keeps the card");
            SavePanel("panel_8_tracking_lost");
            _presenter.TrackingChanged(false);
            yield return null;
            Check(!_panel.IsTrackingNoticeShown, "the notice clears when the eyes come back");

            _panel.source.Data.steps[3].pedalConfirm = true;
            _presenter.Committed(3);
            yield return new WaitForSeconds(Settle);
            Check(_panel.pedalChip.activeSelf, "a step gaze can't be trusted on asks for the pedal");
            SavePanel("panel_9_pedal_step");
            _panel.source.Data.steps[3].pedalConfirm = false;

            _presenter.Completed();
            yield return new WaitForSeconds(Settle);
            Check(_panel.IsMessageShown && _panel.messageTitle.text == "All 5 steps done", "the task ends on the summary");
            SavePanel("panel_10_complete");

            Debug.Log($"[RELAY] TOUR RESULT passed={_passed} failed={_failed}");
            if (Application.isBatchMode) EditorApplication.Exit(_failed == 0 ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        void SavePanel(string imageName)
        {
            Vector3 center = _panel.transform.position;
            Render(Path.Combine(_folder, imageName + ".png"), center + new Vector3(0f, 0f, -0.32f), center, 70.2f, 1600, 900);
        }

        void SaveParticipantView(string imageName)
        {
            Render(Path.Combine(_folder, imageName + ".png"), new Vector3(0f, 1.5f, 0.05f), new Vector3(0f, 0.92f, 0.6f), 70f, 1600, 900);
        }

        static ObjectHighlight Highlight(string id)
        {
            foreach (ObjectHighlight highlight in FindObjectsByType<ObjectHighlight>())
            {
                if (highlight.id == id) return highlight;
            }

            return null;
        }

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
    }
}
#endif
