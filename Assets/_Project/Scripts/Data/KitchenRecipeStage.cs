using System;

namespace XRApartment.Data
{
    public enum KitchenIngredientKind { Other, Tomato, Beef, Onion }
    public enum KitchenCutForm { Whole, Slice, Diced, Strip }
    public enum KitchenStoredFood { Tomato, Egg, Other }
    public enum KitchenRecipeChoice { TomatoEgg, BeefOnion }
    public enum BeefOnionStep { TakeIngredients, CutIngredients, AddToPan, CookAndStir, TurnOffStove, Plate, Served, Burnt }
    public enum TomatoEggStep
    {
        OpenFridge, TakeTomato, SliceTomato, DiceTomato, TakeEgg, TakeBowl,
        CrackEgg, BeatEgg, TurnOnStove, PlacePan, PourEgg, ScrambleEgg,
        AddTomato, StirFry, Ready, Burnt
    }

    [Flags]
    public enum TomatoEggAction
    {
        None = 0, OpenFridge = 1, TakeTomato = 2, SliceTomato = 4, DiceTomato = 8,
        TakeEgg = 16, TakeBowl = 32, CrackEgg = 64, BeatEgg = 128,
        TurnOnStove = 256, PlacePan = 512, PourEgg = 1024,
        ScrambleEgg = 2048, AddTomato = 4096, StirFry = 8192
    }
}
