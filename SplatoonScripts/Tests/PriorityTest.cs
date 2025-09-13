﻿using ECommons;
using ECommons.Configuration;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.ImGuiMethods;
using ECommons.PartyFunctions;
using Dalamud.Bindings.ImGui;
using Splatoon.SplatoonScripting;
using Splatoon.SplatoonScripting.Priority;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplatoonScriptsOfficial.Tests;
public class PriorityTest : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [Dungeons.Sastasha];
    public override Metadata Metadata => new(1, "NightmareXIV");

    private Config C => Controller.GetConfig<Config>();

    public override void OnSettingsDraw()
    {
        try
        {
            ref var r = ref Ref<int>.Get(InternalData.FullName);
            ImGui.InputInt("num", ref r);
            ImGuiEx.Text($"Players with names <= {r}");
            var n = r;
            ImGuiEx.Text($"""
            List:
            {C.Priority.GetPlayers(x => x.Name.Length <= n)?.Select(x => x.Name).Print("\n")}
            
            Your index: {C.Priority.GetOwnIndex(x => x.Name.Length <= n)}
            Your index backwards: {C.Priority.GetOwnIndex(x => x.Name.Length <= n, true)}
            2nd player: {C.Priority.GetPlayer(x => x.Name.Length <= n, 2)}
            """);
        }
        catch(Exception e)
        {
            e.Log();
        }
        ImGui.Separator();
        C.Priority.Draw();
    }

    public class Config : IEzConfig
    {
        public PriorityData4 Priority = new();
    }

    public class PriorityData4 : PriorityData
    {
        public override int GetNumPlayers() => 4;
    }
    public class PriorityData1 : PriorityData
    {
        public override int GetNumPlayers() => 1;
    }
}
