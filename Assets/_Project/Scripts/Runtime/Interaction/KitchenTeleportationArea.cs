using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace XRApartment.Runtime
{
    public sealed class KitchenTeleportationArea : TeleportationArea
    {
        bool HasClearTarget(IXRInteractor interactor) =>
            teleportationProvider is KitchenTeleportationProvider provider && interactor is XRRayInteractor ray &&
            ray.TryGetCurrent3DRaycastHit(out var hit) && colliders.Contains(hit.collider) && provider.CanTeleportTo(hit.point);

        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            base.IsSelectableBy(interactor) && HasClearTarget(interactor);

        protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit hit, ref TeleportRequest request) =>
            base.GenerateTeleportRequest(interactor, hit, ref request) &&
            teleportationProvider is KitchenTeleportationProvider provider && provider.CanTeleportTo(request.destinationPosition);
    }
}
