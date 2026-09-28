using System;
using MelonLoader;

[assembly: MelonInfo(typeof(CrystalWeapons.Core), "Crystal Weapons", "2.0", "ATT", null)]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace CrystalWeapons;

public sealed class Core : MelonMod
{
    internal static MelonLogger.Instance Logger { get; private set; } = null!;

    public override void OnInitializeMelon()
    {
        Logger = LoggerInstance;
        CrystalForgeConfig.Initialize();
        Logger.Msg("Crystal Weapons initialized.");
    }

    public override void OnLateInitializeMelon()
    {
        try
        {
            CrystalMaterialRegistration.CreateAndRegister();
            var targets = CrystalMouldRecipeRegistration.Register();
            if (targets.Count == 0)
            {
                Logger.Warning("Crystal Weapons did not register any valid mould products.");
            }

            Logger.Msg("Crystal Weapons initialized with " + targets.Count + " Crystal mould products.");
        }
        catch (Exception exception)
        {
            Logger.Error("Crystal Weapons late initialization failed: " + exception);
        }
    }
}
