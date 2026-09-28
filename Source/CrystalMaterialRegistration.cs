using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alta;
using Alta.Inventory;
using UnityEngine;

namespace CrystalWeapons;

public static class CrystalMaterialRegistration
{
    public const uint CrystalMaterialHash = 0x43574D00u;
    private const uint RedIronIngotHash = 30996u;
    private const string CrystalMaterialName = "Crystal Red Iron";
    private const string AppearanceTemplateName = "Iron";
    private static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(object).FullName, "MemberwiseClone");

    public static PhysicalMaterial? RegisteredMaterial { get; private set; }

    public static PhysicalMaterial CreateAndRegister()
    {
        if (RegisteredMaterial != null) return RegisteredMaterial;

        Item.CheckItems();
        PhysicalMaterial.CheckItems();
        var redIronItem = Item.All.FirstOrDefault(item => item.Hash == RedIronIngotHash);
        if (redIronItem == null || redIronItem.name.IndexOf("Red Iron", StringComparison.OrdinalIgnoreCase) < 0)
        {
            throw new InvalidOperationException("Could not resolve Red Iron ingot item hash " + RedIronIngotHash + ".");
        }

        var ingot = redIronItem.GetComponent<Ingot>();
        var redIron = ingot == null ? null : ingot.PhysicalMaterial;
        if (redIron == null)
        {
            throw new InvalidOperationException("Red Iron ingot " + redIronItem.name + " has no Ingot.PhysicalMaterial template.");
        }

        var ironAppearance = PhysicalMaterial.All.FirstOrDefault(candidate =>
            string.Equals(candidate.name, AppearanceTemplateName, StringComparison.Ordinal));
        if (ironAppearance == null)
        {
            throw new InvalidOperationException("Could not resolve the vanilla Iron PhysicalMaterial used by the RepairHammer crystal appearance.");
        }

        Core.Logger.Msg("Crystal Weapons material template: " + redIronItem.name + "(" + redIronItem.Hash + ") uses PhysicalMaterial '" + redIron.name + "'(" + redIron.Hash + ").");

        var material = UnityEngine.Object.Instantiate(redIron);
        material.name = CrystalMaterialName;
        CopyAndTintAppearance(material, ironAppearance);
        CrystalForgeConfig.ApplyOverrides(material, redIron);
        AssignStableHash(material, CrystalMaterialHash, CrystalMaterialName);
        Register(material);
        Core.Logger.Msg("Registered Crystal Weapons material '" + material.name + "' with stable hash " + material.Hash + ".");
        return material;
    }

    private static void CopyAndTintAppearance(PhysicalMaterial material, PhysicalMaterial appearanceTemplate)
    {
        var clonedMaterials = new Dictionary<Material, Material>();
        foreach (var field in typeof(PhysicalMaterial).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType == typeof(Material) && field.GetValue(appearanceTemplate) is Material sourceMaterial)
            {
                field.SetValue(material, CloneAndTintMaterial(sourceMaterial, clonedMaterials));
            }
        }

        var channelsField = typeof(PhysicalMaterial).GetField("materialChannels", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(PhysicalMaterial).FullName, "materialChannels");
        if (channelsField.GetValue(appearanceTemplate) is not Array channels)
        {
            throw new InvalidOperationException("Crystal Weapons requires Iron PhysicalMaterial.materialChannels to be an array.");
        }

        var elementType = channels.GetType().GetElementType()
            ?? throw new InvalidOperationException("Crystal Weapons material channel type is unavailable.");
        var clonedChannels = Array.CreateInstance(elementType, channels.Length);
        for (var index = 0; index < channels.Length; index++)
        {
            var channel = channels.GetValue(index);
            if (channel == null) continue;

            var clonedChannel = MemberwiseCloneMethod.Invoke(channel, null)
                ?? throw new InvalidOperationException("Crystal Weapons failed to clone an Iron material channel.");
            foreach (var field in channel.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType == typeof(Material) && field.GetValue(clonedChannel) is Material sourceMaterial)
                {
                    field.SetValue(clonedChannel, CloneAndTintMaterial(sourceMaterial, clonedMaterials));
                }
            }

            clonedChannels.SetValue(clonedChannel, index);
        }

        channelsField.SetValue(material, clonedChannels);
        Core.Logger.Msg("Crystal Weapons appearance: copied the vanilla Iron renderer materials/channels, cloned "
            + clonedMaterials.Count + " Unity materials, and applied the RepairHammer ice-blue tint/emission.");
    }

    private static Material CloneAndTintMaterial(Material source, IDictionary<Material, Material> clones)
    {
        if (clones.TryGetValue(source, out var existing)) return existing;

        var clone = new Material(source) { name = "Crystal Ice " + source.name };
        var tint = new Color(
            CrystalAppearancePolicy.IceTintRed,
            CrystalAppearancePolicy.IceTintGreen,
            CrystalAppearancePolicy.IceTintBlue,
            CrystalAppearancePolicy.IceTintAlpha);
        var emission = new Color(
            CrystalAppearancePolicy.IceEmissionRed,
            CrystalAppearancePolicy.IceEmissionGreen,
            CrystalAppearancePolicy.IceEmissionBlue,
            1f);

        foreach (var propertyName in new[] { "_ColorA", "_ColorB", "_Color" })
        {
            if (CrystalAppearancePolicy.ShouldTint(propertyName) && clone.HasProperty(propertyName)) clone.SetColor(propertyName, tint);
        }

        foreach (var propertyName in new[] { "_Emission", "_EmissionColor" })
        {
            if (!CrystalAppearancePolicy.ShouldSetEmission(propertyName) || !clone.HasProperty(propertyName)) continue;
            clone.EnableKeyword("_EMISSION");
            clone.SetColor(propertyName, emission);
        }

        clones.Add(source, clone);
        return clone;
    }

    private static void Register(PhysicalMaterial material)
    {
        var registry = GetRegistry();
        if (registry.TryGetValue(material.Hash, out var existing))
        {
            if (ReferenceEquals(existing, material))
            {
                RegisteredMaterial = material;
                return;
            }

            throw new InvalidOperationException("A different PhysicalMaterial is already registered for Crystal material hash " + material.Hash + ".");
        }

        registry.Add(material.Hash, material);
        RegisteredMaterial = material;
    }

    private static void AssignStableHash(HashedGeneralValue value, uint hash, string name)
    {
        var hashField = typeof(HashedGeneralValue).GetField("hash", BindingFlags.Instance | BindingFlags.NonPublic);
        if (hashField == null || hashField.FieldType != typeof(int))
        {
            throw new MissingFieldException(typeof(HashedGeneralValue).FullName, "hash (expected serialized Int32 hash)");
        }

        hashField.SetValue(value, unchecked((int)hash));
        if (value.Hash != hash) throw new InvalidOperationException("Could not assign stable hash " + hash + " to " + name + ".");
    }

    private static Dictionary<uint, PhysicalMaterial> GetRegistry()
    {
        var registryType = typeof(HashedGeneralValue<PhysicalMaterial>);
        var registryField = registryType.GetField("items", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(registryType.FullName, "items");
        return registryField.GetValue(null) as Dictionary<uint, PhysicalMaterial>
            ?? throw new InvalidOperationException("PhysicalMaterial registry is unavailable.");
    }
}
