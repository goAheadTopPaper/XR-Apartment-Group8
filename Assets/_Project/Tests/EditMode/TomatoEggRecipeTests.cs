using NUnit.Framework;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public sealed class TomatoEggRecipeTests
    {
        static readonly TomatoEggAction All = (TomatoEggAction)((1 << 14) - 1);
        [Test] public void BlockedDoorEasingCanResumeWithoutApplyingEasingTwice()
        {
            foreach (var t in new[] { 0.0, .005, .025, .2, .5, .75, .995, 1.0 })
                Assert.That(MotionKinematics.InverseSmoothStep(MotionKinematics.SmoothStep(t)), Is.EqualTo(t).Within(.000001));
        }
        [Test] public void StartsWithOpeningFridgeAndCannotFinishWithoutActions()
        {
            var recipe = new TomatoEggRecipe(); recipe.ObserveCooking(true, true, false);
            Assert.AreEqual(TomatoEggStep.OpenFridge, recipe.Step); Assert.IsFalse(recipe.Ready);
        }
        [Test] public void MissingDicingCannotBeReplacedByHotIngredients()
        {
            var recipe = new TomatoEggRecipe(); recipe.Observe(All & ~TomatoEggAction.DiceTomato);
            recipe.ObserveCooking(true, true, false);
            Assert.AreEqual(TomatoEggStep.DiceTomato, recipe.Step); Assert.IsFalse(recipe.Ready);
        }
        [Test] public void ColdFoodCannotCompleteEvenWithAllGestures()
        {
            var recipe = new TomatoEggRecipe(); recipe.Observe(All); recipe.ObserveCooking(true, false, false);
            Assert.AreEqual(TomatoEggStep.StirFry, recipe.Step); Assert.IsFalse(recipe.Ready);
        }
        [Test] public void RequiresActualScramblingAndFinalStirFry()
        {
            var recipe = new TomatoEggRecipe(); recipe.Observe(All & ~TomatoEggAction.ScrambleEgg & ~TomatoEggAction.StirFry);
            recipe.ObserveCooking(true, true, false); Assert.IsFalse(recipe.Ready);
            recipe.Observe(TomatoEggAction.ScrambleEgg); Assert.IsFalse(recipe.Ready);
            recipe.Observe(TomatoEggAction.StirFry); Assert.IsTrue(recipe.Ready);
        }
        [Test] public void BurntIsTerminalUntilReset()
        {
            var recipe = new TomatoEggRecipe(); recipe.Observe(All); recipe.ObserveCooking(true, true, true);
            recipe.ObserveCooking(true, true, false);
            Assert.AreEqual(TomatoEggStep.Burnt, recipe.Step); Assert.IsFalse(recipe.Ready);
            recipe.Reset(); Assert.AreEqual(TomatoEggStep.OpenFridge, recipe.Step);
            Assert.AreEqual(TomatoEggAction.None, recipe.Actions); Assert.IsFalse(recipe.IsBurnt);
        }
        [Test] public void PartialPreparationCanBeObservedWithoutSkippingMissingSteps()
        {
            var recipe = new TomatoEggRecipe(); recipe.Observe(TomatoEggAction.TakeEgg | TomatoEggAction.TurnOnStove);
            Assert.AreEqual(TomatoEggStep.OpenFridge, recipe.Step);
            recipe.Observe(TomatoEggAction.OpenFridge); Assert.AreEqual(TomatoEggStep.TakeTomato, recipe.Step);
        }
    }
}
