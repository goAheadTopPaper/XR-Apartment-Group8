using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRApartment.Runtime.ReviewV2;

namespace XRApartment.EditorTools
{
    public static class ReviewV2SchemeInspection
    {
        // Read-only scene inspection; never saves scene or shared resources.
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ReviewV2Authoring.ScenePath);
            var roots = scene.GetRootGameObjects();
            var lines = roots.Select(r => $"ROOT {r.name} position={r.transform.position:F3} active={r.activeSelf}").ToList();
            foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)))
                if (c.enabled && !c.isTrigger && c.attachedRigidbody == null)
                    lines.Add($"STATIC {c.name} centre={c.bounds.center:F3} size={c.bounds.size:F3} path={PathOf(c.transform)}");
            var host = roots.SelectMany(r => r.GetComponentsInChildren<ReviewSessionHost>(true)).Single();
            lines.Add($"HOST version={host.modelVersion} station={host.station.name} fridge={host.station.ActiveFridge.name}");
            Directory.CreateDirectory("Logs/ReviewV2/M2"); File.WriteAllLines("Logs/ReviewV2/M2/scene-inspection.txt", lines);
            Debug.Log("REVIEW_V2_M2_INSPECTION_COMPLETE scene not saved");
        }
        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }
}
