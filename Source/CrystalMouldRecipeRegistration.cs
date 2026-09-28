using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alta;
using Alta.Blacksmithing;
using Alta.Inventory;
using UnityEngine;

namespace CrystalWeapons;

public static class CrystalMouldRecipeRegistration
{
    public const uint CrystalGemBlueItemHash = 45754u;
    private const string CrystalGemBlueName = "Crystal Gem Blue";
    private const uint RecipeHashOffset = 0x43570000u;
    private const uint RecipeHashMultiplier = 0x9E3779B1u;
    private const string RecipeNamePrefix = "Crystal Mould ";

    private static readonly FieldInfo ItemCountItemField = typeof(ItemCount).GetField("item", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(ItemCount).FullName, "item");
    private static readonly FieldInfo ItemCountCountField = typeof(ItemCount).GetField("count", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(ItemCount).FullName, "count");

    private static bool registered;
    private static IReadOnlyList<CrystalMouldTarget> targets = Array.Empty<CrystalMouldTarget>();
    private static readonly Dictionary<uint, CrystalMouldTarget> TargetsByMouldHash = new Dictionary<uint, CrystalMouldTarget>();
    private static readonly Dictionary<uint, CrystalMouldTarget> TargetsByRecipeHash = new Dictionary<uint, CrystalMouldTarget>();

    public static IReadOnlyList<CrystalMouldTarget> Register()
    {
        if (registered) return targets;

        Item.CheckItems();
        SmeltingRecipe.CheckItems();
        var crystalGemBlue = Item.All.FirstOrDefault(item => item.Hash == CrystalGemBlueItemHash);
        if (crystalGemBlue == null || !string.Equals(crystalGemBlue.name, CrystalGemBlueName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Could not resolve Crystal Gem Blue item hash " + CrystalGemBlueItemHash + " with its expected name.");
        }

        var template = SmeltingRecipe.All.FirstOrDefault(recipe =>
            ReadItemCounts(recipe, "input").Length == 1 && ReadItemCounts(recipe, "output").Length == 1);
        if (template == null)
        {
            throw new InvalidOperationException("No vanilla one-input/one-output SmeltingRecipe template is available.");
        }

        var recipeRegistry = GetRecipeRegistry();
        var registeredTargets = new List<CrystalMouldTarget>();
        var allDefinitions = MouldDefinition.All.ToArray();
        foreach (var missingDefinition in allDefinitions.Where(definition => definition == null))
        {
            Core.Logger.Warning("Skipping mould registry entry because its MouldDefinition is missing.");
        }

        var candidates = allDefinitions
            .Where(definition => definition != null)
            .OrderBy(definition => definition.Product == null ? string.Empty : definition.Product.name, StringComparer.Ordinal)
            .ThenBy(definition => definition.Hash)
            .ToArray();

        foreach (var definition in candidates)
        {
            var product = definition.Product;
            var cost = definition.Cost;
            var outputQuantity = definition.QuantityProduced;
            if (product == null)
            {
                Core.Logger.Warning("Skipping mould " + definition.Hash + ": product is missing.");
                continue;
            }
            if (cost <= 0)
            {
                Core.Logger.Warning("Skipping mould " + definition.Hash + " for " + product.name + ": cost " + cost + " is invalid.");
                continue;
            }
            if (outputQuantity <= 0)
            {
                Core.Logger.Warning("Skipping mould " + definition.Hash + " for " + product.name + ": output quantity " + outputQuantity + " is invalid.");
                continue;
            }
            if (product.Prefab == null)
            {
                Core.Logger.Warning("Skipping mould " + definition.Hash + " for " + product.name + ": product prefab is missing.");
                continue;
            }

            var recipeHash = GetRecipeHash(definition.Hash);
            if (recipeRegistry.ContainsKey(recipeHash) || TargetsByRecipeHash.ContainsKey(recipeHash))
            {
                Core.Logger.Warning("Skipping mould " + definition.Hash + " for " + product.name + ": deterministic recipe hash " + recipeHash + " collides with a registered recipe.");
                continue;
            }

            try
            {
                var recipe = CreateRecipe(template, definition, product, cost, outputQuantity, crystalGemBlue, recipeHash);
                recipeRegistry.Add(recipe.Hash, recipe);
                AddRecipeToAllSmelterUpgradeSets(recipe);
                var target = new CrystalMouldTarget(definition, product, cost, outputQuantity, recipe);
                TargetsByMouldHash.Add(definition.Hash, target);
                TargetsByRecipeHash.Add(recipe.Hash, target);
                registeredTargets.Add(target);
                Core.Logger.Msg("Registered Crystal mould product " + product.name + "(" + product.Hash + "): mould="
                    + definition.Hash + ", Crystal Gem Blue cost=" + cost + ", output=" + outputQuantity
                    + ", recipe=" + recipe.Hash + ".");
            }
            catch (Exception exception)
            {
                Core.Logger.Error("Could not register Crystal mould " + definition.Hash + " for " + product.name + ": " + exception);
            }
        }

        if (registeredTargets.Count > 0)
        {
            CrystalSmelterInputFilter.Allow(crystalGemBlue);
        }

        targets = registeredTargets.ToArray();
        registered = true;
        Core.Logger.Msg("Crystal Weapons registered " + targets.Count + " of " + candidates.Length + " discovered mould products.");
        return targets;
    }

    public static CrystalMouldTarget? FindTarget(uint mouldHash)
    {
        return TargetsByMouldHash.TryGetValue(mouldHash, out var target) ? target : null;
    }

    public static CrystalMouldTarget? FindTarget(uint mouldHash, uint productHash)
    {
        return TargetsByMouldHash.TryGetValue(mouldHash, out var target) && target.Product.Hash == productHash ? target : null;
    }

    public static SmeltingRecipe? FindRecipe(uint recipeHash)
    {
        return TargetsByRecipeHash.TryGetValue(recipeHash, out var target) ? target.Recipe : null;
    }

    public static CrystalMouldTarget? FindTargetForRecipe(uint recipeHash)
    {
        return TargetsByRecipeHash.TryGetValue(recipeHash, out var target) ? target : null;
    }

    private static SmeltingRecipe CreateRecipe(
        SmeltingRecipe template,
        MouldDefinition definition,
        Item product,
        int cost,
        int outputQuantity,
        Item crystalGemBlue,
        uint recipeHash)
    {
        var recipe = UnityEngine.Object.Instantiate(template);
        recipe.name = RecipeNamePrefix + product.name + " " + definition.Hash;
        AssignStableHash(recipe, recipeHash, recipe.name);

        var inputs = ReadItemCounts(recipe, "input");
        var outputs = ReadItemCounts(recipe, "output");
        SetItem(inputs[0], crystalGemBlue);
        SetCount(inputs[0], cost);
        SetItem(outputs[0], product);
        SetCount(outputs[0], outputQuantity);
        return recipe;
    }

    private static uint GetRecipeHash(uint mouldHash)
    {
        // Multiplication by an odd number is one-to-one over UInt32 values.
        return unchecked(mouldHash * RecipeHashMultiplier + RecipeHashOffset);
    }

    private static ItemCount[] ReadItemCounts(SmeltingRecipe recipe, string fieldName)
    {
        var field = typeof(SmeltingRecipe).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(SmeltingRecipe).FullName, fieldName);
        return field.GetValue(recipe) as ItemCount[]
            ?? throw new InvalidOperationException("Smelting recipe " + fieldName + " array is unavailable.");
    }

    private static void SetItem(ItemCount itemCount, Item item)
    {
        ItemCountItemField.SetValue(itemCount, item);
    }

    private static void SetCount(ItemCount itemCount, int count)
    {
        if (ItemCountCountField.FieldType != typeof(int))
        {
            throw new MissingFieldException(typeof(ItemCount).FullName, "count (expected Int32)");
        }

        ItemCountCountField.SetValue(itemCount, count);
    }

    private static void AssignStableHash(HashedGeneralValue value, uint hash, string name)
    {
        var field = typeof(HashedGeneralValue).GetField("hash", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(HashedGeneralValue).FullName, "hash");
        field.SetValue(value, unchecked((int)hash));
        if (value.Hash != hash) throw new InvalidOperationException("Could not assign stable recipe hash " + hash + " to " + name + ".");
    }

    private static Dictionary<uint, SmeltingRecipe> GetRecipeRegistry()
    {
        var registryType = typeof(HashedGeneralValue<SmeltingRecipe>);
        var field = registryType.GetField("items", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(registryType.FullName, "items");
        return field.GetValue(null) as Dictionary<uint, SmeltingRecipe>
            ?? throw new InvalidOperationException("Smelting recipe registry is unavailable.");
    }

    private static void AddRecipeToAllSmelterUpgradeSets(SmeltingRecipe recipe)
    {
        var recipesField = typeof(SmelterUpgrades).GetField("recipes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(SmelterUpgrades).FullName, "recipes");
        foreach (var upgrades in SmelterUpgrades.All)
        {
            var recipes = recipesField.GetValue(upgrades) as SmeltingRecipe[]
                ?? throw new InvalidOperationException("Smelter upgrade recipe list is unavailable.");
            if (recipes.Any(existing => existing != null && existing.Hash == recipe.Hash)) continue;
            recipesField.SetValue(upgrades, recipes.Concat(new[] { recipe }).ToArray());
        }
    }
}
