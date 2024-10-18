using System.Collections.Generic;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using ECommons.Interop;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;
using PInvoke;
using Splatoon.SplatoonScripting;

namespace SplatoonScriptsOfficial.Generic;

public class TPClick : SplatoonScript
{
    private bool _isTpOnce;
    public override HashSet<uint>? ValidTerritories => [];
    public override Metadata? Metadata => new(1, "Garume");

    private Config C => Controller.GetConfig<Config>();

    public override void OnSettingsDraw()
    {
        ImGuiEx.Text(Loc("Teleport to your mouse location on click while CTRL is held.",
            "CTRLを押しながらクリックするとマウスの場所にテレポートします。"));
        ImGui.Text("Active: ");
        ImGui.SameLine();
        if (C.IsActive)
            ImGui.TextColored(EColor.GreenBright, "True");
        else
            ImGui.TextColored(EColor.RedBright, "False");

        ImGui.Separator();
        if (ImGui.Button("Toggle")) C.IsActive = !C.IsActive;

        if (ImGuiEx.CollapsingHeader("Debug"))
        {
            ImGui.Text("Player position: " + Player.Position);

            unsafe
            {
                ImGui.Text("WindowInactive: " + Framework.Instance()->WindowInactive);
            }
            ImGui.Text("IsMouseLeftClicked: " + IsMouseLeftClicked);;
            ImGui.Text("IsCtrlPressed: " + ImGui.GetIO().KeyCtrl);
            
        }
    }

    public bool IsMouseLeftClicked => Bitmask.IsBitSet(User32.GetKeyState((int)LimitedKeys.LeftMouseButton), 15);

    public override unsafe void OnUpdate()
    {
        if (!C.IsActive)
            return;

        if (Framework.Instance()->WindowInactive)
            return;

        if (IsMouseLeftClicked && ImGui.GetIO().KeyCtrl)
        {
            var pos = ImGui.GetMousePos();
            if (Svc.GameGui.ScreenToWorld(pos, out var worldPos))
                if (!_isTpOnce)
                {
                    Player.GameObject->SetPosition(worldPos.X, worldPos.Y, worldPos.Z);
                    _isTpOnce = true;
                }
        }
        else
        {
            _isTpOnce = false;
        }
    }

    public class Config : IEzConfig
    {
        public bool IsActive;
    }
}