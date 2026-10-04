using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRApartment.Data;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    /// <summary>
    /// Builds the swappable kitchen review scene SCN_Kitchen_Review for PCVR (Quest Link /
    /// Virtual Desktop streaming, TECH-02): Starter Assets rig + teleport, a greybox kitchen
    /// whose dimensions are read from the registered ModelAssumptions (MOD-01, never
    /// hard-coded claims), fridge prefabs for archetypes F2/F3/F4 with kinematic door/drawer
    /// interactions (FR-04), and preset benchmark swapping B1/B2/B3 (PRD 13.3, D03).
    ///
    /// Menu: XR Apartment → Build Kitchen Review Scene, then XR Apartment → Benchmark →
    /// Apply B1/B2/B3 (also works in Play mode for live comparison).
    /// </summary>
    public static class KitchenSceneBootstrap
    {
        const string Marker = "[KITCHEN]";
        const string SampleRoot = "Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets";
        const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        const string ScenePath = "Assets/_Project/Scenes/SCN_Kitchen_Review.unity";
        const string PrefabRoot = "Assets/_Project/Prefabs";
        const string MaterialRoot = "Assets/_Project/Materials";
        const string SettingsRoot = "Assets/_Project/Settings";

        static ModelAssumptions assumptions;

        // ------------------------------------------------------------------
        // Menu entries
        // ------------------------------------------------------------------

        [MenuItem("XR Apartment/Build Kitchen Review Scene")]
        public static void BuildKitchenScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogError($"{Marker} scene build is edit-mode only — exit Play mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            assumptions = EnsureAssumptions();
            EnsureRegistry();
            EnsureMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var defaultCamera = GameObject.Find("Main Camera");
            if (defaultCamera != null) Object.DestroyImmediate(defaultCamera);

            BuildLighting();
            Spawn($"{SampleRoot}/Prefabs/XR Origin (XR Rig).prefab", new Vector3(0f, 0f, 0.9f), 180f);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<XRUIInputModule>();
            EnsureInteractionManager();

            BuildShell();
            BuildKitchenRun();
            BuildBenchmarkRoot();
            PlaceProps();
            BuildMenuPanel();
            Spawn($"{SampleRoot}/DemoAssets/Prefabs/Teleport/Teleport Anchor.prefab", new Vector3(0f, 0.02f, 0.45f));

            ApplyBenchmark("B1");

            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();

            var marker = Object.FindFirstObjectByType<BenchmarkSlotMarker>();
            Debug.Log($"{Marker} saved={saved} path={ScenePath} benchmark={marker.BenchmarkId} " +
                      $"buildScenes={EditorBuildSettings.scenes.Length}. Swap via XR Apartment/Benchmark menu.");
        }

        [MenuItem("XR Apartment/Benchmark/Apply B1 - Proud + F2")]
        public static void ApplyB1() => ApplyBenchmark("B1");

        [MenuItem("XR Apartment/Benchmark/Apply B2 - Flush + F2")]
        public static void ApplyB2() => ApplyBenchmark("B2");

        [MenuItem("XR Apartment/Benchmark/Apply B3 - Flush + F4")]
        public static void ApplyB3() => ApplyBenchmark("B3");

        // ------------------------------------------------------------------
        // Benchmark swapping (D03 preset configuration switching)
        // ------------------------------------------------------------------

        public static void ApplyBenchmark(string benchmarkId)
        {
            var marker = Object.FindFirstObjectByType<BenchmarkSlotMarker>();
            if (marker == null)
            {
                Debug.LogError($"{Marker} no BENCH_Benchmark in the open scene — run 'XR Apartment/Build Kitchen Review Scene' first.");
                return;
            }

            var registry = AssetDatabase.LoadAssetAtPath<BenchmarkRegistry>($"{SettingsRoot}/REG_Benchmarks.asset");
            var def = registry != null ? registry.Find(benchmarkId) : null;
            if (def == null)
            {
                Debug.LogError($"{Marker} benchmark '{benchmarkId}' not found in REG_Benchmarks.");
                return;
            }

            var slot = marker.transform.Find("SLOT_Fridge");
            if (slot == null)
            {
                Debug.LogError($"{Marker} SLOT_Fridge missing under BENCH_Benchmark.");
                return;
            }

            for (var i = slot.childCount - 1; i >= 0; i--)
                DestroySafe(slot.GetChild(i).gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/PREF_Fridge_{def.Fridge}.prefab");
            if (prefab == null)
            {
                Debug.LogError($"{Marker} fridge prefab missing: PREF_Fridge_{def.Fridge}. Run the build menu once.");
                return;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(slot, false);
            instance.transform.localPosition = Vector3.zero;

            // Flush-oriented (B2/B3) shows the filler panels that bring the cabinetry line out
            // to the fridge front; Proud (B1) leaves the fridge standing proud of the run.
            var flush = def.Cabinet == CabinetStrategy.FlushOriented;
            foreach (var trimName in new[] { "TRIM_Filler_Left", "TRIM_Filler_Right", "TRIM_Bridge_Top" })
            {
                var trim = marker.transform.Find(trimName);
                if (trim != null) trim.gameObject.SetActive(flush);
            }

            marker.BenchmarkId = def.Id;
            marker.Fridge = def.Fridge;
            marker.Cabinet = def.Cabinet;
            EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
            Debug.Log($"{Marker} applied {def.Id}: {def.Cabinet} + {def.Fridge}");
        }

        static void DestroySafe(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------
        // Registered geometry assumptions (MOD-01)
        // ------------------------------------------------------------------

        static readonly (string key, float metres, string note)[] GreyboxParameters =
        {
            ("room_width", 3.6f, "灰盒房间宽度"),
            ("room_depth", 3.0f, "灰盒房间进深"),
            ("room_height", 2.6f, "灰盒房间净高"),
            ("counter_height", 0.90f, "台面完成面高（厨房人体工学常用值）"),
            ("counter_depth", 0.60f, "柜体进深"),
            ("counter_run_length", 2.22f, "冰箱槽右侧的连续台面长度"),
            ("worktop_thickness", 0.04f, "台面板厚度"),
            ("toe_kick_height", 0.10f, "踢脚高度"),
            ("upper_cabinet_bottom", 1.50f, "吊柜底面高"),
            ("upper_cabinet_depth", 0.35f, "吊柜进深"),
            ("walkway_clearance", 1.00f, "台面前通行净距（单人厨房下限）"),
            ("fridge_slot_width", 0.78f, "冰箱槽宽度（含两侧留缝）"),
            ("fridge_width", 0.75f, "冰箱灰盒宽度"),
            ("fridge_depth", 0.70f, "冰箱灰盒深度"),
            ("fridge_height", 1.85f, "冰箱灰盒高度"),
            ("fridge_door_thickness", 0.05f, "门板厚度"),
            ("fridge_proud_offset", 0.10f, "Proud 状态冰箱前缘超出台面前缘的距离"),
            ("fridge_drawer_travel", 0.35f, "抽屉演示行程"),
        };

        static ModelAssumptions EnsureAssumptions()
        {
            var path = $"{SettingsRoot}/ASM_KitchenParameters.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ModelAssumptions>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<ModelAssumptions>();
            asset.SetModelVersion("0.1-graybox");
            foreach (var (key, metres, note) in GreyboxParameters)
            {
                asset.Upsert(new AssumptionParameter
                {
                    key = key,
                    valueMetres = metres,
                    source = note,
                    owner = "D",
                    status = "assumption",
                    verification = "pending",
                });
            }
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"{Marker} created {path} (MOD-01 greybox assumptions)");
            return asset;
        }

        /// Reads one registered length; falls back only when the key was removed from the register.
        static float M(string key, float fallback)
        {
            if (assumptions != null && assumptions.TryGetLength(key, out var metres)) return metres;
            Debug.LogWarning($"{Marker} assumption '{key}' missing in ASM_KitchenParameters, using fallback {fallback}m");
            return fallback;
        }

        static BenchmarkRegistry EnsureRegistry()
        {
            var path = $"{SettingsRoot}/REG_Benchmarks.asset";
            var registry = AssetDatabase.LoadAssetAtPath<BenchmarkRegistry>(path);
            if (registry != null) return registry;

            registry = ScriptableObject.CreateInstance<BenchmarkRegistry>();
            var entries = new[]
            {
                ("B1", CabinetStrategy.Proud, FridgeArchetype.F2),
                ("B2", CabinetStrategy.FlushOriented, FridgeArchetype.F2),
                ("B3", CabinetStrategy.FlushOriented, FridgeArchetype.F4),
            };
            var list = new List<BenchmarkDefinition>();
            foreach (var (id, cabinet, fridge) in entries)
            {
                var def = ScriptableObject.CreateInstance<BenchmarkDefinition>();
                def.Configure(id, cabinet, fridge, SessionMode.Both, string.Empty);
                AssetDatabase.CreateAsset(def, $"{SettingsRoot}/BENCH_{id}.asset");
                list.Add(def);
            }

            var so = new SerializedObject(registry);
            var prop = so.FindProperty("benchmarks");
            prop.arraySize = list.Count;
            for (var i = 0; i < list.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(registry, path);
            Debug.Log($"{Marker} created {path} with the frozen PRD 13.3 mapping B1/B2/B3");
            return registry;
        }

        // ------------------------------------------------------------------
        // Scene construction
        // ------------------------------------------------------------------

        static void BuildLighting()
        {
            var light = GameObject.Find("Directional Light");
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var sun = light.GetComponent<Light>();
                if (sun != null) { sun.intensity = 1.05f; sun.shadows = LightShadows.Soft; }
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);

            var lampGo = new GameObject("Lamp_Counter");
            lampGo.transform.position = new Vector3(0.2f, 2.35f, -0.9f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 4.5f;
            lamp.intensity = 0.7f;
            lamp.color = new Color(1f, 0.95f, 0.85f);
        }

        static void EnsureInteractionManager()
        {
            if (Object.FindFirstObjectByType<XRInteractionManager>() != null) return;
            var manager = new GameObject("XR Interaction Manager");
            manager.AddComponent<XRInteractionManager>();
        }

        static void BuildShell()
        {
            var shell = new GameObject("ENV_Kitchen_Shell");
            float roomW = M("room_width", 3.6f);
            float roomD = M("room_depth", 3.0f);
            float roomH = M("room_height", 2.6f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(shell.transform, false);
            floor.transform.localScale = new Vector3(roomW / 10f, 1f, roomD / 10f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = Mat("MAT_Kitchen_Floor");
            floor.AddComponent<TeleportationArea>();

            Box(shell.transform, "Wall_Back", new Vector3(0f, roomH / 2f, -roomD / 2f - 0.05f),
                new Vector3(roomW + 0.2f, roomH, 0.1f), Mat("MAT_Kitchen_Wall"));
            Box(shell.transform, "Wall_Left", new Vector3(-roomW / 2f - 0.05f, roomH / 2f, 0f),
                new Vector3(0.1f, roomH, roomD + 0.2f), Mat("MAT_Kitchen_Wall"));
            Box(shell.transform, "Wall_Right", new Vector3(roomW / 2f + 0.05f, roomH / 2f, 0f),
                new Vector3(0.1f, roomH, roomD + 0.2f), Mat("MAT_Kitchen_Wall"));
            Box(shell.transform, "Window", new Vector3(-roomW / 2f + 0.005f, 1.6f, -0.2f),
                new Vector3(0.02f, 1.0f, 1.2f), Mat("MAT_Window_Glow"));
        }

        static void BuildKitchenRun()
        {
            var run = new GameObject("ENV_Kitchen_Run");

            float roomD = M("room_depth", 3.0f);
            float ch = M("counter_height", 0.90f);
            float cd = M("counter_depth", 0.60f);
            float wt = M("worktop_thickness", 0.04f);
            float toe = M("toe_kick_height", 0.10f);
            float runLen = M("counter_run_length", 2.22f);
            float ub = M("upper_cabinet_bottom", 1.50f);
            float ud = M("upper_cabinet_depth", 0.35f);

            const float runStart = -0.72f; // fridge slot (x -1.5..-0.72) sits to the left of the run
            float runEnd = runStart + runLen;
            float runCx = (runStart + runEnd) / 2f;
            float backZ = -roomD / 2f;

            var cabinet = Mat("MAT_Kitchen_Cabinet");
            var interior = Mat("MAT_Kitchen_Interior");
            var metal = Mat("MAT_Kitchen_Metal");

            float bodyTop = ch - wt;
            Box(run.transform, "ToeKick", new Vector3(runCx, toe / 2f, backZ + 0.275f),
                new Vector3(runLen, toe, 0.55f), Mat("MAT_Kitchen_ToeKick"));
            Box(run.transform, "CabinetBody", new Vector3(runCx, (toe + bodyTop) / 2f, backZ + 0.29f),
                new Vector3(runLen, bodyTop - toe, 0.58f), cabinet);

            // door seams + bars so the run reads as four base cabinets and four wall units
            float bodyFrontZ = backZ + 0.58f;
            const float upperTop = 2.2f;
            float[] seams = { -0.165f, 0.39f, 0.945f };
            foreach (var seamX in seams)
            {
                Box(run.transform, "CabinetSeam", new Vector3(seamX, (toe + bodyTop) / 2f, bodyFrontZ + 0.002f),
                    new Vector3(0.012f, bodyTop - toe - 0.04f, 0.006f), interior);
                Box(run.transform, "UpperSeam", new Vector3(seamX, (ub + upperTop) / 2f, backZ + ud + 0.002f),
                    new Vector3(0.012f, upperTop - ub - 0.04f, 0.006f), interior);
                Box(run.transform, "Handle_Lower", new Vector3(seamX + 0.27f, bodyTop - 0.10f, bodyFrontZ + 0.025f),
                    new Vector3(0.14f, 0.02f, 0.03f), metal);
                Box(run.transform, "Handle_Upper", new Vector3(seamX + 0.27f, ub - 0.04f, backZ + ud + 0.025f),
                    new Vector3(0.14f, 0.02f, 0.03f), metal);
            }

            Box(run.transform, "UpperCabinets", new Vector3(runCx, (ub + upperTop) / 2f, backZ + ud / 2f),
                new Vector3(runLen, upperTop - ub, ud), cabinet);

            var top = Box(run.transform, "CounterTop", new Vector3(runCx, ch - wt / 2f, backZ + (cd + 0.02f) / 2f),
                new Vector3(runLen + 0.04f, wt, cd + 0.02f), Mat("MAT_Kitchen_CounterTop"));
            TagReview(top, ReviewRole.CounterSurface, ElementStatus.Simplified, "counter_height", "操作台面");

            Box(run.transform, "Sink_Basin", new Vector3(0.10f, ch + 0.001f, backZ + 0.32f),
                new Vector3(0.46f, 0.012f, 0.36f), interior);
            Cyl(run.transform, "Sink_FaucetPost", new Vector3(0.10f, ch + 0.14f, backZ + 0.08f),
                new Vector3(0.035f, 0.14f, 0.035f), metal);
            Box(run.transform, "Sink_FaucetSpout", new Vector3(0.10f, ch + 0.27f, backZ + 0.16f),
                new Vector3(0.03f, 0.03f, 0.16f), metal);

            float walk = M("walkway_clearance", 1.00f);
            var zone = Box(run.transform, "ZONE_Circulation",
                new Vector3(runCx, 0.004f, backZ + cd + 0.02f + walk / 2f),
                new Vector3(runLen, 0.002f, walk), Mat("MAT_Zone_Circulation"));
            // visual marker only: its collider would eat teleport rays across the walkway
            Object.DestroyImmediate(zone.GetComponent<Collider>());
            TagReview(zone, ReviewRole.CirculationZone, ElementStatus.Assumption, "walkway_clearance", "通行净距");
        }

        static void BuildBenchmarkRoot()
        {
            var bench = new GameObject("BENCH_Benchmark");
            bench.AddComponent<BenchmarkSlotMarker>();

            float roomD = M("room_depth", 3.0f);
            float slotW = M("fridge_slot_width", 0.78f);
            float fw = M("fridge_width", 0.75f);
            float fd = M("fridge_depth", 0.70f);
            float fh = M("fridge_height", 1.85f);

            const float slotStartX = -1.5f; // 0.30m margin from the left wall
            float slotCx = slotStartX + slotW / 2f;
            float backZ = -roomD / 2f;
            const float backGap = 0.02f;
            float trimZ = backZ + backGap + fd / 2f;

            var slot = new GameObject("SLOT_Fridge");
            slot.transform.SetParent(bench.transform, false);
            slot.transform.localPosition = new Vector3(slotCx, 0f, trimZ);

            // Flush-oriented trim: filler panels + top bridge bring the tall-unit line out to
            // the fridge front so the appliance reads as built-in. Hidden for Proud (B1).
            var cabinet = Mat("MAT_Kitchen_Cabinet");
            Box(bench.transform, "TRIM_Filler_Left", new Vector3(slotCx - fw / 2f - 0.015f, fh / 2f, trimZ),
                new Vector3(0.03f, fh, fd), cabinet);
            Box(bench.transform, "TRIM_Filler_Right", new Vector3(slotCx + fw / 2f + 0.015f, fh / 2f, trimZ),
                new Vector3(0.03f, fh, fd), cabinet);
            Box(bench.transform, "TRIM_Bridge_Top", new Vector3(slotCx, fh + 0.175f, trimZ),
                new Vector3(slotW, 0.35f, fd), cabinet);

            EnsureFridgePrefabs();
        }

        // ------------------------------------------------------------------
        // Fridge prefabs (F2 双门 / F3 三门 / F4 四门十字), kinematic openings
        // ------------------------------------------------------------------

        static void EnsureFridgePrefabs()
        {
            float fw = M("fridge_width", 0.75f);
            float fd = M("fridge_depth", 0.70f);
            float fh = M("fridge_height", 1.85f);
            float ft = M("fridge_door_thickness", 0.05f);

            foreach (FridgeArchetype archetype in System.Enum.GetValues(typeof(FridgeArchetype)))
            {
                var path = $"{PrefabRoot}/PREF_Fridge_{archetype}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;

                var go = BuildFridge(archetype, fw, fd, fh, ft);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
                Debug.Log($"{Marker} created prefab {path}");
            }
        }

        static GameObject BuildFridge(FridgeArchetype archetype, float w, float d, float h, float t)
        {
            var root = new GameObject("Fridge_" + archetype);
            var bodyMat = Mat("MAT_Fridge_Body");
            var doorMat = Mat("MAT_Fridge_Door");
            var handleMat = Mat("MAT_Fridge_Handle");
            var interiorMat = Mat("MAT_Kitchen_Interior");
            float travel = M("fridge_drawer_travel", 0.35f);

            var body = Box(root.transform, "BODY_Fridge", new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d), bodyMat);
            TagReview(body, ReviewRole.StorageZone, ElementStatus.Reviewable, "fridge_depth", "冰箱储物区");

            // dark inset so an opened door/drawer reads as a cavity instead of a solid box
            void Opening(string name, float centerX, float centerY, float width, float height)
            {
                Box(root.transform, name, new Vector3(centerX, centerY, d / 2f + 0.003f),
                    new Vector3(width - 0.02f, height - 0.02f, 0.006f), interiorMat);
            }

            void Swing(string name, float bottomY, float height, float panelWidth, float centerX, bool hingeLeft)
            {
                Opening(name.Replace("DOOR_", "OPENING_"), centerX, bottomY + height / 2f, panelWidth, height);

                var pivot = new GameObject(name);
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition =
                    new Vector3(hingeLeft ? centerX - panelWidth / 2f : centerX + panelWidth / 2f, bottomY, d / 2f);

                Box(pivot.transform, "Panel",
                    new Vector3(hingeLeft ? panelWidth / 2f : -panelWidth / 2f, height / 2f, t / 2f),
                    new Vector3(panelWidth, height, t), doorMat);
                Box(pivot.transform, "Handle",
                    new Vector3(hingeLeft ? panelWidth - 0.05f : -(panelWidth - 0.05f), height / 2f, t + 0.03f),
                    new Vector3(0.03f, Mathf.Min(0.35f, height * 0.4f), 0.035f), handleMat);

                pivot.AddComponent<XRSimpleInteractable>();
                var swing = pivot.AddComponent<KinematicSwingDoor>();
                SetField(swing, "swingSign", hingeLeft ? -1f : 1f);
                TagReview(pivot, ReviewRole.FridgeDoor, ElementStatus.Reviewable, "fridge_depth", name);
            }

            void Slide(string name, float bottomY, float height, float panelWidth, float centerX)
            {
                Opening(name.Replace("DRAWER_", "OPENING_"), centerX, bottomY + height / 2f, panelWidth, height);

                var drawer = new GameObject(name);
                drawer.transform.SetParent(root.transform, false);
                drawer.transform.localPosition = new Vector3(centerX, bottomY, d / 2f);

                Box(drawer.transform, "Panel", new Vector3(0f, height / 2f, t / 2f),
                    new Vector3(panelWidth - 0.02f, height - 0.02f, t), doorMat);
                Box(drawer.transform, "Handle", new Vector3(0f, height - 0.08f, t + 0.025f),
                    new Vector3(panelWidth * 0.5f, 0.03f, 0.035f), handleMat);

                drawer.AddComponent<XRSimpleInteractable>();
                var slide = drawer.AddComponent<KinematicLinearDrawer>();
                SetField(slide, "openDistanceMetres", travel);
                TagReview(drawer, ReviewRole.FridgeDrawer, ElementStatus.Reviewable, "fridge_depth", name);
            }

            // 双门/三门冰箱的平开门统一同侧铰链（实测玩家视角：异侧会让两扇门向两边张开，
            // 不符合常见双门形态）；铰链放在靠台面一侧，门扇向房间中央摆出，远离左侧墙。
            switch (archetype)
            {
                case FridgeArchetype.F2: // 双门：上冷冻小门 + 下冷藏大门（同侧铰链）
                    Swing("DOOR_FridgeDoor_Freezer", 1.30f, 0.55f, w, 0f, false);
                    Swing("DOOR_FridgeDoor_Fresh", 0.06f, 1.24f, w, 0f, false);
                    break;
                case FridgeArchetype.F3: // 三门：上冷藏门 + 中软冻门（同侧铰链）+ 下冷冻抽屉
                    Swing("DOOR_FridgeDoor_Upper", 0.85f, 1.00f, w, 0f, false);
                    Swing("DOOR_FridgeDoor_Middle", 0.50f, 0.35f, w, 0f, false);
                    Slide("DRAWER_FridgeDrawer_Lower", 0.06f, 0.44f, w, 0f);
                    break;
                case FridgeArchetype.F4: // 四门十字：上对开两门 + 下左右两列抽屉
                    Swing("DOOR_FridgeDoor_Left", 0.90f, 0.95f, w / 2f, -w / 4f, true);
                    Swing("DOOR_FridgeDoor_Right", 0.90f, 0.95f, w / 2f, +w / 4f, false);
                    Slide("DRAWER_FridgeDrawer_Left", 0.06f, 0.84f, w / 2f, -w / 4f);
                    Slide("DRAWER_FridgeDrawer_Right", 0.06f, 0.84f, w / 2f, +w / 4f);
                    break;
            }

            return root;
        }

        // ------------------------------------------------------------------
        // Grabbable props (XRI grab sanity check for controllers)
        // ------------------------------------------------------------------

        static void PlaceProps()
        {
            float ch = M("counter_height", 0.90f);
            float roomD = M("room_depth", 3.0f);
            float backZ = -roomD / 2f;

            var props = new GameObject("PROPS_Counter");
            var pot = EnsurePropPrefab("PREF_Prop_Pot", BuildPot);
            var cup = EnsurePropPrefab("PREF_Prop_Cup", BuildCup);
            InstantiateAt(pot, props.transform, new Vector3(0.55f, ch + 0.06f, backZ + 0.35f));
            InstantiateAt(cup, props.transform, new Vector3(0.85f, ch + 0.05f, backZ + 0.48f));
        }

        static GameObject EnsurePropPrefab(string prefabName, System.Func<GameObject> build)
        {
            var path = $"{PrefabRoot}/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;

            var go = build();
            prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"{Marker} created prefab {path}");
            return prefab;
        }

        static GameObject BuildPot()
        {
            var root = new GameObject("Prop_Pot");
            Cyl(root.transform, "BODY_Pot", Vector3.zero, new Vector3(0.18f, 0.06f, 0.18f), Mat("MAT_Prop_Pot"));
            foreach (var side in new[] { -1f, 1f })
                Box(root.transform, "HANDLE_Pot", new Vector3(side * 0.115f, 0.02f, 0f),
                    new Vector3(0.10f, 0.016f, 0.016f), Mat("MAT_Kitchen_Metal"));

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<XRGrabInteractable>();
            return root;
        }

        static GameObject BuildCup()
        {
            var root = new GameObject("Prop_Cup");
            Cyl(root.transform, "BODY_Cup", Vector3.zero, new Vector3(0.08f, 0.05f, 0.08f), Mat("MAT_Prop_Cup"));

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<XRGrabInteractable>();
            return root;
        }

        // ------------------------------------------------------------------
        // World-space menu panel (回到起点 / 暂停 / 平滑移动开关)
        // ------------------------------------------------------------------

        static void BuildMenuPanel()
        {
            var canvasGo = new GameObject("UI_MenuPanel", typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 300f);
            canvasGo.transform.localScale = Vector3.one * 0.0015f;
            canvasGo.transform.SetPositionAndRotation(new Vector3(0.45f, 1.35f, 0.05f), Quaternion.identity);
            canvasGo.transform.LookAt(new Vector3(0f, 1.5f, 0.9f)); // face the player start
            canvasGo.transform.Rotate(0f, 180f, 0f); // canvas front is local -Z; flip after LookAt
            canvasGo.AddComponent<KitchenMenuController>();

            var bg = AddUi(canvasGo.transform, "BG", typeof(Image));
            Stretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.88f);

            var title = AddUi(canvasGo.transform, "Title", typeof(Text));
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(400f, 48f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);
            var titleText = title.GetComponent<Text>();
            titleText.text = "厨房评审 · 菜单";
            titleText.font = BuiltinFont();
            titleText.fontSize = 30;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.90f, 0.92f, 0.95f);

            MakeButton(canvasGo.transform, "BTN_Reset", "回到起点", new Vector2(0f, -88f));
            MakeButton(canvasGo.transform, "BTN_Pause", "暂停", new Vector2(0f, -150f));
            MakeButton(canvasGo.transform, "BTN_Locomotion", "平滑移动：关", new Vector2(0f, -212f));
        }

        static void MakeButton(Transform canvas, string name, string label, Vector2 anchoredPosition)
        {
            var button = AddUi(canvas, name, typeof(Image), typeof(Button));
            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(340f, 52f);
            buttonRect.anchoredPosition = anchoredPosition;
            button.GetComponent<Image>().color = new Color(0.20f, 0.33f, 0.50f);

            var labelGo = AddUi(button.transform, "Label", typeof(Text));
            Stretch(labelGo.GetComponent<RectTransform>());
            var text = labelGo.GetComponent<Text>();
            text.text = label;
            text.font = BuiltinFont();
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        static GameObject AddUi(Transform parent, string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Font BuiltinFont()
        {
            // 2022 的内置动态字体，正文经 OS 字体回退可显示中文
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ------------------------------------------------------------------
        // Small authoring helpers
        // ------------------------------------------------------------------

        static void TagReview(GameObject go, ReviewRole role, ElementStatus status, string assumptionKey, string label)
        {
            var element = go.AddComponent<ReviewElement>();
            var so = new SerializedObject(element);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.FindProperty("status").enumValueIndex = (int)status;
            so.FindProperty("assumptionKey").stringValue = assumptionKey ?? string.Empty;
            so.FindProperty("reviewerLabel").stringValue = label ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetField(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureMaterials()
        {
            Mat("MAT_Kitchen_Floor", new Color(0.55f, 0.52f, 0.48f), 0.05f, 0.35f);
            Mat("MAT_Kitchen_Wall", new Color(0.85f, 0.84f, 0.80f), 0f, 0.4f);
            Mat("MAT_Kitchen_Cabinet", new Color(0.72f, 0.58f, 0.42f), 0f, 0.35f);
            Mat("MAT_Kitchen_CounterTop", new Color(0.25f, 0.26f, 0.28f), 0.1f, 0.6f);
            Mat("MAT_Kitchen_ToeKick", new Color(0.20f, 0.20f, 0.20f), 0f, 0.3f);
            Mat("MAT_Kitchen_Interior", new Color(0.05f, 0.05f, 0.06f), 0f, 0.2f);
            Mat("MAT_Kitchen_Metal", new Color(0.75f, 0.76f, 0.78f), 0.9f, 0.7f);
            Mat("MAT_Fridge_Body", new Color(0.78f, 0.79f, 0.81f), 0.6f, 0.5f);
            Mat("MAT_Fridge_Door", new Color(0.83f, 0.84f, 0.86f), 0.6f, 0.55f);
            Mat("MAT_Fridge_Handle", new Color(0.85f, 0.86f, 0.88f), 1.0f, 0.8f);
            Mat("MAT_Window_Glow", new Color(1f, 1f, 0.95f), 0f, 0f, 1.15f);
            Mat("MAT_Zone_Circulation", new Color(0.35f, 0.55f, 0.35f), 0f, 0.15f);
            Mat("MAT_Prop_Pot", new Color(0.16f, 0.25f, 0.45f), 0.3f, 0.5f);
            Mat("MAT_Prop_Cup", new Color(0.90f, 0.88f, 0.85f), 0f, 0.5f);
        }

        static Material Mat(string name)
        {
            var path = $"{MaterialRoot}/{name}.asset";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material Mat(string name, Color color, float metallic, float smoothness, float emission = 0f)
        {
            var mat = Mat(name);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
            }
            return mat;
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Cyl(Transform parent, string name, Vector3 center, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Spawn(string prefabPath, Vector3 position, float yaw = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"{Marker} prefab missing: {prefabPath}");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return instance;
        }

        static GameObject InstantiateAt(GameObject prefab, Transform parent, Vector3 localPosition)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            return instance;
        }
    }
}
