using Alta.Blacksmithing;
using Alta.Inventory;

namespace CrystalWeapons;

public sealed class CrystalMouldTarget
{
    public CrystalMouldTarget(MouldDefinition definition, Item product, int cost, int outputQuantity, SmeltingRecipe recipe)
    {
        Definition = definition;
        Product = product;
        Cost = cost;
        OutputQuantity = outputQuantity;
        Recipe = recipe;
    }

    public MouldDefinition Definition { get; }
    public Item Product { get; }
    public int Cost { get; }
    public int OutputQuantity { get; }
    public SmeltingRecipe Recipe { get; }
    public uint RecipeHash => Recipe.Hash;
}
