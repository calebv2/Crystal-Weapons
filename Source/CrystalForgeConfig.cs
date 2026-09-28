using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using MelonLoader;

namespace CrystalWeapons;

public static class CrystalForgeConfig
{
    private const string CategoryName = "CrystalWeapons";
    private const float InheritValue = -1f;
    private static readonly string[] FloatFieldNames =
    {
        "sourceThermalConductivity",
        "receiveThermalConductivity",
        "internalThermalConductivity",
        "glowingStart",
        "glowingEnd",
        "forgeMultiplier",
        "meltingPoint",
        "maxTemperatureForParticles",
        "nailHealthMultiplier",
        "maxCraftingDamageMultiplier",
        "damageMultiplier",
        "durabilityMultiplier",
        "density",
        "weightMultiplier",
        "tightnessMultiplier",
        "tightnessBowImpact",
        "minimumProjectileWeight",
        "maxInvalidProjectileVelocityMultiplier",
        "noiseMultiplier"
    };

    private static readonly BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, MelonPreferences_Entry<float>> FloatEntries = new Dictionary<string, MelonPreferences_Entry<float>>(StringComparer.Ordinal);
    private static MelonPreferences_Entry<int>? hardnessLevelOverride;
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized) return;

        var category = MelonPreferences.CreateCategory(CategoryName, "Crystal Weapons");
        foreach (var fieldName in FloatFieldNames)
        {
            var identifier = fieldName + "Override";
            FloatEntries.Add(identifier, category.CreateEntry<float>(
                identifier,
                InheritValue,
                fieldName + " override",
                "Set to -1 to inherit the Red Iron value; use a finite, nonnegative value to override."));
        }

        hardnessLevelOverride = category.CreateEntry<int>(
            "hardnessLevelOverride",
            -1,
            "hardnessLevel override",
            "Set to -1 to inherit the Red Iron value; use a nonnegative integer to override.");
        initialized = true;
    }

    public static void ApplyOverrides(PhysicalMaterial target, PhysicalMaterial redIron)
    {
        if (!initialized) Initialize();
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (redIron == null) throw new ArgumentNullException(nameof(redIron));

        foreach (var fieldName in FloatFieldNames)
        {
            var field = GetMaterialField(fieldName, typeof(float));
            var redIronValue = (float)(field.GetValue(redIron) ?? 0f);
            var configuredValue = FloatEntries[fieldName + "Override"].Value;
            if (configuredValue == InheritValue)
            {
                field.SetValue(target, redIronValue);
                continue;
            }

            if (float.IsNaN(configuredValue) || float.IsInfinity(configuredValue) || configuredValue < 0f)
            {
                Core.Logger.Warning("Invalid config value for " + fieldName + "Override="
                    + configuredValue.ToString(CultureInfo.InvariantCulture) + "; using Red Iron value "
                    + redIronValue.ToString("0.###", CultureInfo.InvariantCulture) + ".");
                field.SetValue(target, redIronValue);
                continue;
            }

            field.SetValue(target, configuredValue);
        }

        var hardnessField = GetMaterialField("hardnessLevel", typeof(uint));
        var redIronHardness = (uint)(hardnessField.GetValue(redIron) ?? 0u);
        var configuredHardness = hardnessLevelOverride!.Value;
        if (configuredHardness == -1)
        {
            hardnessField.SetValue(target, redIronHardness);
        }
        else if (configuredHardness < 0)
        {
            Core.Logger.Warning("Invalid config value for hardnessLevelOverride=" + configuredHardness
                + "; using Red Iron value " + redIronHardness + ".");
            hardnessField.SetValue(target, redIronHardness);
        }
        else
        {
            hardnessField.SetValue(target, (uint)configuredHardness);
        }

        var effectiveValues = new List<string>();
        foreach (var fieldName in FloatFieldNames)
        {
            var field = GetMaterialField(fieldName, typeof(float));
            effectiveValues.Add(fieldName + "=" + ((float)(field.GetValue(target) ?? 0f)).ToString("0.###", CultureInfo.InvariantCulture));
        }
        effectiveValues.Add("hardnessLevel=" + hardnessField.GetValue(target));
        Core.Logger.Msg("Crystal Weapons effective Red Iron based material stats: " + string.Join(", ", effectiveValues) + ".");
    }

    private static FieldInfo GetMaterialField(string fieldName, Type expectedType)
    {
        var field = typeof(PhysicalMaterial).GetField(fieldName, InstanceFields);
        if (field == null || field.FieldType != expectedType)
        {
            throw new MissingFieldException(typeof(PhysicalMaterial).FullName, fieldName + " (expected " + expectedType.Name + ")");
        }

        return field;
    }
}
