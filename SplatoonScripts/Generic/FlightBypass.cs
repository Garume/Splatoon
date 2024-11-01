using System.Collections.Generic;
using ECommons.EzHookManager;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Splatoon.SplatoonScripting;

namespace SplatoonScriptsOfficial.Generic;

public class FlightBypass : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => [];
    public override Metadata? Metadata => new(1, "Garume");

    private delegate nint IsFlightProhibited(nint a1);
    [EzHook("E8 ?? ?? ?? ?? 85 C0 74 07 32 C0 48 83 C4 38", false)]
    private readonly EzHook<IsFlightProhibited> IsFlightProhibitedHook = null!;

    public override void OnSettingsDraw()
    {
        ImGui.Text("Bypasses flight restrictions in all zones where it's possible to fly.");
    }

    public override void OnEnable()
    {
        IsFlightProhibitedHook.Enable();
    }
    
    public override void OnDisable()
    {
        IsFlightProhibitedHook.Disable();
    }
}