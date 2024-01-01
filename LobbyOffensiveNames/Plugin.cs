using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LobbyInviteOnly
{
    [BepInPlugin(modGUID, "LobbyOffensiveNames", modVersion)]
    internal class PluginLoader : BaseUnityPlugin
    {
        private const string modGUID = "Dev1A3.LobbyOffensiveNames";

        private readonly Harmony harmony = new Harmony(modGUID);

        private const string modVersion = "1.0.1";

        private static bool initialized;

        public static PluginLoader Instance { get; private set; }

        private void Awake()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            Instance = this;
            Assembly patches = Assembly.GetExecutingAssembly();
            harmony.PatchAll(patches);

            OffensiveNamesConfig.InitConfig();
        }

        public void BindConfig<T>(ref ConfigEntry<T> config, string section, string key, T defaultValue, string description = "")
        {
            config = ((BaseUnityPlugin)this).Config.Bind<T>(section, key, defaultValue, description);
        }
    }
    internal class OffensiveNamesConfig
    {
        public static ConfigEntry<bool> FilterEnabled;
        public static ConfigEntry<string> FilterTerms;
        public static string[] BlockedTermsRaw;

        public static void InitConfig()
        {
            PluginLoader.Instance.BindConfig(ref FilterEnabled, "Settings", "Filter Enabled", true, "Should the offensive lobby name filter be enabled?");
            PluginLoader.Instance.BindConfig(ref FilterTerms, "Settings", "Filter Terms", "nigger,nigga,n1g,nigers,negro,faggot,minors,chink,buttrape,molest,beastiality,cocks,cumshot,ejaculate,pedophile,furfag,necrophilia", "This should be a comma-separated list. Leaving this blank will also disable the filter.");
            BlockedTermsRaw = FilterTerms.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        }
    }

    [HarmonyPatch]
    internal static class SteamLobbyManagerStart_Patch
    {
        [HarmonyPatch(typeof(SteamLobbyManager), "Start")]
        [HarmonyPrefix]
        private static void Prefix(ref SteamLobbyManager __instance)
        {
            __instance.censorOffensiveLobbyNames = OffensiveNamesConfig.FilterEnabled.Value && OffensiveNamesConfig.BlockedTermsRaw.Length > 0;
        }
    }

    [HarmonyPatch]
    public static class loadLobbyListAndFilter_Patch
    {
        [HarmonyPatch(typeof(SteamLobbyManager), "loadLobbyListAndFilter", MethodType.Enumerator)]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TranspileMoveNext(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            var skip = 0;
            foreach (var instruction in instructions)
            {
                if (skip-- > 0) continue;

                // check for IL_0021
                if (instruction.opcode == OpCodes.Ldc_I4_S && (sbyte)instruction.operand == 21)
                {
                    Debug.Log("Replaced offensiveWords");
                    // replace entire new array op codes with an array pointer
                    newInstructions.Add(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(OffensiveNamesConfig), "BlockedTermsRaw")));

                    // skip all array codes but keep:
                    // IL_00dd: stfld        string[] SteamLobbyManager/'<loadLobbyListAndFilter>d__15'::'<offensiveWords>5__2'
                    skip = 21 * 4 + 1;

                    continue;
                }

                newInstructions.Add(instruction);
            }

            return newInstructions.AsEnumerable();
        }
    }
}