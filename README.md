# Crystal Weapons

Crystal Weapons adds Crystal Gem Blue smelting recipes for every valid forge mould product. It includes weapons, tools, hammers, and any other product defined by a mould; it does not limit recipes to a hand-picked item list.

## Recipes

- Crystal Gem Blue costs the same count as the mould's normal ingot cost.
- The recipe produces the mould product's native output quantity.
- The output keeps its normal item identity and receives a registered Crystal physical material.
- Crystal inputs require the corresponding registered mould. Mixed ingredients and invalid or insufficient Crystal stacks are rejected without consuming them.
- Ordinary smelter recipes continue through the game's normal recipe path.

Mould entries with a missing product, nonpositive cost, nonpositive output quantity, or missing output prefab are logged and skipped. Recipe hashes are derived from mould hashes and checked against the game's live recipe registry before registration.

## Material, appearance, and configuration

The Crystal material takes gameplay stats from the physical material attached to the native Red Iron ingot; every stat defaults to that installed game's Red Iron value. Its renderer materials and material channels use the vanilla Iron template, matching the Crystal Repair Hammer appearance setup, then receive the same ice-blue tint and emission. The client companion reapplies the color after the game assigns its atlas materials and keeps the color visible during heat updates; this does not change actual item temperature.

MelonLoader creates a `CrystalWeapons` preferences category displayed as **Crystal Weapons**. Each gameplay override uses the matching game field name plus `Override`. Set a float override to `-1` to inherit Red Iron, or to a finite nonnegative number to replace it. `hardnessLevelOverride` uses the same `-1` inherit value and accepts nonnegative integers. The supplied `CrystalWeapons.example.cfg` contains only this mod's settings and uses the Red Iron defaults. When upgrading from an older release, rename the preferences section to `[CrystalWeapons]` so existing custom overrides carry over.

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

## Build

```bash
./build.sh
```

The script builds `CrystalWeapons.dll` against the installed A Township Tale managed assemblies and writes the Release artifact to `Crystal Weapons Build/CrystalWeapons.dll`. Set `GAME_PATH` if the game files are installed elsewhere, or `DOTNET` to select a .NET SDK executable.

The separate client project links only the material, appearance, and config sources used by the server project. Run `./build-client.sh` to build its Release artifact separately.

This project contains no Repair Hammer repair behavior and does not modify the source RepairHammer project.
