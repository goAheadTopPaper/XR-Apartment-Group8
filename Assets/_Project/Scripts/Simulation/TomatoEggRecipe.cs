using XRApartment.Data;

namespace XRApartment.Simulation
{
    /// Evidence for the optional recipe. It never advances the formal review journey.
    public sealed class TomatoEggRecipe
    {
        const TomatoEggAction All = (TomatoEggAction)((1 << 14) - 1);
        public TomatoEggAction Actions { get; private set; }
        public bool EggCooked { get; private set; }
        public bool TomatoCooked { get; private set; }
        public bool IsBurnt { get; private set; }
        public bool Ready => !IsBurnt && EggCooked && TomatoCooked && (Actions & All) == All;
        public TomatoEggStep Step
        {
            get
            {
                if (IsBurnt) return TomatoEggStep.Burnt;
                for (var i = 0; i < 14; i++)
                    if ((Actions & (TomatoEggAction)(1 << i)) == 0) return (TomatoEggStep)i;
                return Ready ? TomatoEggStep.Ready : TomatoEggStep.StirFry;
            }
        }
        public void Observe(TomatoEggAction evidence) => Actions |= evidence & All;
        public void ObserveCooking(bool eggCooked, bool tomatoCooked, bool burnt)
        { EggCooked = eggCooked; TomatoCooked = tomatoCooked; IsBurnt |= burnt; }
        public void Reset() { Actions = TomatoEggAction.None; EggCooked = TomatoCooked = IsBurnt = false; }
    }
}
