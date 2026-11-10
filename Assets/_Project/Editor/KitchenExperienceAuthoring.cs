using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRApartment.Runtime;
using Object = UnityEngine.Object;

namespace XRApartment.EditorTools
{
    public static partial class KitchenCookingAuthoring
    {
        const string RoomEnvelope = "COL_KitchenEnvelope";

        [MenuItem("XR Apartment/Cooking/Apply Room Containment")]
        public static void ApplyExperienceMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyExperienceBatch();
        }

        public static void ApplyExperienceBatch()
        {
            KitchenAuthoring.RequireEditMode();
            ConfigureSharedExperience();
            AssetDatabase.SaveAssets();
        }

        static void ConfigureSharedExperience()
        {
            KitchenAuthoring.EditPrefab(KitchenAuthoring.Prefabs + "PREF_Kitchen_Environment.prefab", ConfigureRoomEnvelope);
        }

        static void ConfigureRoomEnvelope(GameObject root)
        {
            var floor = root.GetComponentsInChildren<MeshCollider>(true).Single(c => c.name == "SM_Ground");
            var old = root.transform.Find(RoomEnvelope);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var envelope = Child(root.transform, RoomEnvelope);
            var bounds = floor.bounds;
            const float skin = .08f;
            const float height = 3.2f;
            var mid = bounds.center;
            mid.y = bounds.max.y + height * .5f;
            Add("Underfloor", new Vector3(mid.x, bounds.max.y - .065f, mid.z),
                new Vector3(bounds.size.x, .12f, bounds.size.z));
            Add("West", new Vector3(bounds.min.x - skin * .5f, mid.y, mid.z),
                new Vector3(skin, height, bounds.size.z + skin * 2f));
            Add("East", new Vector3(bounds.max.x + skin * .5f, mid.y, mid.z),
                new Vector3(skin, height, bounds.size.z + skin * 2f));
            Add("South", new Vector3(mid.x, mid.y, bounds.min.z - skin * .5f),
                new Vector3(bounds.size.x, height, skin));
            Add("North", new Vector3(mid.x, mid.y, bounds.max.z + skin * .5f),
                new Vector3(bounds.size.x, height, skin));
            envelope.gameObject.AddComponent<KitchenRoomBounds>().Configure(floor);
            Parameter("kitchen_virtual_edge_height", height);
            Parameter("kitchen_virtual_edge_thickness", skin);

            void Add(string name, Vector3 centre, Vector3 size)
            {
                var wall = Child(envelope, name);
                wall.SetPositionAndRotation(centre, Quaternion.identity);
                wall.gameObject.AddComponent<BoxCollider>().size = size;
            }
        }
    }
}
