using System;
using UnityEngine;
using XRApartment.Data;

namespace XRApartment.Runtime
{
    public sealed class KitchenFridge : MonoBehaviour
    {
        [SerializeField] KinematicSwingDoor door;
        [SerializeField] KitchenCookingProfile profile;
        [SerializeField] Light interiorLight;
        [SerializeField] Bounds storageBounds = new Bounds(new Vector3(0, .95f, 0), new Vector3(.62f, 1.65f, .61f));
        public KinematicSwingDoor Door => door;
        public bool Accessible => door != null && profile != null && door.Progress >= profile.fridgeAccessProgress;
        public event Action<KitchenStoredFood> Retrieved;
        public void Configure(KinematicSwingDoor leaf, KitchenCookingProfile settings, Bounds interior)
        { door = leaf; profile = settings; storageBounds = interior; }
        public bool Contains(Vector3 point) => storageBounds.Contains(transform.InverseTransformPoint(point));
        public void ConfigureLight(Light lamp) => interiorLight = lamp;
        void LateUpdate() { if (interiorLight != null) interiorLight.enabled = door != null && door.Progress > .02; }
        public void RecordRetrieval(KitchenStoredFood food) => Retrieved?.Invoke(food);
        public void ResetFridge() { if (door != null) door.SetOpenImmediate(false); }
    }
}
