using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Runtime;
using XRApartment.Runtime.UI;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string RadioName = "PREF_Cooking_Radio";
        const string RadioPrefab = Prefabs + RadioName + ".prefab";
        const string RadioMusic = Art + "MUS_Radio_FunkyHouse.mp3";

        public static void ApplyRoomComfortBatch()
        {
            KitchenAuthoring.RequireEditMode();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureCookingAmenities(scene);
            ConfigureComfortOptions(CookingMovement(scene).GetComponentInChildren<LocomotionSettingsView>(true));
            KitchenAuthoring.SaveScene(scene);
            Debug.Log("KITCHEN_COMFORT_READY " + scene.path);
        }

        static KitchenLocomotionSettings CookingMovement(Scene scene) => scene.GetRootGameObjects()
            .Select(root => root.GetComponent<KitchenLocomotionSettings>()).Single(settings => settings != null);

        static void ConfigureCookingAmenities(Scene scene)
        {
            var movement = CookingMovement(scene);
            var origin = movement.GetComponent<XROrigin>();
            origin.CameraYOffset = KitchenLocomotionSettings.ComfortEyeHeight;
            var tracking = origin.CameraFloorOffsetObject.transform;
            tracking.localPosition = new Vector3(tracking.localPosition.x, origin.CameraYOffset, tracking.localPosition.z);
            var body = origin.Origin.GetComponent<CharacterController>();
            body.height = origin.CameraYOffset;
            body.center = new Vector3(body.center.x, body.height * .5f + body.skinWidth, body.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(origin);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tracking);
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);

            ConfigureCookingWindows(scene, movement);

            var prefab = BuildRadio();
            var radio = scene.GetRootGameObjects().SingleOrDefault(root => root.GetComponent<KitchenRadio>() != null);
            if (radio == null) radio = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var table = scene.GetRootGameObjects().Single(root => root.name == "PREF_Furniture_DiningSet").transform;
            radio.transform.SetPositionAndRotation(table.TransformPoint(-.13f, .758f, -.22f), table.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(radio.transform);
        }

        static void ConfigureComfortOptions(LocomotionSettingsView menu)
        {
            var panel = (RectTransform)menu.transform.Find("Panel");
            panel.sizeDelta = new Vector2(640, 640);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            Place("Title", 271);
            Place("Current", 219);
            Place("Teleport", 155);
            Place("Smooth", 155);
            Place("Instructions", 63);
            Place("EnterReview", -37);
            Place("Close", -37);
            Place("Language", -109);
            Place("Shortcut", -267);
            var standing = HeightButton("StandingHeight", -148, "Align standing height");
            var seated = HeightButton("SeatedHeight", 148, "Align seated height");
            Wire(menu, ("standingHeight", standing), ("seatedHeight", seated));

            void Place(string name, float y)
            {
                var rect = (RectTransform)panel.Find(name);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }

            UnityEngine.UI.Button HeightButton(string name, float x, string label)
            {
                var present = panel.Find(name);
                if (present != null) return present.GetComponent<UnityEngine.UI.Button>();
                return KitchenLocomotionAuthoring.Button(name, panel, x, -183, 274, 58, label);
            }
        }

        static GameObject BuildRadio()
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(RadioPrefab);
            var track = original == null ? null : original.GetComponent<AudioSource>().clip;
            var root = new GameObject(RadioName, typeof(AudioSource), typeof(KitchenRadio));
            try
            {
                var speaker = root.GetComponent<AudioSource>();
                speaker.clip = track != null ? track : KitchenAuthoring.LoadRequired<AudioClip>(RadioMusic);
                speaker.loop = true;
                speaker.playOnAwake = false;
                speaker.volume = KitchenRadio.DefaultVolume;
                speaker.spatialBlend = 1f;
                speaker.minDistance = 1.5f;
                speaker.maxDistance = 8f;
                speaker.rolloffMode = AudioRolloffMode.Linear;
                speaker.dopplerLevel = 0f;

                var walnut = Material("RadioWalnut", new Color(.24f, .119f, .059f), 0f, .39f);
                var cream = Material("RadioEnamel", new Color(.78f, .69f, .52f), .07f, .42f);
                var dark = Material("RadioGrille", new Color(.048f, .043f, .032f), 0f, .23f);
                var gold = Material("RadioBrass", new Color(.63f, .42f, .22f), .5f, .58f);
                var body = Cube(root.transform, "WalnutCase", new Vector3(0f, .076f, 0f), new Vector3(.318f, .144f, .137f), walnut);
                var unitCube = body.GetComponent<MeshFilter>().sharedMesh;
                Block(root.transform, "Faceplate", new Vector3(0f, .076f, .07f), new Vector3(.302f, .127f, .006f), cream);
                Block(root.transform, "SpeakerCloth", new Vector3(-.069f, .077f, .074f), new Vector3(.151f, .105f, .003f), dark);
                Block(root.transform, "TunerWindow", new Vector3(.080f, .113f, .075f), new Vector3(.128f, .023f, .005f), dark);
                BuildRadioGrille(root.transform, unitCube, gold);
                Block(root.transform, "TuningNeedle", new Vector3(.081f, .113f, .078f), new Vector3(.002f, .016f, .001f), gold);
                Block(root.transform, "TuningScale", new Vector3(.080f, .121f, .078f), new Vector3(.112f, .0015f, .001f), cream);
                Block(root.transform, "LeftFoot", new Vector3(-.115f, .003f, 0f), new Vector3(.034f, .006f, .096f), dark);
                Block(root.transform, "RightFoot", new Vector3(.115f, .003f, 0f), new Vector3(.034f, .006f, .096f), dark);

                var power = Control("Power", .035f, gold);
                Block(power.transform, "PowerMark", new Vector3(0f, .003f, .011f), new Vector3(.003f, .015f, .0012f), dark);
                var down = Control("VolumeDown", .083f, dark);
                var up = Control("VolumeUp", .128f, dark);
                foreach (var control in new[] { down, up })
                    Block(control.transform, "MinusMark", new Vector3(0f, 0f, .011f), new Vector3(.014f, .0024f, .0012f), cream);
                Block(up.transform, "PlusMark", new Vector3(0f, 0f, .011f), new Vector3(.0024f, .014f, .0012f), cream);
                Block(root.transform, "LampBezel", new Vector3(.137f, .084f, .074f), new Vector3(.012f, .012f, .004f), dark);
                var amber = Material("RadioLamp", new Color(.95f, .35f, .045f), 0f, .48f);
                amber.EnableKeyword("_EMISSIVE_COLOR");
                amber.SetColor("_EmissiveColor", new Color(1.8f, .48f, .04f));
                KitchenHdrpMaterials.Validate(amber);
                var light = Block(root.transform, "PowerLight", new Vector3(.137f, .084f, .078f), new Vector3(.008f, .008f, .004f), amber);
                light.enabled = false;
                root.GetComponent<KitchenRadio>().Configure(power, down, up, light);
                return PrefabUtility.SaveAsPrefabAsset(root, RadioPrefab);

                XRSimpleInteractable Control(string name, float x, Material material)
                {
                    var host = Child(root.transform, name);
                    host.localPosition = new Vector3(x, .048f, .081f);
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    visual.name = "Knob";
                    visual.transform.SetParent(host, false);
                    visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    visual.transform.localScale = new Vector3(.034f, .010f, .034f);
                    visual.GetComponent<Renderer>().sharedMaterial = material;
                    Object.DestroyImmediate(visual.GetComponent<Collider>());
                    var collider = host.gameObject.AddComponent<BoxCollider>();
                    collider.center = Vector3.forward * .005f;
                    collider.size = new Vector3(.042f, .049f, .024f);
                    var button = host.gameObject.AddComponent<XRSimpleInteractable>();
                    button.colliders.Add(collider);
                    return button;
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        static MeshRenderer Block(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var part = Cube(parent, name, centre, size, material);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        static void BuildRadioGrille(Transform parent, Mesh cube, Material material)
        {
            var slats = new CombineInstance[7];
            for (var i = 0; i < slats.Length; i++)
            {
                slats[i].mesh = cube;
                slats[i].transform = Matrix4x4.TRS(new Vector3(-.069f, .043f + .011f * i, .077f),
                    Quaternion.identity, new Vector3(.13f, .0018f, .002f));
            }
            var mesh = new Mesh { name = "MESH_Cooking_RadioGrille" };
            mesh.CombineMeshes(slats);
            var grille = Child(parent, "SpeakerGrille").gameObject;
            grille.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh.name, mesh);
            var renderer = grille.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
