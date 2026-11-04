using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Relay.EditorTools
{
    /// <summary>
    /// Builds the whole bench scene at real-world size (1 unit = 1 metre), with everything wired up. The room
    /// is deliberately plain: anything eye-catching would pull people's gaze and spoil the data.
    /// </summary>
    public static class RelaySceneBuilder
    {
        const string RootFolder = "Assets/RELAY";
        const string MaterialFolder = "Assets/RELAY/Generated";
        const string ScenePath = "Assets/RELAY/RelayBottleScene.unity";
        const float TableTop = 0.75f;
        const float RowZ = 0.5f;
        static readonly Vector3 ScreenCentre = new Vector3(0f, 1.05f, 0.88f);

        static Material _bottleMat, _labelMat, _capMat, _glassMat, _waterMat, _woodMat, _floorMat, _wallMat;
        static Material _ceilingMat, _lightMat, _darkMat, _metalMat, _inkMat, _buttonMat;

        [MenuItem("RELAY/Build Bottle Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(RootFolder, "Generated");

            RelayMeshes.BeginBuild();
            MakeMaterials();
            ApplyVrQuality();
            BuildRoom();
            BuildBench();

            GameObject tray = MeshObject("CapTray", RelayMeshes.Tray(), _metalMat, null, new Vector3(0f, TableTop, 0.4f));
            var trayFloor = tray.AddComponent<BoxCollider>();
            trayFloor.center = new Vector3(0f, 0.002f, 0f);
            trayFloor.size = new Vector3(0.12f, 0.004f, 0.12f);
            AddAoi(tray, "tray", 0.07f, Vector3.zero);

            string[] bottleIds = { "A", "B", "C" };
            float[] bottleX = { -0.45f, -0.30f, -0.15f };
            for (int i = 0; i < bottleIds.Length; i++)
            {
                BuildBottle(bottleIds[i], new Vector3(bottleX[i], TableTop, RowZ), tray.transform);
            }

            string[] glassIds = { "1", "2", "3" };
            float[] glassX = { 0.15f, 0.30f, 0.45f };
            for (int i = 0; i < glassIds.Length; i++)
            {
                BuildGlass(glassIds[i], new Vector3(glassX[i], TableTop, RowZ));
            }

            RenderStepImages();
            BuildManager();
            bool hasRig = AddXrOrigin();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            if (hasRig)
            {
                Debug.Log($"[RELAY] Scene built: {ScenePath}");
            }
            else
            {
                Debug.LogWarning("[RELAY] Scene built, but the XR Origin prefab wasn't found. In Package Manager, import " +
                                 "the XR Interaction Toolkit 'Starter Assets' sample, then run RELAY > Build Bottle Scene again.");
            }
        }

        // A headset needs a steady 90 frames per second and crisp edges more than it needs fancy effects.
        static void ApplyVrQuality()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pipeline != null)
            {
                pipeline.msaaSampleCount = 4;
                pipeline.shadowDistance = 6f;
                pipeline.supportsHDR = false;
                EditorUtility.SetDirty(pipeline);
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer != null)
            {
                foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                {
                    if (feature != null && feature.name.Contains("AmbientOcclusion")) feature.SetActive(false);
                }

                EditorUtility.SetDirty(renderer);
            }
        }

        static void MakeMaterials()
        {
            _bottleMat = GetMaterial("Bottle", new Color(0.13f, 0.40f, 0.68f), 0.55f, 0.1f);
            _labelMat = GetMaterial("Label", new Color(0.93f, 0.93f, 0.9f), 0.25f, 0f);
            _capMat = GetMaterial("Cap", new Color(0.9f, 0.9f, 0.92f), 0.45f, 0f);
            _glassMat = GetMaterial("Glass", new Color(0.82f, 0.92f, 1f, 0.22f), 0.92f, 0f, true);
            _waterMat = GetMaterial("Water", new Color(0.2f, 0.55f, 0.95f, 0.72f), 0.9f, 0f, true);
            _woodMat = GetMaterial("Wood", Color.white, 0.32f, 0f, false, RelayMeshes.WoodTexture());
            _floorMat = GetMaterial("Floor", Color.white, 0.22f, 0f, false, RelayMeshes.TileTexture(), new Vector2(10f, 10f));
            _wallMat = GetMaterial("Wall", new Color(0.78f, 0.79f, 0.8f), 0.1f, 0f);
            _ceilingMat = GetMaterial("Ceiling", new Color(0.86f, 0.86f, 0.86f), 0.05f, 0f);
            _lightMat = GetMaterial("LightPanel", Color.white, 0.5f, 0f);
            _lightMat.SetColor("_EmissionColor", new Color(1.3f, 1.3f, 1.25f));
            _darkMat = GetMaterial("Dark", new Color(0.07f, 0.08f, 0.09f), 0.45f, 0f);
            _metalMat = GetMaterial("Metal", new Color(0.48f, 0.5f, 0.53f), 0.5f, 0.2f);
            _inkMat = GetMaterial("Ink", new Color(0.1f, 0.11f, 0.13f), 0.2f, 0f);
            _buttonMat = GetMaterial("Button", new Color(0.12f, 0.62f, 0.32f), 0.5f, 0f);
        }

        static void BuildRoom()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.GetComponent<Renderer>().sharedMaterial = _floorMat;
            floor.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            const float HalfWidth = 2.5f;
            const float Back = 3.2f;
            const float Front = -2.2f;
            const float Height = 2.8f;
            float depth = Back - Front;
            float midZ = (Back + Front) * 0.5f;

            RoomBox("WallBack", new Vector3(0f, Height * 0.5f, Back), new Vector3(HalfWidth * 2f, Height, 0.1f), _wallMat);
            RoomBox("WallFront", new Vector3(0f, Height * 0.5f, Front), new Vector3(HalfWidth * 2f, Height, 0.1f), _wallMat);
            RoomBox("WallLeft", new Vector3(-HalfWidth, Height * 0.5f, midZ), new Vector3(0.1f, Height, depth), _wallMat);
            RoomBox("WallRight", new Vector3(HalfWidth, Height * 0.5f, midZ), new Vector3(0.1f, Height, depth), _wallMat);
            RoomBox("Ceiling", new Vector3(0f, Height, midZ), new Vector3(HalfWidth * 2f, 0.1f, depth), _ceilingMat);
            RoomBox("LightPanelFront", new Vector3(0f, Height - 0.06f, -0.4f), new Vector3(1.2f, 0.02f, 0.6f), _lightMat);
            RoomBox("LightPanelBack", new Vector3(0f, Height - 0.06f, 1.6f), new Vector3(1.2f, 0.02f, 0.6f), _lightMat);

            GameObject sun = GameObject.Find("Directional Light");
            if (sun != null)
            {
                sun.name = "Ceiling Light";
                sun.transform.rotation = Quaternion.Euler(62f, -18f, 0f);
                var light = sun.GetComponent<Light>();
                light.intensity = 0.85f;
                light.color = new Color(1f, 0.98f, 0.95f);
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.55f;
                RenderSettings.sun = light;
            }

            // Set directly, so the room doesn't depend on baked lighting.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.73f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.6f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.39f, 0.38f);
        }

        // Shell pieces never cast shadows, or the ceiling would block the light.
        static void RoomBox(string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = VisualBox(name, position, size, material, null);
            box.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildBench()
        {
            var table = new GameObject("Table");
            table.transform.position = new Vector3(0f, TableTop, 0.6f);

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Top";
            top.transform.SetParent(table.transform, false);
            top.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            top.transform.localScale = new Vector3(1.4f, 0.04f, 0.7f);
            top.GetComponent<Renderer>().sharedMaterial = _woodMat;

            float[] legX = { -0.64f, 0.64f };
            float[] legZ = { -0.29f, 0.29f };
            foreach (float x in legX)
            {
                foreach (float z in legZ)
                {
                    VisualBox("Leg", table.transform.position + new Vector3(x, -0.395f, z), new Vector3(0.05f, 0.71f, 0.05f), _metalMat, table.transform);
                }
            }

            VisualBox("RailBack", table.transform.position + new Vector3(0f, -0.09f, 0.29f), new Vector3(1.28f, 0.06f, 0.025f), _metalMat, table.transform);
            VisualBox("RailLeft", table.transform.position + new Vector3(-0.64f, -0.09f, 0f), new Vector3(0.025f, 0.06f, 0.58f), _metalMat, table.transform);
            VisualBox("RailRight", table.transform.position + new Vector3(0.64f, -0.09f, 0f), new Vector3(0.025f, 0.06f, 0.58f), _metalMat, table.transform);

            var monitor = new GameObject("Monitor");
            monitor.transform.position = ScreenCentre;
            VisualBox("Frame", ScreenCentre + new Vector3(0f, 0f, 0.02f), new Vector3(0.84f, 0.49f, 0.035f), _darkMat, monitor.transform);
            VisualBox("Neck", new Vector3(0f, (TableTop + ScreenCentre.y) * 0.5f, ScreenCentre.z + 0.05f), new Vector3(0.06f, ScreenCentre.y - TableTop, 0.03f), _darkMat, monitor.transform);
            VisualBox("Foot", new Vector3(0f, TableTop + 0.006f, ScreenCentre.z + 0.03f), new Vector3(0.28f, 0.012f, 0.1f), _darkMat, monitor.transform);
        }

        static void BuildBottle(string id, Vector3 position, Transform tray)
        {
            GameObject spot = MeshObject($"Spot_{id}", RelayMeshes.SpotRing(), _inkMat, null, position + Vector3.up * 0.001f);
            AddAoi(spot, $"spot_{id}", 0.05f, Vector3.zero);
            AddHighlight(spot, $"spot_{id}", spot.GetComponent<Renderer>());
            AddFlatLabel(id, spot.transform, new Vector3(0f, 0.001f, -0.07f));

            // The pivot is at the base, so bottles stand on their marked spots.
            var root = new GameObject($"Bottle_{id}");
            root.transform.position = position + Vector3.up * 0.002f;

            GameObject body = MeshObject("Body", RelayMeshes.Bottle(), _bottleMat, root.transform, root.transform.position);
            MeshObject("LabelBand", RelayMeshes.BottleLabel(), _labelMat, root.transform, root.transform.position);
            AddBottleLetter(id, root.transform);

            // A box, because a bottle must be able to stand and a round collider would tip over.
            var bodyShape = new GameObject("BodyCollider");
            bodyShape.transform.SetParent(root.transform, false);
            bodyShape.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var bodyCollider = bodyShape.AddComponent<BoxCollider>();
            bodyCollider.size = new Vector3(0.07f, 0.2f, 0.07f);

            var mouth = new GameObject("Mouth").transform;
            mouth.SetParent(root.transform, false);
            mouth.localPosition = new Vector3(0f, 0.205f, 0f);

            var seat = new GameObject("CapSeat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(0f, 0.21f, 0f);

            var gripObject = new GameObject("CapGrip");
            gripObject.transform.SetParent(root.transform, false);
            gripObject.transform.localPosition = seat.localPosition;
            var gripCollider = gripObject.AddComponent<SphereCollider>();
            gripCollider.radius = 0.03f;
            var grip = gripObject.AddComponent<CapGripInteractable>();
            grip.colliders.Add(gripCollider);

            var bottleGrab = root.AddComponent<NoStealGrabInteractable>();
            ConfigureGrab(bottleGrab, bodyCollider);
            root.GetComponent<Rigidbody>().mass = 0.5f;

            var bottle = root.AddComponent<Bottle>();
            bottle.id = id;
            bottle.mouth = mouth;
            root.AddComponent<BottlePour>().waterMaterial = _waterMat;
            root.AddComponent<FallRespawn>().label = $"bottle_{id}";
            AddAoi(root, $"bottle_{id}", 0.1f, new Vector3(0f, 0.1f, 0f));
            AddHighlight(root, $"bottle_{id}", body.GetComponent<Renderer>());

            bottle.cap = BuildCap(id, bottle, seat, grip, tray);
        }

        static BottleCap BuildCap(string id, Bottle bottle, Transform seat, CapGripInteractable grip, Transform tray)
        {
            var root = new GameObject($"Cap_{id}");
            root.transform.position = seat.position;

            GameObject shape = MeshObject("Shape", RelayMeshes.Cap(), _capMat, root.transform, seat.position);
            var capCollider = root.AddComponent<BoxCollider>();
            capCollider.size = new Vector3(0.036f, 0.022f, 0.036f);

            var capGrab = root.AddComponent<NoStealGrabInteractable>();
            ConfigureGrab(capGrab, capCollider);
            Rigidbody rb = root.GetComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.isKinematic = true; // BottleCap seats it at start

            var cap = root.AddComponent<BottleCap>();
            cap.bottle = bottle;
            cap.seat = seat;
            cap.grip = grip;

            var respawn = root.AddComponent<FallRespawn>();
            respawn.label = $"cap_{id}";
            respawn.home = tray;
            AddAoi(root, $"cap_{id}", 0.025f, Vector3.zero);
            AddHighlight(root, $"cap_{id}", shape.GetComponent<Renderer>());
            return cap;
        }

        static void ConfigureGrab(NoStealGrabInteractable grab, Collider collider)
        {
            grab.colliders.Add(collider);
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true; // holds it where the hand took it, with no jump to the palm
        }

        static void BuildGlass(string id, Vector3 position)
        {
            var root = new GameObject($"Glass_{id}");
            root.transform.position = position;

            var glass = root.AddComponent<Glass>();
            glass.id = id;

            GameObject shell = MeshObject("Shell", RelayMeshes.Glass(), _glassMat, root.transform, position);

            // A Unity cylinder is 1 unit wide and 2 units tall; Glass scales it to match the water level.
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(root.transform, false);
            water.GetComponent<Renderer>().sharedMaterial = _waterMat;
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            water.SetActive(false);
            glass.waterVisual = water.transform;

            GameObject line = MeshObject("FillLine", RelayMeshes.FillLine(), _inkMat, root.transform, position);
            line.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            glass.fillLine = line.transform;

            AddFlatLabel(id, root.transform, new Vector3(0f, 0.001f, -0.075f));
            AddAoi(root, $"glass_{id}", 0.06f, new Vector3(0f, 0.06f, 0f));
            AddHighlight(root, $"glass_{id}", shell.GetComponent<Renderer>());
        }

        static void BuildManager()
        {
            var manager = new GameObject("RelayManager");
            var source = manager.AddComponent<TaskSource>();
            var truth = manager.AddComponent<TaskTruth>();
            truth.source = source;
            var gaze = manager.AddComponent<GazeInput>();
            var tracker = manager.AddComponent<GazeObjectTracker>();
            tracker.gaze = gaze;
            manager.AddComponent<RelayRigSetup>();

            var signals = manager.AddComponent<RelaySignals>();
            signals.gaze = tracker;

            // On the monitor behind the bench, low enough that a look up from the work is a short one.
            InstructionPanel panel = RelayPanelBuilder.Build(source, ScreenCentre);
            AddAoi(panel.gameObject, "display", 0.3f, Vector3.zero);

            var presenter = manager.AddComponent<RelayPresenter>();
            presenter.panel = panel;
            presenter.source = source;

            manager.AddComponent<ManualEngine>();
            manager.AddComponent<TouchEngine>().button = BuildTouchButton(signals);

            var recorder = manager.AddComponent<TrialRecorder>();
            recorder.truth = truth;
            recorder.source = source;
            recorder.panel = panel;
            recorder.gaze = tracker;

            var session = manager.AddComponent<RelaySession>();
            session.source = source;
            session.truth = truth;
            session.presenter = presenter;
            session.signals = signals;
            session.recorder = recorder;

            manager.AddComponent<RelayDeskMode>();

            // Shows the true step, so it starts switched off. Tick it on only while building.
            var labelObject = new GameObject("DebugStepLabel");
            labelObject.transform.position = new Vector3(0f, 1.4f, 0.88f);
            var label = labelObject.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.anchor = TextAnchor.MiddleCenter;
            label.characterSize = 0.01f;
            label.fontSize = 40;
            truth.debugLabel = label;
            labelObject.SetActive(false);
        }

        // Starts switched off: it only exists in the touch condition, and elsewhere it would draw the eyes.
        static TouchButton BuildTouchButton(RelaySignals signals)
        {
            var root = new GameObject("TouchButton");
            root.transform.position = new Vector3(0.55f, TableTop, 0.33f);
            MeshObject("Base", RelayMeshes.ButtonBase(), _darkMat, root.transform, root.transform.position);
            GameObject top = MeshObject("Top", RelayMeshes.ButtonTop(), _buttonMat, root.transform, root.transform.position);
            top.transform.localPosition = new Vector3(0f, 0.012f, 0f);

            var shape = root.AddComponent<BoxCollider>();
            shape.center = new Vector3(0f, 0.016f, 0f);
            shape.size = new Vector3(0.1f, 0.032f, 0.1f);
            var interactable = root.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Add(shape);

            var button = root.AddComponent<TouchButton>();
            button.signals = signals;
            button.movingPart = top.transform;
            AddAoi(root, "button", 0.06f, new Vector3(0f, 0.02f, 0f));
            root.SetActive(false);
            return button;
        }

        // A picture of each object alone on the bench, for the step cards.
        static void RenderStepImages()
        {
            const string Folder = RootFolder + "/Resources/StepImages";
            if (!AssetDatabase.IsValidFolder(RootFolder + "/Resources")) AssetDatabase.CreateFolder(RootFolder, "Resources");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(RootFolder + "/Resources", "StepImages");

            Bottle[] bottles = Object.FindObjectsByType<Bottle>();
            BottleCap[] caps = Object.FindObjectsByType<BottleCap>();
            Glass[] glasses = Object.FindObjectsByType<Glass>();
            AoiTarget[] targets = Object.FindObjectsByType<AoiTarget>();
            string[] letters = { "A", "B", "C" };

            foreach (AoiTarget target in targets)
            {
                string kind = StepContent.Kind(target.id);
                Vector3 offset;
                float fieldOfView;
                switch (kind)
                {
                    case "bottle": offset = new Vector3(0f, 0.1f, -0.4f); fieldOfView = 38f; break;
                    case "cap": offset = new Vector3(0f, 0.08f, -0.18f); fieldOfView = 32f; break;
                    case "glass": offset = new Vector3(0f, 0.1f, -0.3f); fieldOfView = 34f; break;
                    case "spot": offset = new Vector3(0f, 0.2f, -0.2f); fieldOfView = 40f; break;
                    default: continue;
                }

                string letter = target.id.Substring(target.id.IndexOf('_') + 1);
                bool showBottle = kind == "bottle" || kind == "cap";
                foreach (Bottle b in bottles) b.gameObject.SetActive(showBottle && b.id == letter);
                foreach (BottleCap c in caps) c.gameObject.SetActive(showBottle && c.bottle.id == letter);
                foreach (Glass g in glasses) g.gameObject.SetActive(kind == "glass" && g.id == letter);
                foreach (string other in letters) SetSpotActive(other, other == letter);

                string path = $"{Folder}/{target.id}.png";
                Vector3 center = target.Center;
                RelayPanelTour.Render(path, center + offset, center, fieldOfView, 640, 480);
                AssetDatabase.ImportAsset(path);
            }

            foreach (Bottle b in bottles) b.gameObject.SetActive(true);
            foreach (BottleCap c in caps) c.gameObject.SetActive(true);
            foreach (Glass g in glasses) g.gameObject.SetActive(true);
            foreach (string other in letters) SetSpotActive(other, true);
        }

        // Hidden spots can't be found by name, so their renderers are switched rather than the objects.
        static void SetSpotActive(string letter, bool active)
        {
            GameObject spot = GameObject.Find($"Spot_{letter}");
            if (spot == null) return;
            foreach (Renderer r in spot.GetComponentsInChildren<Renderer>()) r.enabled = active;
        }

        static bool AddXrOrigin()
        {
            string[] guids = AssetDatabase.FindAssets("\"XR Origin (XR Rig)\" t:Prefab");
            string path = null;
            foreach (string guid in guids)
            {
                string candidate = AssetDatabase.GUIDToAssetPath(guid);
                if (candidate.Contains("Starter Assets"))
                {
                    path = candidate;
                    break;
                }
            }

            if (path == null) return false;

            // The rig brings its own camera.
            GameObject defaultCamera = GameObject.Find("Main Camera");
            if (defaultCamera != null) Object.DestroyImmediate(defaultCamera);

            var origin = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            origin.transform.position = Vector3.zero;

            // Participants stay at the bench.
            Transform locomotion = origin.transform.Find("Locomotion");
            if (locomotion != null) locomotion.gameObject.SetActive(false);
            return true;
        }

        static GameObject MeshObject(string name, Mesh mesh, Material material, Transform parent, Vector3 worldPosition)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static GameObject VisualBox(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            if (parent != null) box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        static void AddAoi(GameObject target, string id, float radius, Vector3 localCenter)
        {
            var aoi = target.AddComponent<AoiTarget>();
            aoi.id = id;
            aoi.radiusMeters = radius;
            aoi.localCenter = localCenter;
        }

        static void AddHighlight(GameObject target, string id, params Renderer[] renderers)
        {
            var highlight = target.AddComponent<ObjectHighlight>();
            highlight.id = id;
            highlight.renderers = renderers;
        }

        static void AddBottleLetter(string text, Transform parent)
        {
            TextMeshPro label = AddText(text, parent, new Vector3(0f, 0.08f, -0.0365f), 0.32f);
            label.color = new Color(0.08f, 0.1f, 0.13f);
        }

        // Lies flat on the bench, reading towards the person.
        static void AddFlatLabel(string text, Transform parent, Vector3 localPosition)
        {
            TextMeshPro label = AddText(text, parent, localPosition, 0.3f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label.color = new Color(0.08f, 0.1f, 0.13f);
        }

        static TextMeshPro AddText(string text, Transform parent, Vector3 localPosition, float size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var label = go.AddComponent<TextMeshPro>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(0.1f, 0.06f);
            return label;
        }

        static Material GetMaterial(string name, Color color, float smoothness, float metallic, bool transparent = false, Texture2D texture = null, Vector2? tiling = null)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (texture != null) material.mainTexture = texture;
            if (tiling.HasValue) material.mainTextureScale = tiling.Value;

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            // ObjectHighlight glows through this; it stays black until something is highlighted.
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
