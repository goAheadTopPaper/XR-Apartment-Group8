using XRApartment.Data;

namespace XRApartment.Simulation.ReviewV2
{
    public static class CookingPreparationEvidence
    {
        // The cut count catches the first real cut; SliceTomato is a fallback after all cuts finish.
        // CrackEgg requires enough egg to reach the bowl.
        // Opening storage, retrieving food, holding a tool, and switching on heat are not preparation evidence.
        const TomatoEggAction ProcessingActions = TomatoEggAction.SliceTomato | TomatoEggAction.CrackEgg;

        public static TomatoEggAction NewProcessingActions(TomatoEggAction previous, TomatoEggAction current)
            => current & ~previous & ProcessingActions;

        public static bool HasNewTomatoCut(int previous, int current) => current > previous && current > 0;
    }
}
