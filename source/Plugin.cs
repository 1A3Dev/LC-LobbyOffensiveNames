using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
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

        internal static ManualLogSource logSource;

        private void Awake()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            Instance = this;
            logSource = Logger;

            OffensiveNamesConfig.InitConfig();

            Assembly patches = Assembly.GetExecutingAssembly();
            harmony.PatchAll(patches);
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
            PluginLoader.Instance.BindConfig(ref FilterTerms, "Settings", "Filter Terms", "nigger,faggot,n1g,nigers,cunt,pussies,pussy,minors,chink,buttrape,molest,rape,coon,negro,beastiality,cocks,cumshot,ejaculate,pedophile,furfag,necrophilia,yiff,sex,nigga", "This should be a comma-separated list. Leaving this blank will also disable the filter.");
            BlockedTermsRaw = FilterTerms.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            FilterTerms.SettingChanged += (sender, args) =>
            {
                BlockedTermsRaw = FilterTerms.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            };
        }
    }

    [HarmonyPatch]
    internal static class SteamLobbyManagerStart_Patch
    {
        [HarmonyPatch(typeof(SteamLobbyManager), "OnEnable")]
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
            int skip = 0;
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (skip-- > 0) {
                    continue;
                }

                // check for IL_0022: ldc.i4.s
                int arrayCount = 23;
                if (instruction.opcode == OpCodes.Ldc_I4_S && (sbyte)instruction.operand == arrayCount)
                {
                    // replace entire new array op codes with an array pointer
                    newInstructions.Add(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(OffensiveNamesConfig), "BlockedTermsRaw")));

                    // skip all array codes but keep:
                    // IL_00EF: stfld     string[] SteamLobbyManager/'<loadLobbyListAndFilter>d__20'::'<offensiveWords>5__2'
                    skip = arrayCount * 4 + 1;

                    alreadyReplaced = true;
                    continue;
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) PluginLoader.logSource.LogWarning($"loadLobbyListAndFilter_Patch failed to replace offensiveWords");

            return newInstructions.AsEnumerable();
        }
    }
}