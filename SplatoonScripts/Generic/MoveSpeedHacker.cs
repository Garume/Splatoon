using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Splatoon.SplatoonScripting;

namespace SplatoonScriptsOfficial.Generic;

internal class MoveSpeedHacker : SplatoonScript
{
    private Action<float> _onSpeedChange;
    public override HashSet<uint>? ValidTerritories => [];
    public override Metadata? Metadata => new(1, "Garume");

    private Config C => Controller.GetConfig<Config>();

    public override void OnEnable()
    {
        _onSpeedChange += SpeedChange;
    }

    public override void OnDisable()
    {
        _onSpeedChange -= SpeedChange;
    }

    public override void OnSettingsDraw()
    {
        ImGuiEx.Text("Modify your movement speed.");
        ImGui.Text("Active: ");
        ImGui.SameLine();
        if (C.IsActive)
            ImGui.TextColored(EColor.GreenBright, "True");
        else
            ImGui.TextColored(EColor.RedBright, "False");
        
        ImGui.Text("Current speed: " + (C.IsActive ? C.MovementSpeed : 1.0f).ToString("F"));

        ImGui.Separator();

        if (ImGui.Button("Toggle"))
        {
            C.IsActive = !C.IsActive;
            _onSpeedChange?.Invoke(C.IsActive ? C.MovementSpeed : 1.0f);
        }

        ImGui.Text("Movement speed:");
        ImGui.SameLine();
        var speed = C.MovementSpeed;
        ImGui.DragFloat("##Speed", ref C.MovementSpeed, 0.01f, 1f, 1.3f);
        if (!speed.Equals(C.MovementSpeed))
            _onSpeedChange?.Invoke(C.MovementSpeed);
    }

    private unsafe void SpeedChange(float baseSpeed)
    {
        var speed = 6 * baseSpeed;
        Svc.SigScanner.TryScanText(
            "F3 0F 59 05 ?? ?? ?? ?? F3 0F 59 05 ?? ?? ?? ?? F3 0F 58 05 ?? ?? ?? ?? 44 0F 28 C8", out var address);
        address = address + 4 + Marshal.ReadInt32(address + 4) + 4;
        SafeMemory.Write(address + 20, speed);
        SafeMemory.Write(
            ((delegate* unmanaged[Stdcall]<byte, nint>)Svc.SigScanner.ScanText(
                "E8 ?? ?? ?? ?? 48 85 C0 74 AE 83 FD 05"))(1) + 8, speed);
    }


    private class Config : IEzConfig
    {
        public bool IsActive;
        public float MovementSpeed = 1.0f;
    }
}