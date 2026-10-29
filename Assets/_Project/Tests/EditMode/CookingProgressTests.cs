using NUnit.Framework;
using XRApartment.Data;
using XRApartment.Simulation;

namespace XRApartment.Tests
{
    public sealed class CookingProgressTests
    {
        [Test] public void HeatingStopsWhenRemovedAndOvercookingIsIrreversible()
        {
            var progress = new CookingProgress(12, 32);
            progress.Advance(1, 12); Assert.AreEqual(CookingStage.Cooked, progress.Stage);
            progress.Advance(0, 100); Assert.AreEqual(12, progress.Dose);
            progress.Advance(1, 20); Assert.AreEqual(CookingStage.Burnt, progress.Stage);
            progress.Advance(0, 100); Assert.AreEqual(CookingStage.Burnt, progress.Stage);
        }

        [Test] public void StirringConservesVolumeAndPouringRetainsMixAndHeat()
        {
            var bowl = new EggBatch(200, 12, 32); var pan = new EggBatch(200, 12, 32);
            bowl.AddRaw(50); bowl.Whisk(.7f, .7f); bowl.Heat(1, 4);
            Assert.AreEqual(CookingStage.Beaten, bowl.Stage);
            Assert.AreEqual(30, bowl.TransferTo(pan, 30));
            Assert.AreEqual(50, bowl.Millilitres + pan.Millilitres);
            Assert.AreEqual(1, pan.MixedFraction); Assert.AreEqual(4, pan.Cooking.Dose);
            pan.Heat(1, 8); Assert.AreEqual(CookingStage.Cooked, pan.Stage);
        }

        [Test] public void CapacityAndHotSolidRejectionDoNotLoseSourceVolume()
        {
            var source = new EggBatch(200, 12, 32); var target = new EggBatch(60, 12, 32);
            source.AddRaw(100); Assert.AreEqual(60, source.TransferTo(target, 100));
            Assert.AreEqual(40, source.Millilitres); target.Heat(1, 12);
            Assert.AreEqual(0, source.TransferTo(target, 40)); Assert.AreEqual(40, source.Millilitres);
            Assert.AreEqual(0, target.AddRaw(10)); Assert.AreEqual(0, target.TransferTo(source, 10));
        }

        [Test] public void NewRawEggDilutesMixingAndEmptyingResetsTheBatch()
        {
            var batch = new EggBatch(200, 12, 32); batch.AddRaw(50); batch.Whisk(.7f, .7f);
            batch.AddRaw(50); Assert.That(batch.MixedFraction, Is.EqualTo(.5f).Within(.0001f));
            batch.Remove(100); Assert.AreEqual(CookingStage.Empty, batch.Stage);
            batch.AddRaw(50); Assert.AreEqual(CookingStage.Raw, batch.Stage); Assert.AreEqual(0, batch.MixedFraction);
        }

        [Test] public void InvalidInputCannotCreateVolumeOrInstantCooking()
        {
            var batch = new EggBatch(200, 12, 32); batch.AddRaw(50);
            foreach (var value in new[] { float.NaN, float.PositiveInfinity, -1f })
            { Assert.AreEqual(0, batch.AddRaw(value)); batch.Whisk(value, .7f); batch.Heat(value, 10); batch.Heat(1, value); }
            Assert.AreEqual(50, batch.Millilitres); Assert.AreEqual(0, batch.MixedFraction); Assert.AreEqual(0, batch.Cooking.Dose);
        }
    }
}
