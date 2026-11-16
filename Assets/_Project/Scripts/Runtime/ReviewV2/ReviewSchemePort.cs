using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRApartment.Data.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    [DefaultExecutionOrder(-200)]
    public sealed class ReviewSchemePort : MonoBehaviour, ISchemePort, ISchemeModelCatalog
    {
        public ReviewSessionHost host;
        public ReviewSchemeWorld[] layouts = Array.Empty<ReviewSchemeWorld>();
        public string initialSchemeId = SchemeIds.IntegratedKitchen;
        ReviewSchemeWorld current;
        bool changing;
        public ReviewSchemeWorld Current => current;
        ReviewSchemeWorld Find(string id) => layouts.SingleOrDefault(w => w != null && w.schemeId == id);
        public bool Supports(string id) => layouts.Count(w => w != null && w.schemeId == id) == 1 && Find(id).IsConfigured;
        public string ModelVersionFor(string id) => Supports(id) ? Find(id).modelVersion : null;
        void Awake()
        {
            if (host == null || !Supports(initialSchemeId)) throw new InvalidOperationException("Initial authored scheme is unavailable");
            current = Stage(Find(initialSchemeId));
            current.Bind(host); host.modelVersion = current.modelVersion; host.initialSchemeId = current.schemeId;
            host.schemePort = this; current.gameObject.SetActive(true);
        }
        ReviewSchemeWorld Stage(ReviewSchemeWorld prefab)
        {
            if (prefab.gameObject.activeSelf) throw new InvalidOperationException("Scheme prefab must be saved inactive");
            var instance = Instantiate(prefab);
            var scene = SceneManager.CreateScene("ReviewV2-" + prefab.schemeId + "-" + Guid.NewGuid().ToString("N"));
            SceneManager.MoveGameObjectToScene(instance.gameObject, scene);
            return instance;
        }
        static void Release(ReviewSchemeWorld world)
        {
            if (world == null) return;
            world.gameObject.SetActive(false);
            SceneManager.UnloadSceneAsync(world.gameObject.scene);
        }
        public void Change(string id, string schemeId, Action<string, bool, string> completed)
        {
            if (changing || !Supports(schemeId) || current == null)
            { completed(id, false, "SchemeUnavailable"); return; }
            changing = true;
            ReviewSchemeWorld next = null;
            var previous = current;
            string error = null;
            try
            {
                next = Stage(Find(schemeId));
                if (!next.IsConfigured) throw new InvalidOperationException("Authored world references are invalid");
                // Inspect the staged geometry synchronously, without advancing physics or
                // disabling the old world; rejection must also preserve existing held objects.
                next.Bind(host); next.gameObject.SetActive(true); Physics.SyncTransforms();
                var obstruction = DestinationObstruction(next);
                if (obstruction != null) throw new InvalidOperationException(obstruction);
                previous.gameObject.SetActive(false);
                current = next;
                host.WorldRebound();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Release(next); previous.Bind(host); previous.gameObject.SetActive(true); current = previous;
                host.WorldRebound(false);
            }
            changing = false;
            if (error == null) Release(previous);
            completed(id, error == null, error ?? "Target design loaded from its initial world; feedback retains its original visit");
        }
        string DestinationObstruction(ReviewSchemeWorld world)
        {
            var camera = host.locomotion == null ? null : host.locomotion.Origin.Camera;
            if (camera == null) return null;
            var point = camera.transform.position;
            // Query the live destination collider shapes after SyncTransforms. Restrict the
            // result to its isolated scene while the previous world remains available for rollback.
            foreach (var collider in Physics.OverlapSphere(point, .18f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider.gameObject.scene == world.gameObject.scene && collider.attachedRigidbody == null)
                    return $"Move away from destination furniture: {collider.name}";
            }
            return null;
        }
        void OnDestroy() => Release(current);
    }
}
