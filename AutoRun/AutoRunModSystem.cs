using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using HarmonyLib;
using System;

namespace AutoRun;

public class AutoRunModSystem : ModSystem
{
    public static ICoreClientAPI Api { get; private set; }
    private Harmony _harmony;
    internal static bool isAutoMoving = false;
    internal static bool isAutoSprinting = false;
    private static bool walkTriggerOnUpAlsoOriginal = false;
    private static bool _startingRun = false;
    internal static bool _suppressNextWalkUp = false;
    private static bool _debug = true;

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        Api = api;

        walkTriggerOnUpAlsoOriginal = Api.Input.HotKeys["walkforward"].TriggerOnUpAlso;
        Api.Input.HotKeys["walkforward"].TriggerOnUpAlso = false;
        Api.Input.SetHotKeyHandler("walkforward", OnWalkForwardHotkey);
        Api.Input.SetHotKeyHandler("sprint", OnSprintHotkey);

        Api.Input.RegisterHotKey(
            hotkeyCode: "autorun",
            name: "(AutoRun) Toggle auto forward movement",
            key: GlKeys.V,
            type: HotkeyType.CharacterControls
        );
        Api.Input.SetHotKeyHandler("autorun", OnAutoRunHotkey);

        Api.Event.RegisterGameTickListener(OnGameTick, 0);

        Api.ChatCommands.Create("autorun")
            .WithDescription("AutoRun mod settings")
            .BeginSubCommand("debug")
                .WithDescription("Toggle debug messages")
                .HandleWith(OnDebugCommand)
            .EndSubCommand();


        _harmony = new Harmony("com.AutoRun");
        _harmony.PatchAll();

        DebugMessage("AutoRun mod loaded v adding back in harmony :)");
    }

    public override void Dispose()
    {
        if (isAutoMoving) StopRunning();
        if (Api != null)
            Api.Input.HotKeys["walkforward"].TriggerOnUpAlso = walkTriggerOnUpAlsoOriginal;
        Api = null;
        _harmony.UnpatchAll("com.AutoRun");
        base.Dispose();
    }

    // Key handlers

    private bool OnAutoRunHotkey(KeyCombination comb)
    {
        if (isAutoMoving) StopRunning();
        else StartRunning();
        return true;
    }

    private bool OnWalkForwardHotkey(KeyCombination comb)
    {
        if (isAutoMoving && !_startingRun) StopRunning(continueWalking: true);
        return false;
    }

    private bool OnSprintHotkey(KeyCombination comb)
    {
        if (isAutoMoving)
        {
            if (!isAutoSprinting)
            {
                isAutoSprinting = true;
                (Api.World as ClientMain)?.OnKeyDown(new KeyEvent { KeyCode = sprintKey() });
                DebugMessage("AutoRun: SPRINT ON");
            }
        }
        else
        {
            if (isAutoSprinting)
            {
                isAutoSprinting = false;
                (Api.World as ClientMain)?.OnKeyUp(new KeyEvent { KeyCode = sprintKey() });
                DebugMessage("AutoRun: SPRINT OFF");
            }
        }
        return true;
    }

    private static void StartRunning(bool sendKeyDown = true)
    {
        isAutoMoving = true;
        if (sendKeyDown)
        {
            if (Api.Input.KeyboardKeyState[walkforwardKey()])
            {
                // if walkforward key is already being held while activating autorun,
                // suppress the next keyup event for walkforward
                _suppressNextWalkUp = true;
            }
            else
            {
                _startingRun = true;
                (Api.World as ClientMain)?.OnKeyDown(new KeyEvent { KeyCode = walkforwardKey() });
                _startingRun = false;
            }
        }
        DebugMessage("AutoRun: ON");
    }

    private static void StopRunning(bool continueWalking = false)
    {
        if (!isAutoMoving) return;

        isAutoMoving = false;

        if (isAutoSprinting)
        {
            isAutoSprinting = false;
            (Api.World as ClientMain)?.OnKeyUp(new KeyEvent { KeyCode = sprintKey() });
        }

        _suppressNextWalkUp = false;
        (Api.World as ClientMain)?.OnKeyUp(new KeyEvent { KeyCode = walkforwardKey() });

        if (continueWalking)
            (Api.World as ClientMain)?.OnKeyDown(new KeyEvent { KeyCode = walkforwardKey() });

        DebugMessage("AutoRun: OFF");
    }

    private void OnGameTick(float dt)
    {
        if (!isAutoMoving) return;
        if (Api.Input.KeyboardKeyState[walkbackwardKey()])
            StopRunning();
    }

    internal static int walkforwardKey()
        => Api.Input.HotKeys["walkforward"].CurrentMapping.KeyCode;

    internal static int walkbackwardKey()
        => Api.Input.HotKeys["walkbackward"].CurrentMapping.KeyCode;

    internal static int sprintKey()
        => Api.Input.HotKeys["sprint"].CurrentMapping.KeyCode;

    private TextCommandResult OnDebugCommand(TextCommandCallingArgs args)
    {
        _debug = !_debug;
        return TextCommandResult.Success($"AutoRun debug messages: {(_debug ? "ON" : "OFF")}");
    }

    private static void DebugMessage(string msg)
    {
        if (_debug) Api.ShowChatMessage(msg);
    }
}

[HarmonyPatch]
internal static class AutoRunPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClientMain), "OnKeyUp")]
    public static bool Before_ClientMain_OnKeyUp(KeyEvent args)
    {
        if (args.KeyCode == AutoRunModSystem.walkforwardKey() && AutoRunModSystem._suppressNextWalkUp && AutoRunModSystem.isAutoMoving)
        {
            AutoRunModSystem._suppressNextWalkUp = false;
            return false;
        }

        if (args.KeyCode == AutoRunModSystem.sprintKey() && AutoRunModSystem.isAutoSprinting)
        {
            // Keep sprint/Ctrl depressed while auto-sprinting by suppressing key-up events.
            return false;
        }

        return true;
    }
}
