using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Relay.EditorTools
{
    public static class RelayTools
    {
        const string ScenePath = "Assets/RELAY/RelayBottleScene.unity";
        const int Width = 1600;
        const int Height = 900;

        [MenuItem("RELAY/Run Task Test")]
        public static void RunTaskTest()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(ScenePath);
            RelayTaskTest.RequestRun();
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("RELAY/Run Full Trial Test")]
        public static void RunFullTrialTest()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(ScenePath);
            RelayDeskTest.RequestRun();
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("RELAY/Save Panel Images")]
        public static void SavePanelImages()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(ScenePath);
            RelayPanelTour.RequestRun();
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Saves pictures of the scene to the Previews folder, for reports and slides.</summary>
        [MenuItem("RELAY/Save Preview Images")]
        public static void SavePreviewImages()
        {
            EditorSceneManager.OpenScene(ScenePath);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Previews"));
            Directory.CreateDirectory(folder);

            RelayPanelTour.Render(Path.Combine(folder, "participant_view.png"), new Vector3(0f, 1.5f, 0.05f), new Vector3(0f, 0.92f, 0.6f), 70f, Width, Height);
            RelayPanelTour.Render(Path.Combine(folder, "overview.png"), new Vector3(-1.3f, 1.6f, -0.9f), new Vector3(0f, 0.85f, 0.55f), 50f, Width, Height);
            RelayPanelTour.Render(Path.Combine(folder, "bottle_closeup.png"), new Vector3(-0.3f, 1.05f, 0.05f), new Vector3(-0.3f, 0.85f, 0.5f), 40f, Width, Height);

            // Half-fill glasses so the picture shows the water. The scene isn't saved, so this doesn't last.
            foreach (Glass glass in Object.FindObjectsByType<Glass>()) glass.AddWater(glass.capacityMl * 0.5f);
            RelayPanelTour.Render(Path.Combine(folder, "glasses_closeup.png"), new Vector3(0.3f, 1.0f, 0.15f), new Vector3(0.3f, 0.82f, 0.5f), 40f, Width, Height);
            Debug.Log($"[RELAY] Preview images saved to {folder}");
        }
    }
}
