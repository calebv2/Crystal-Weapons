using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CrystalWeapons.Core), "Crystal Weapons", "2.0", "ATT", null)]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace CrystalWeapons;

public sealed class Core : MelonMod
{
    internal static MelonLogger.Instance Logger { get; private set; } = null!;
    private bool clientRuntimeEnabled;

    public override void OnInitializeMelon()
    {
        Logger = LoggerInstance;
        if (IsServerRuntime())
        {
            Logger.Warning("Crystal Weapons is client-only and will remain disabled in this server runtime.");
            return;
        }

        CrystalForgeConfig.Initialize();
        var crystalHarmony = new HarmonyLib.Harmony("ATT.CrystalWeapons.Client");
        CrystalClientAppearancePatch.Install(crystalHarmony);
        clientRuntimeEnabled = true;
        Logger.Msg("Crystal Weapons initialized in client mode; shared Crystal material registration and renderer appearance are enabled.");
    }

    public override void OnLateInitializeMelon()
    {
        if (!clientRuntimeEnabled || IsServerRuntime()) return;

        try
        {
            var material = CrystalMaterialRegistration.CreateAndRegister();
            Logger.Msg("Crystal Weapons registered shared material hash " + material.Hash + " on the client.");
        }
        catch (Exception exception)
        {
            Logger.Error("Crystal Weapons material registration failed on the client: " + exception);
        }
    }

    private static bool IsServerRuntime()
    {
        return Application.isBatchMode || NetworkSceneManager.IsServer || ServerAssemblyIsLoaded();
    }

    private static bool ServerAssemblyIsLoaded()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(assembly.GetName().Name, "CrystalWeapons", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
