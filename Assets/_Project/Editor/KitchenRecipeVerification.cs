using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRApartment.Runtime;

namespace XRApartment.EditorTools
{
    public static class KitchenRecipeVerification
    {
        public static void InspectBatch()
        {
            EditorSceneManager.OpenScene(KitchenCookingAuthoring.ScenePath);
            var fridge = UnityEngine.Object.FindObjectOfType<KitchenFridge>();
            var environment = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Single(go => go.name == "PREF_Kitchen_Environment");
            foreach (var collider in environment.GetComponentsInChildren<Collider>())
                if (collider.name.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0)
                    Debug.Log("RECIPE_TABLE_BOUNDS " + collider.name + " " + collider.bounds);
            foreach (var z in new[] { -1.9f, -1.75f, -1.65f, -1.5f, -1.3f, -1.1f, -.9f, -.7f })
                foreach (var x in new[] { -3.65f, -3.45f, -3f, -2.55f, -2.07f, -1.77f, -1.5f })
                {
                    var hits = Physics.RaycastAll(new Vector3(x, 2.3f, z), Vector3.down, 3f);
                    var support = hits.Where(hit => hit.collider.transform.IsChildOf(environment.transform) && hit.normal.y > .5f)
                        .OrderBy(hit => hit.distance).FirstOrDefault();
                    Debug.Log("RECIPE_SUPPORT x=" + x + " z=" + z + " y=" + support.point.y + " collider=" + (support.collider == null ? "none" : support.collider.name));
                }
            foreach (var source in new[] { "fridge_body", "fridge_door", "spatula", "egg_curds" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Cooking/RecipeModels/" + source + ".fbx");
                Debug.Log("RECIPE_MODEL_AXIS " + source + " root=" + model.transform.localEulerAngles + " scale=" + model.transform.localScale);
                foreach (var mesh in model.GetComponentsInChildren<MeshFilter>(true).Take(3))
                    Debug.Log("RECIPE_MODEL_PART " + mesh.name + " local=" + mesh.transform.localPosition + " bounds=" + mesh.sharedMesh.bounds);
            }
            var door = fridge.Door; var probe = door.transform.Find("COL_DoorSweepProbe").GetComponent<BoxCollider>();
            Physics.SyncTransforms(); probe.enabled = true;
            try
            {
                foreach (var obstacle in Physics.OverlapBox(probe.transform.TransformPoint(probe.center), probe.size * .5f,
                             probe.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                    if (!obstacle.transform.IsChildOf(door.transform) && Physics.ComputePenetration(probe, probe.transform.position,
                            probe.transform.rotation, obstacle, obstacle.transform.position, obstacle.transform.rotation, out var normal, out var depth))
                        Debug.Log("RECIPE_DOOR_OVERLAP " + obstacle.name + " depth=" + depth + " normal=" + normal + " bounds=" + obstacle.bounds);
            }
            finally { probe.enabled = false; }
            foreach (var renderer in fridge.GetComponentsInChildren<MeshRenderer>())
                Debug.Log("RECIPE_FRIDGE_RENDERER " + renderer.name + " world bounds=" + renderer.bounds);
        }
    }
}
