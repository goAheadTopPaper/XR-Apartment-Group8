using System.Linq;
using UnityEngine;

namespace XRApartment.Runtime.ReviewV2
{
    // A complete authored world, instantiated inactive into its own runtime scene.
    public sealed class ReviewSchemeWorld : MonoBehaviour
    {
        public string schemeId, modelVersion;
        public KitchenCookingStation station;
        public KitchenRecipeController recipe;
        public KitchenRoomReset roomReset;
        public ReviewGroceryActivity groceries;
        public ReviewSceneIdentity[] identities;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(schemeId) && !string.IsNullOrWhiteSpace(modelVersion) &&
            station != null && recipe != null && roomReset != null && groceries != null && groceries.IsConfigured &&
            station.ActiveFridge != null && identities != null && identities.Length > 0 &&
            identities.All(i => i != null && !string.IsNullOrWhiteSpace(i.Id)) &&
            identities.Select(i => i.Id).Distinct().Count() == identities.Length;
        public void Bind(ReviewSessionHost host)
        {
            host.station = station; host.recipe = recipe; host.roomReset = roomReset;
            host.groceries = groceries; host.identities = identities;
            groceries.host = host;
            foreach (var bridge in GetComponentsInChildren<CookingGrabBridge>(true)) bridge.host = host;
        }
    }
}
