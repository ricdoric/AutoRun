using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using HarmonyLib;

namespace AutoRun;

public class AutoRunModSystem : ModSystem
{
    public static ICoreClientAPI Api { get; private set; }
    private Harmony _harmony;
    private static bool isRunning = false;
    private static bool walkTriggerOnUpAlsoOriginal = false;
    private static bool _startingRun = false;
    internal static bool _suppressNextWalkUp = false;
    private static bool _debug = false;

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        Api = api;

        walkTriggerOnUpAlsoOriginal = Api.Input.HotKeys["walkforward"].TriggerOnUpAlso;
        Api.Input.HotKeys["walkforward"].TriggerOnUpAlso = false;
        Api.Input.SetHotKeyHandler("walkforward", OnWalkForwardHotkey);

        Api.Input.RegisterHotKey("autorun", "Auto Run", GlKeys.V, HotkeyType.CharacterControls);
        Api.Input.SetHotKeyHandler("autorun", OnAutoRunHotkey);

        Api.Event.RegisterGameTickListener(OnTick, 0);

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
        if (isRunning) StopRunning();
        if (Api != null)
            Api.Input.HotKeys["walkforward"].TriggerOnUpAlso = walkTriggerOnUpAlsoOriginal;
        Api = null;
        _harmony.UnpatchAll("com.AutoRun");
        base.Dispose();
    }

    // Key handlers

    private bool OnAutoRunHotkey(KeyCombination comb)
    {
        if (isRunning) StopRunning();
        else StartRunning();
        return true;
    }

    private bool OnWalkForwardHotkey(KeyCombination comb)
    {
        if (isRunning && !_startingRun) StopRunning(continueWalking: true);
        return false;
    }

    private static void StartRunning(bool sendKeyDown = true)
    {
        isRunning = true;
        if (sendKeyDown)
        {
            if (Api.Input.KeyboardKeyState[GetWalkKeyCode()])
            {
                // if walkforward key is already being held while activating autorun,
                // suppress the next keyup event for walkforward
                _suppressNextWalkUp = true;
            }
            else
            {
                _startingRun = true;
                (Api.World as ClientMain)?.OnKeyDown(new KeyEvent { KeyCode = GetWalkKeyCode() });
                _startingRun = false;
            }
        }
        DebugMessage("AutoRun: ON");
    }

    private static void StopRunning(bool continueWalking = false)
    {
        isRunning = false;
        (Api.World as ClientMain)?.OnKeyUp(new KeyEvent { KeyCode = GetWalkKeyCode() });
        if (continueWalking)
            (Api.World as ClientMain)?.OnKeyDown(new KeyEvent { KeyCode = GetWalkKeyCode() });
        DebugMessage("AutoRun: OFF");
    }

    private void OnTick(float dt)
    {
        if (!isRunning) return;
        if (Api.Input.KeyboardKeyState[GetWalkBackKeyCode()])
            StopRunning();
    }

    internal static int GetWalkKeyCode()
        => Api.Input.HotKeys["walkforward"].CurrentMapping.KeyCode;

    internal static int GetWalkBackKeyCode()
        => Api.Input.HotKeys["walkbackward"].CurrentMapping.KeyCode;

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
        if (args.KeyCode == AutoRunModSystem.GetWalkKeyCode() && AutoRunModSystem._suppressNextWalkUp)
        {
            AutoRunModSystem._suppressNextWalkUp = false;
            return false;
        }
        return true;
    }
}
