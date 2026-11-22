using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRApartment.Simulation.ReviewV2;

namespace XRApartment.Runtime.ReviewV2
{
    public sealed class ReviewGroceryActivity : MonoBehaviour
    {
        public ReviewSessionHost host;
        public ReviewSceneIdentity zoneIdentity;
        public BoxCollider storageZone;
        public XRGrabInteractable[] groceries = Array.Empty<XRGrabInteractable>();
        public int requiredCount = 2;
        public float stableSeconds = .5f, maximumSpeed = .15f;
        public float maximumDrift = .02f;
        GroceryStorageProgress progress;
        float[] stable;
        Vector3[] stablePositions;
        bool[] released;
        string visit;
        public bool IsConfigured => storageZone != null && zoneIdentity != null && groceries.Length >= requiredCount &&
            requiredCount >= 2 && stableSeconds > 0 && maximumSpeed > 0 && maximumDrift > 0 && groceries.All(g => g != null &&
                g.GetComponent<ReviewSceneIdentity>() != null) &&
            groceries.Select(g => g.GetComponent<ReviewSceneIdentity>().Id).Distinct().Count() == groceries.Length;
        void Start()
        {
            if (!IsConfigured) { enabled = false; return; }
            progress = new GroceryStorageProgress(groceries.Select(g => g.GetComponent<ReviewSceneIdentity>().Id), requiredCount);
            stable = new float[groceries.Length]; released = new bool[groceries.Length];
            stablePositions = new Vector3[groceries.Length];
            foreach (var grab in groceries) grab.lastSelectExited.AddListener(Released);
            visit = host.Snapshot.SchemeVisitId;
        }
        void OnDestroy()
        { foreach (var grab in groceries) if (grab != null) grab.lastSelectExited.RemoveListener(Released); }
        void Released(SelectExitEventArgs args)
        {
            if (args.isCanceled || !host.CanObserve || !host.station.ActiveFridge.Accessible) return;
            int i = Array.IndexOf(groceries, args.interactableObject as XRGrabInteractable);
            if (i >= 0) { released[i] = true; stable[i] = 0; }
        }
        public bool Contains(XRGrabInteractable grab)
        {
            bool found = false;
            var bounds = new Bounds(storageZone.center, storageZone.size);
            foreach (var col in grab.colliders)
            {
                if (col == null || !col.enabled || col.isTrigger || !col.gameObject.activeInHierarchy) continue;
                found = true;
                var b = col.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                        (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    if (!bounds.Contains(storageZone.transform.InverseTransformPoint(corner))) return false;
                }
            }
            return found;
        }
        void Update()
        {
            if (progress == null) return;
            if (visit != host.Snapshot.SchemeVisitId)
            { visit = host.Snapshot.SchemeVisitId; progress.Reset(); Array.Clear(stable, 0, stable.Length); Array.Clear(released, 0, released.Length); }
            if (!host.CanObserve) { Array.Clear(stable, 0, stable.Length); return; }
            for (int i = 0; i < groceries.Length; i++)
            {
                var grab = groceries[i];
                if (grab.isSelected) { released[i] = false; stable[i] = 0; continue; }
                var body = grab.GetComponent<Rigidbody>();
                if (!released[i] || !grab.gameObject.activeInHierarchy || !Contains(grab) ||
                    (body != null && body.velocity.magnitude > maximumSpeed))
                { stable[i] = 0; continue; }
                var position = body != null ? body.worldCenterOfMass : grab.transform.position;
                if (stable[i] <= 0) stablePositions[i] = position;
                if (Vector3.Distance(stablePositions[i], position) > maximumDrift)
                { stable[i] = 0; continue; }
                stable[i] += Time.deltaTime;
                if (stable[i] < stableSeconds) continue;
                released[i] = false;
                string id = grab.GetComponent<ReviewSceneIdentity>().Id;
                if (progress.Store(id)) host.ObserveGroceryStored(id, zoneIdentity.Id, progress.State);
            }
        }
    }
}
