using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Steamworks.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyInviteOnly;

[BepInPlugin(modGUID, "LobbyInviteOnly", modVersion)]
internal class PluginLoader : BaseUnityPlugin
{
    private const string modGUID = "Dev1A3.LobbyInviteOnly";

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
    }
}

[HarmonyPatch]
class Patch
{
    public static bool isLobbyInviteOnly = false;
    public static Animator setInviteOnlyButtonAnimator;

    private static void SetLobbyInviteOnly(ref MenuManager __instance)
    {
        if (GameNetworkManager.Instance.disableSteam)
        {
            return;
        }

        __instance.hostSettings_LobbyPublic = false;
        isLobbyInviteOnly = true;
        __instance.setPrivateButtonAnimator.SetBool("isPressed", false);
        __instance.setPublicButtonAnimator.SetBool("isPressed", false);
        setInviteOnlyButtonAnimator.SetBool("isPressed", true);
        __instance.privatePublicDescription.text = "INVITE ONLY means you must send invites through Steam for players to join.";
    }

    [HarmonyPatch(typeof(MenuManager), "ClickHostButton")]
    [HarmonyPrefix]
    private static bool MenuManagerClickHostButton(MenuManager __instance)
    {
        __instance.HostSettingsScreen.SetActive(value: true);
        if (GameNetworkManager.Instance.disableSteam)
        {
            __instance.HostSettingsOptionsLAN.SetActive(value: true);
            __instance.HostSettingsOptionsNormal.SetActive(value: false);
        }

        if ((bool)Object.FindObjectOfType<SaveFileUISlot>())
        {
            Object.FindObjectOfType<SaveFileUISlot>().SetButtonColorForAllFileSlots();
        }

        if (isLobbyInviteOnly)
        {
            SetLobbyInviteOnly(ref __instance);
        } else
        {
            __instance.HostSetLobbyPublic(__instance.hostSettings_LobbyPublic);
        }

        return false;
    }

    [HarmonyPatch(typeof(MenuManager), "Start")]
    [HarmonyPostfix]
    private static void MenuManagerStart(MenuManager __instance)
    {
        if (GameNetworkManager.Instance.disableSteam)
        {
            return;
        }

        float height = 14.5f;
        GameObject publicButtonObject = GameObject.Find("/Canvas/MenuContainer/LobbyHostSettings/Panel/LobbyHostOptions/OptionsNormal/Public");
        if (publicButtonObject != null)
        {
            height = publicButtonObject.GetComponent<RectTransform>().localPosition.y;

            publicButtonObject.GetComponent<RectTransform>().localScale = new Vector3(0.7f, 0.9f, 1f);
            publicButtonObject.GetComponent<RectTransform>().localPosition = new Vector3(-127f, height, 30f);
        }

        GameObject friendsButtonObject = GameObject.Find("/Canvas/MenuContainer/LobbyHostSettings/Panel/LobbyHostOptions/OptionsNormal/Private");
        if (friendsButtonObject != null)
        {
            friendsButtonObject.GetComponent<RectTransform>().localScale = new Vector3(0.7f, 0.9f, 1f);
            friendsButtonObject.GetComponent<RectTransform>().localPosition = new Vector3(40f, height, 30f);

            GameObject inviteOnlyButtonObject = Object.Instantiate(friendsButtonObject.gameObject, friendsButtonObject.transform.parent);
            inviteOnlyButtonObject.name = "InviteOnly";
            inviteOnlyButtonObject.GetComponent<RectTransform>().localPosition = new Vector3(127f, height, 30f);
            inviteOnlyButtonObject.GetComponentInChildren<TextMeshProUGUI>().text = "Invite-only";
            setInviteOnlyButtonAnimator = inviteOnlyButtonObject.GetComponent<Animator>();
            Button inviteOnlyButton = inviteOnlyButtonObject.GetComponent<Button>();
            inviteOnlyButton.onClick = new Button.ButtonClickedEvent();
            inviteOnlyButton.onClick.AddListener(() => {
                SetLobbyInviteOnly(ref __instance);
            });
        }
    }

    [HarmonyPatch(typeof(MenuManager), "HostSetLobbyPublic")]
    [HarmonyPostfix]
    private static void MenuManagerHostSetLobbyPublic(ref MenuManager __instance, bool setPublic = false)
    {
        if (GameNetworkManager.Instance.disableSteam)
        {
            return;
        }

        isLobbyInviteOnly = false;
        setInviteOnlyButtonAnimator.SetBool("isPressed", false);
        if (!setPublic)
        {
            __instance.privatePublicDescription.text = "FRIENDS ONLY means only friends or invited people can join.";
        }
    }

    [HarmonyPatch(typeof(GameNetworkManager), "SteamMatchmaking_OnLobbyCreated")]
    [HarmonyPostfix]
    internal static void SteamMatchmaking_OnLobbyCreated(GameNetworkManager __instance, Steamworks.Result result, Lobby lobby)
    {
        if (isLobbyInviteOnly)
        {
            __instance.lobbyHostSettings.isLobbyPublic = false;
            ((Lobby)lobby).SetPrivate();
        }
    }
}