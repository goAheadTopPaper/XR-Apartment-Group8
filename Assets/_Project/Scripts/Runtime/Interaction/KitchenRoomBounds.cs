using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRApartment.Runtime
{
    /// Virtual containment follows the authored floor envelope of the active kitchen.
    public sealed class KitchenRoomBounds : MonoBehaviour
    {
        static readonly Dictionary<int, KitchenRoomBounds> Rooms = new Dictionary<int, KitchenRoomBounds>();
        [SerializeField] Collider floor;
        internal float FloorHeight { get; private set; }

        public void Configure(Collider surface) => floor = surface;

        void OnEnable()
        {
            FloorHeight = floor.bounds.max.y;
            Rooms[gameObject.scene.handle] = this;
        }

        void OnDisable() => Rooms.Remove(gameObject.scene.handle);

        internal static bool TryGet(Scene scene, out KitchenRoomBounds room) => Rooms.TryGetValue(scene.handle, out room);

        internal bool Contains(Vector3 position)
        {
            var bounds = floor.bounds;
            return position.x >= bounds.min.x && position.x <= bounds.max.x &&
                position.z >= bounds.min.z && position.z <= bounds.max.z && position.y >= FloorHeight - .32f;
        }

        internal bool AtFloor(Vector3 position) => Mathf.Abs(position.y - FloorHeight) < .12f;
    }
}
