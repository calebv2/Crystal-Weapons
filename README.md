# Crystal Weapons

Crystal Weapons adds Crystal Gem smelting recipes for every valid mould. It includes weapons, tools, hammers, and anything else defined by a mould.

## Recipes

- Copper Ingot and Crystal Gem each cost the mould's normal ingot count. A mould that normally costs 5 ingots requires 5 Copper Ingots and 5 Crystal Gems.
- The output keeps its normal item identity and receives a registered Crystal physical material.
- Crystal inputs require the corresponding registered mould. Mixed ingredients and invalid or insufficient Crystal stacks are rejected without consuming them.

## Material, appearance, and configuration

The Crystal material uses a damage multiplier of 0.85 and durability multiplier of 0.85, between Copper (0.7) and Iron (1.0). Other gameplay. Its renderer materials and material channels use the vanilla Iron template, matching the Crystal appearance, then receive a ice-blue tint and emission.

MelonLoader creates a `CrystalWeapons` preferences category displayed as **Crystal Weapons**. Each gameplay override uses the matching game field name plus `Override`. The damage and durability overrides default to `0.85`; other float overrides default to `-1` to inherit Red Iron. All float overrides accept finite nonnegative values. `hardnessLevelOverride` uses the same `-1` inherit value and accepts nonnegative integers. The supplied `CrystalWeapons.example.cfg` contains only this mod's settings. When upgrading from an older release, rename the preferences section to `[CrystalWeapons]` so existing custom overrides carry over.

Configurable fields:

```text
sourceThermalConductivityOverride
receiveThermalConductivityOverride
internalThermalConductivityOverride
glowingStartOverride
glowingEndOverride
forgeMultiplierOverride
meltingPointOverride
maxTemperatureForParticlesOverride
nailHealthMultiplierOverride
maxCraftingDamageMultiplierOverride
damageMultiplierOverride
durabilityMultiplierOverride
densityOverride
weightMultiplierOverride
tightnessMultiplierOverride
tightnessBowImpactOverride
minimumProjectileWeightOverride
maxInvalidProjectileVelocityMultiplierOverride
noiseMultiplierOverride
hardnessLevelOverride
```

Invalid values are logged and fall back to the Red Iron field value.

## Client companion

Dedicated servers use `CrystalWeapons.dll` for Crystal recipes, forge behavior, material registration, and output assignment. Player clients use `CrystalWeapons.Client.dll`; it contains only the shared material registration, appearance, and config needed to resolve the replicated Crystal material hash locally. It does not add recipes or patch forging.

Build the client DLL with `./build-client.sh`; the artifact is written to `Crystal Weapons Client Build/CrystalWeapons.Client.dll`. Install that client DLL in each player's client `Mods` folder. Keep `CrystalWeapons.dll` on the server and do not place both variants in the same process. The client build disables itself in batch/server runtimes.

Keep matching `CrystalWeapons` override values on the server and clients so the registered Crystal material has consistent local stats. The server remains authoritative for recipes, crafting, damage, and durability.
