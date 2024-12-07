using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.MathHelpers;
using ImGuiNET;
using Splatoon;
using Splatoon.SplatoonScripting;

namespace SplatoonScriptsOfficial.Duties.Dawntrail.The_Futures_Rewritten;

public class P5_Fulgent_Blade : SplatoonScript
{
    public enum Direction
    {
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        NorthWest
    }

    public enum State
    {
        None,
        Start,
        Blade1,
        Blade2,
        Blade3,
        Stack,
        End
    }

    private readonly Dictionary<Direction, Vector2> _bladePositions = new();
    private bool _isMirror;

    private Direction? _safeDirection;

    private State _state = State.None;

    public Direction[] ExpectedSafeDirections = [Direction.North, Direction.East, Direction.South, Direction.West];
    public override HashSet<uint>? ValidTerritories => [1238];
    public override Metadata? Metadata => new(1, "Garume");

    public IEnumerable<IEventObj> Blades =>
        Svc.Objects.Where(x => x.DataId == 0x1EBBF7).OfType<IEventObj>();

    public Config C => Controller.GetConfig<Config>();

    public override void OnSetup()
    {
        Controller.RegisterElement("Safe", new Element(0)
        {
            radius = 1f,
            tether = true,
            thicc = 6f
        });
    }
    

    public Direction GetDirection(Vector2 pos)
    {
        var isEast = pos.X > 101;
        var isWest = pos.X < 99;
        var isNorth = pos.Y < 99;
        var isSouth = pos.Y > 101;

        if (isNorth && isEast) return Direction.NorthEast;
        if (isNorth && isWest) return Direction.NorthWest;
        if (isSouth && isEast) return Direction.SouthEast;
        if (isSouth && isWest) return Direction.SouthWest;
        if (isNorth) return Direction.North;
        if (isEast) return Direction.East;
        if (isSouth) return Direction.South;
        if (isWest) return Direction.West;
        return Direction.North;
    }

    public void Reset()
    {
        _state = State.None;
        _bladePositions.Clear();
        _safeDirection = null;
        _waveCount = 0;
    }

    public override void OnReset()
    {
        Reset();
    }

    public override void OnStartingCast(uint source, uint castId)
    {
        if (castId == 40306)
        {
            Reset();
            _state = State.Start;
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (_state is State.None or State.End) return;
        if (set.Action.Value.RowId is 40308 or 40309)
        {
            _waveCount++;
            if (_waveCount == 4)
            {
                DrawBlade1();
                _state = State.Blade1;
            }

            if (_waveCount == 15)
            {
                DrawBlade2();
                _state = State.Blade2;
            }

            if (_waveCount == 31)
            {
                DrawBlade3();
                _state = State.Blade3;
            }

            if (_waveCount > 40)
            {
                DrawStack();
                _state = State.Stack;
            }
        }
        
        if (set.Action.Value.RowId == 40310)
        {
            _state = State.End;
        }
    }

    public override void OnUpdate()
    {
        if (_state is State.None or State.End) Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
        else
            Controller.GetRegisteredElements().Each(x => x.Value.color = GradientColor.Get(C.BaitColor1, C.BaitColor2).ToUint());
        if (_state == State.Start && _bladePositions.Count() != 6)
        {
            foreach (var blade in Blades)
            {
                var pos = blade.Position.ToVector2();
                var dir = GetDirection(pos);
                DuoLog.Error($"Blade {blade.Name} is in {pos}");
                _bladePositions[dir] = pos;
            }

            if (_bladePositions.Count() == 6)
            {
                var excluded = Enum.GetValues<Direction>().Where(x => !_bladePositions.ContainsKey(x)).ToArray();
                _safeDirection = ExpectedSafeDirections.FirstOrDefault(x => !_bladePositions.ContainsKey(x));
                
                if (excluded.Contains(Direction.West) && excluded.Contains(Direction.NorthWest))
                    _isMirror = true;
                else if (excluded.Contains(Direction.North) && excluded.Contains(Direction.NorthEast))
                    _isMirror = true;
                else if (excluded.Contains(Direction.East) && excluded.Contains(Direction.SouthEast))
                    _isMirror = true;
                else if (excluded.Contains(Direction.South) && excluded.Contains(Direction.SouthWest))
                    _isMirror = true;
                else _isMirror = false;
                
                DrawStartPosition();
            }
        }
    }
    
    private int _waveCount = 0;

    public void DrawStartPosition()
    {
        var position = CalculatePosition(new Vector2(-3, 8));
        if (Controller.TryGetElementByName("Safe", out var safe))
        {
            safe.Enabled = true;
            safe.SetRefPosition(position.ToVector3(0));
        }
    }
    
    public  Vector2 CalculatePosition( Vector2 offset)
    {
        Vector2 center = new Vector2(100, 100); 

        return _safeDirection switch
        {
            Direction.North => center + new Vector2(_isMirror ? offset.X : -offset.X, -offset.Y),
            Direction.East => center + new Vector2(offset.Y, _isMirror ? -offset.X : offset.X),
            Direction.South => center + new Vector2(_isMirror ? -offset.X : offset.X, offset.Y),
            Direction.West => center + new Vector2(-offset.Y, _isMirror ? -offset.X : offset.X),
            _ => center 
        };
    }
    
    public void DrawBlade1()
    {
        var position = CalculatePosition(new Vector2(1, 6));
        if (Controller.TryGetElementByName("Safe", out var safe))
        {
            safe.Enabled = true;
            safe.SetRefPosition(position.ToVector3(0));
        }
    }
    
    
    public void DrawBlade2()
    {
        var position = CalculatePosition(new Vector2(-1, 3));
        if (Controller.TryGetElementByName("Safe", out var safe))
        {
            safe.Enabled = true;
            safe.SetRefPosition(position.ToVector3(0));
        }
    }
    
    public void DrawBlade3()
    {
        var position = CalculatePosition(new Vector2(-3, 8));
        if (Controller.TryGetElementByName("Safe", out var safe))
        {
            safe.Enabled = true;
            safe.SetRefPosition(position.ToVector3(0));
        }
    }
    
    public void DrawStack()
    {
        _isMirror = C.StackIsLeft;
        var position = CalculatePosition(new Vector2(4, 5)); 
        if (Controller.TryGetElementByName("Safe", out var safe))
        {
            safe.Enabled = true;
            safe.SetRefPosition(position.ToVector3(0));
        }
    }

    public override void OnSettingsDraw()
    {
        if (ImGuiEx.CollapsingHeader("General"))
        {
            ImGui.Checkbox("Stack is Left", ref C.StackIsLeft);
            ImGui.Text("Bait Color:");
            ImGui.Indent();
            ImGui.ColorEdit4("##BaitColor1", ref C.BaitColor1, ImGuiColorEditFlags.NoInputs);
            ImGui.SameLine();
            ImGui.ColorEdit4("##BaitColor2", ref C.BaitColor2, ImGuiColorEditFlags.NoInputs);
            ImGui.Unindent();
        }

        if (ImGuiEx.CollapsingHeader("Debug"))
        {
            ImGui.Text($"State: {_state}");
            ImGui.Text($"Wave Count: {_waveCount}");
            ImGuiEx.Text($"Is Mirror: {_isMirror}");
            ImGuiEx.Text(
                $"Excluded Directions: {Enum.GetValues<Direction>().Where(x => !_bladePositions.ContainsKey(x)).Select(x => x.ToString()).Join(",")}");
            ImGui.Text($"Safe Direction: {_safeDirection}");
            foreach (var blade in _bladePositions) ImGui.Text($"{blade.Key}: {blade.Value}");
        }
    }


    public class Config : IEzConfig
    {
        public Vector4 BaitColor1 = 0xFFFF00FF.ToVector4();
        public Vector4 BaitColor2 = 0xFFFFFF00.ToVector4();
        public bool StackIsLeft = false;
    }
}
