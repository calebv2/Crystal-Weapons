using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alta.Blacksmithing;
using Alta.Inventory;
using HarmonyLib;

namespace CrystalWeapons;

[HarmonyPatch(typeof(Smelter), "FindRecipe")]
internal static class CrystalSmelterRecipeGate
{
    private static readonly FieldInfo CurrentMouldField = typeof(Smelter).GetField("currentMould", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(Smelter).FullName, "currentMould");
    private static readonly MethodInfo ConsumeMethod = typeof(Smelter).GetMethod(
        "Consume",
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        new[] { typeof(Dictionary<Item, int>) },
        null)
        ?? throw new MissingMethodException(typeof(Smelter).FullName, "Consume(Dictionary<Item, int>)");

    private static bool Prefix(
        Smelter __instance,
        float totalFuel,
        Dictionary<Item, int> input,
        ref float requiredFuel,
        ref SmeltingRecipe? __result)
    {
        var crystalEntries = input.Where(entry => entry.Key != null && entry.Key.Hash == CrystalMouldRecipeRegistration.CrystalGemBlueItemHash).ToArray();
        if (crystalEntries.Length == 0) return true;

        if (input.Count != 1 || crystalEntries.Length != 1 || crystalEntries[0].Value <= 0)
        {
            Reject(ref requiredFuel, ref __result, "Crystal Gem Blue was mixed with other inputs or had an invalid count.");
            return false;
        }

        var mould = CurrentMouldField.GetValue(__instance) as Mould;
        var definition = mould == null ? null : mould.Definition;
        var target = definition == null
            ? null
            : CrystalMouldRecipeRegistration.FindTarget(definition.Hash, definition.Product == null ? 0u : definition.Product.Hash);
        if (target == null)
        {
            Reject(ref requiredFuel, ref __result, "Crystal Gem Blue input has no registered matching mould.");
            return false;
        }

        if (crystalEntries[0].Value < target.Cost)
        {
            Reject(ref requiredFuel, ref __result, "Crystal Gem Blue count " + crystalEntries[0].Value + " is below mould cost " + target.Cost + ".");
            return false;
        }

        if (totalFuel < target.Recipe.Duration)
        {
            Reject(ref requiredFuel, ref __result, "Available fuel is below this mould recipe's duration.");
            return false;
        }

        // Every Crystal recipe has the same item input, so the native matcher
        // would select the first matching mould product in its recipe list.
        // Select this mould's exact recipe and invoke the game's normal dock
        // consumption routine with only its required Crystal cost.
        var requiredInputs = new Dictionary<Item, int>
        {
            [crystalEntries[0].Key] = target.Cost
        };
        try
        {
            ConsumeMethod.Invoke(__instance, new object[] { requiredInputs });
        }
        catch (Exception exception)
        {
            requiredFuel = 0f;
            __result = null;
            Core.Logger.Error("Crystal mould recipe " + target.RecipeHash + " could not consume its required Crystal input: " + exception);
            return false;
        }

        requiredFuel = target.Recipe.Duration;
        __result = target.Recipe;
        return false;
    }

    private static void Reject(ref float requiredFuel, ref SmeltingRecipe? result, string reason)
    {
        requiredFuel = 0f;
        result = null;
        Core.Logger.Msg("Crystal mould recipe rejected: " + reason);
    }
}
