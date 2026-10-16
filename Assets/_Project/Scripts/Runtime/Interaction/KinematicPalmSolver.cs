using UnityEngine;

namespace XRApartment.Runtime
{
    /// Sweeps a small visual palm proxy against static geometry. It never applies forces,
    /// moves an XR device, or changes the interaction target or a reach measurement.
    public sealed class KinematicPalmSolver
    {
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        readonly SphereCollider probe;
        readonly Transform ownerRoot;
        readonly int layers;

        public bool QuerySaturated { get; private set; }

        public KinematicPalmSolver(SphereCollider probe, Transform ownerRoot, int layers)
        {
            this.probe = probe;
            this.ownerRoot = ownerRoot;
            this.layers = layers;
        }

        public Vector3 Resolve(Vector3 from, Vector3 target, float radius, float skin, Transform held = null)
        {
            probe.radius = radius;
            // Unity 2022's PhysX backend returns no penetration for a disabled probe.
            // Enable only for this synchronous query, then remove it before simulation
            // or XRI processing can observe a physical hand collider.
            probe.enabled = true;
            try
            {
                return ResolveEnabled(from, target, radius, skin, held);
            }
            finally
            {
                probe.enabled = false;
            }
        }

        Vector3 ResolveEnabled(Vector3 from, Vector3 target, float radius, float skin, Transform held)
        {
            QuerySaturated = false;
            var position = Depenetrate(from, radius, skin, held);
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var delta = target - position;
                var distance = delta.magnitude;
                if (distance < 0.00001f) break;
                var direction = delta / distance;
                var count = Physics.SphereCastNonAlloc(position, radius, direction, hits,
                    distance + skin, layers, QueryTriggerInteraction.Ignore);
                if (count == hits.Length)
                {
                    QuerySaturated = true;
                    break;
                }
                var nearest = -1;
                var nearestDistance = float.PositiveInfinity;
                for (var i = 0; i < count; i++)
                {
                    if (!Blocks(hits[i].collider, held) || hits[i].distance >= nearestDistance) continue;
                    nearest = i;
                    nearestDistance = hits[i].distance;
                }
                if (nearest < 0)
                {
                    position = target;
                    break;
                }
                position += direction * Mathf.Max(0f, nearestDistance - skin);
                target = position + Vector3.ProjectOnPlane(target - position, hits[nearest].normal);
            }
            return Depenetrate(position, radius, skin, held);
        }

        Vector3 Depenetrate(Vector3 position, float radius, float skin, Transform held)
        {
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var count = Physics.OverlapSphereNonAlloc(position, radius, overlaps, layers, QueryTriggerInteraction.Ignore);
                if (count == overlaps.Length) QuerySaturated = true;
                var moved = false;
                for (var i = 0; i < count; i++)
                {
                    var obstacle = overlaps[i];
                    if (!Blocks(obstacle, held)) continue;
                    if (!Physics.ComputePenetration(probe, position, Quaternion.identity,
                            obstacle, obstacle.transform.position, obstacle.transform.rotation,
                            out var normal, out var depth)) continue;
                    position += normal * (depth + skin);
                    moved = true;
                }
                if (!moved) break;
            }
            return position;
        }

        bool Blocks(Collider obstacle, Transform held)
        {
            if (obstacle == null || obstacle == probe || obstacle.attachedRigidbody != null) return false;
            if (ownerRoot != null && obstacle.transform.IsChildOf(ownerRoot)) return false;
            return held == null || !obstacle.transform.IsChildOf(held);
        }
    }
}
