using UnityEngine;

namespace XRApartment.Runtime.ReviewV2.UI
{
    // Scene-only presentation override. Recipe observations and UI text updates continue unchanged.
    public sealed class ReviewLegacyCookingHintVisibility : MonoBehaviour
    {
        [SerializeField] ReviewSessionHost host;
        KitchenRecipeController observedRecipe;
        Canvas hiddenCanvas;
        bool originalEnabled;

        void LateUpdate()
        {
            var recipe = host != null ? host.recipe : null;
            if (recipe == observedRecipe) return;
            Restore();
            observedRecipe = recipe;
            if (recipe == null) return;
            var panel = recipe.transform.Find("UI_CookingInstructions");
            hiddenCanvas = panel != null ? panel.GetComponent<Canvas>() : null;
            if (hiddenCanvas == null) return;
            originalEnabled = hiddenCanvas.enabled;
            hiddenCanvas.enabled = false;
        }
        void Restore()
        {
            if (hiddenCanvas != null) hiddenCanvas.enabled = originalEnabled;
            hiddenCanvas = null;
        }
        void OnDisable() { Restore(); observedRecipe = null; }
    }
}
