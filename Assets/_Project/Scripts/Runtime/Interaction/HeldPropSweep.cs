using UnityEngine;

namespace XRApartment.Runtime
{
    /// A conservative oriented box enclosing the complete held prop, including handles.
    /// Keeps translation/rotation on the reachable side of geometry without adding forces.
    public sealed class HeldPropSweep
    {
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[64];
        readonly BoxCollider probe;
        readonly Transform prop;
        readonly int layers;
        readonly Vector3 centre;
        readonly Vector3 extents;
        readonly Vector3 reach;
        readonly Vector3 probeSize;
        readonly Vector3 inverseScale;
        Transform rig;

        public bool QuerySaturated { get; private set; }

        public HeldPropSweep(BoxCollider probe, Transform prop, int layers)
        {
            this.probe = probe;
            this.prop = prop;
            this.layers = layers;
            var scale = prop.lossyScale;
            probeSize = probe.size;
            inverseScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
            centre = Vector3.Scale(probe.center, scale);
            extents = Vector3.Scale(probe.size * .5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            reach = new Vector3(Mathf.Abs(centre.x), Mathf.Abs(centre.y), Mathf.Abs(centre.z)) + extents;
        }

        public Pose Resolve(Pose from, Pose requested, Transform ownerRig, float skin,
            float rotationStepDegrees, int rotationIterations)
        {
            QuerySaturated = false;
            rig = ownerRig;
            // ComputePenetration in Unity 2022 requires an enabled query collider.
            // It is disabled again before XRI or the physics simulation can observe it.
            probe.size = probeSize + Vector3.Scale(inverseScale, Vector3.one * (skin * 2f));
            probe.enabled = true;
            try
            {
                var safe = RecoverOverlap(from, skin);
                if (QuerySaturated) return from;
                safe.rotation = Rotate(safe, requested.rotation, rotationStepDegrees, rotationIterations);
                safe.position = Slide(safe, requested.position, skin);
                return safe;
            }
            finally
            {
                probe.enabled = false;
                probe.size = probeSize;
            }
        }

        Pose RecoverOverlap(Pose pose, float skin)
        {
            for (var iteration = 0; iteration < 4; iteration++)
            {
                var count = Overlap(pose, Vector3.one * skin);
                if (QuerySaturated) return pose;
                var moved = false;
                for (var i = 0; i < count; i++)
                {
                    var obstacle = overlaps[i];
                    if (!Blocks(obstacle)) continue;
                    if (!Physics.ComputePenetration(probe, pose.position, pose.rotation,
                            obstacle, obstacle.transform.position, obstacle.transform.rotation,
                            out var normal, out var depth)) continue;
                    pose.position += normal * (depth + skin);
                    moved = true;
                }
                if (!moved) break;
            }
            return pose;
        }

        Quaternion Rotate(Pose pose, Quaternion requested, float stepDegrees, int iterations)
        {
            for (var step = 0; step < iterations; step++)
            {
                var angle = Quaternion.Angle(pose.rotation, requested);
                if (angle < .001f) break;
                var next = Quaternion.RotateTowards(pose.rotation, requested, stepDegrees);
                var middle = new Pose(pose.position, Quaternion.Slerp(pose.rotation, next, .5f));
                // Bound the whole angular arc separately on each local axis. In
                // particular, yaw has no vertical arc: a resting pot can still turn.
                var relative = Quaternion.Inverse(pose.rotation) * next;
                relative.ToAngleAxis(out _, out var axis);
                var margin = AngularMargin(axis, Mathf.Min(angle, stepDegrees) * Mathf.Deg2Rad * .5f);
                if (Occupied(middle, margin)) break;
                pose.rotation = next;
            }
            return pose.rotation;
        }

        Vector3 AngularMargin(Vector3 axis, float halfAngle)
        {
            // Rodrigues' rotation formula bounds each corner's displacement from
            // the midpoint. The second-order term also includes an off-centre pivot.
            axis = axis.normalized;
            var a = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            var cross = new Vector3(a.y * reach.z + a.z * reach.y,
                a.z * reach.x + a.x * reach.z, a.x * reach.y + a.y * reach.x);
            var bend = new Vector3(
                (1f - a.x * a.x) * reach.x + a.x * a.y * reach.y + a.x * a.z * reach.z,
                (1f - a.y * a.y) * reach.y + a.y * a.x * reach.x + a.y * a.z * reach.z,
                (1f - a.z * a.z) * reach.z + a.z * a.x * reach.x + a.z * a.y * reach.y);
            return cross * Mathf.Sin(halfAngle) + bend * (1f - Mathf.Cos(halfAngle));
        }

        Vector3 Slide(Pose pose, Vector3 requested, float skin)
        {
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var delta = requested - pose.position;
                var distance = delta.magnitude;
                if (distance < .00001f) break;
                var direction = delta / distance;
                var count = Physics.BoxCastNonAlloc(pose.position + pose.rotation * centre, extents,
                    direction, hits, pose.rotation, distance + skin, layers, QueryTriggerInteraction.Ignore);
                if (count == hits.Length)
                {
                    QuerySaturated = true;
                    break; // An incomplete query must never allow the prop through a surface.
                }
                var nearest = -1;
                var nearestDistance = float.PositiveInfinity;
                for (var i = 0; i < count; i++)
                {
                    if (!Blocks(hits[i].collider) || hits[i].distance >= nearestDistance) continue;
                    // Casts that start touching a surface can report a zero-distance hit.
                    // Moving out of or along that surface must still be possible.
                    if (hits[i].distance < .00001f && Vector3.Dot(direction, hits[i].normal) >= -.001f) continue;
                    nearest = i;
                    nearestDistance = hits[i].distance;
                }
                if (nearest < 0)
                {
                    pose.position = requested;
                    break;
                }
                pose.position += direction * Mathf.Max(0f, nearestDistance - skin);
                requested = pose.position + Vector3.ProjectOnPlane(requested - pose.position, hits[nearest].normal);
            }
            return pose.position;
        }

        int Overlap(Pose pose, Vector3 margin)
        {
            var count = Physics.OverlapBoxNonAlloc(pose.position + pose.rotation * centre,
                extents + margin, overlaps, pose.rotation, layers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) QuerySaturated = true;
            return count;
        }

        bool Occupied(Pose pose, Vector3 margin)
        {
            var count = Overlap(pose, margin);
            if (QuerySaturated) return true;
            for (var i = 0; i < count; i++)
                if (Blocks(overlaps[i])) return true;
            return false;
        }

        bool Blocks(Collider obstacle) => obstacle != null && !obstacle.transform.IsChildOf(prop) &&
            (rig == null || !obstacle.transform.IsChildOf(rig)) &&
            !Physics.GetIgnoreLayerCollision(prop.gameObject.layer, obstacle.gameObject.layer);
    }
}
