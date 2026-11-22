using UnityEngine;

namespace XRApartment.Data
{
    [CreateAssetMenu(menuName = "XR Apartment/Kitchen Cooking Profile")]
    public sealed class KitchenCookingProfile : ScriptableObject
    {
        [Min(.1f)] public float cookedSeconds = 12f;
        [Min(.2f)] public float burntSeconds = 32f;
        [Min(1f)] public float eggMillilitres = 50f;
        [Min(.01f)] public float whiskMetres = .7f;
        [Min(.01f)] public float maxWhiskMetresPerStep = .05f;
        [Min(.01f)] public float eggKnockSpeed = .55f;
        [Min(.1f)] public float emptyingSeconds = 2f;
        [Min(.01f)] public float receiverHeight = .2f;
        [Header("Tomato and egg recipe — prototype tuning")]
        [Min(1f)] public float tomatoGrams = 120f;
        [Range(.1f, 1f)] public float requiredTomatoFraction = .75f;
        [Min(.01f)] public float scrambleMetres = .45f;
        [Min(.01f)] public float stirFryMetres = .65f;
        [Range(.05f, .8f)] public float coagulationStartsAt = .25f;
        [Range(0f, 1f)] public float fridgeAccessProgress = .35f;
        [Min(.01f)] public float maxSpatulaMetresPerStep = .035f;
        [Min(.1f)] public float minimumEggMillilitres = 35f;
        [Min(0f)] public float foodLinearDamping = 3f;
        [Min(0f)] public float foodAngularDamping = 6f;
        [Min(.0001f)] public float foodContactOffset = .001f;
        [Min(0f)] public float cutSeparationSpeed = .01f;
        public Color rawAlbumen = new Color(.94f, .9f, .68f, .42f);
        public Color beatenEgg = new Color(1f, .66f, .08f, 1f);
        public Color cookedEgg = new Color(1f, .85f, .43f, 1f);
        public Color cookedFoodTint = new Color(.72f, .46f, .22f, 1f);
        public Color burntTint = new Color(.12f, .075f, .035f, 1f);
        public Color steam = new Color(.85f, .85f, .85f, .16f);
        public Color smoke = new Color(.16f, .14f, .12f, .25f);
    }
}
