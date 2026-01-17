using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.LanguageHelpers;
using Newtonsoft.Json;
using Splatoon.SplatoonScripting;

namespace Splatoon.Gui.Scripting;

internal static class TabScripting
{
    internal static volatile bool ForceUpdate = false;
    internal static string Search = "";
    internal static string RequestOpen = null;
    private const int ScriptConfigUndoLimit = 10;
    private const float ScriptConfigUndoIdleSeconds = 0.4f;
    private static readonly Dictionary<string, ScriptConfigUndoState> ScriptConfigUndoStates = [];
    internal static void Draw()
    {
        if(ImGui.IsWindowAppearing()) RequestOpen = null;
        if(ScriptingProcessor.ThreadIsRunning)
        {
            ImGuiEx.LineCentered("ThreadCompilerRunning", delegate
            {
                ImGuiEx.Text(GradientColor.Get(ImGuiColors.DalamudWhite, ImGuiColors.ParsedPink), "Scripts are being installed, please wait...".Loc());
            });
        }
        else
        {
            ImGuiEx.TextWrapped(ImGuiColors.DalamudOrange, "Please note that scripts have direct and unrestricted access to your PC and game. Ensure that you know what you're installing.".Loc());
        }
        var force = ForceUpdate;
        if(ImGui.Checkbox($"Force Update".Loc(), ref force)) ForceUpdate = force;
        ImGuiEx.Tooltip("Enable this checkbox and click \"Reload and Update\" button to forcibly redownload all scripts, even should they have no updates, that were installed from the Internet.");
        ImGui.SameLine();
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Undo, "Reload and Update".Loc()))
        {
            var dir = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "ScriptCache");
            foreach(var x in Directory.GetFiles(dir))
            {
                if(x.EndsWith(".bin"))
                {
                    PluginLog.Information($"Deleting {x}");
                    File.Delete(x);
                }
            }
            ScriptingProcessor.ReloadAll();
        }
        ImGuiEx.Tooltip("Clears cache, recompiles and reloads all scripts and checks them for updates immediately.");
        ImGui.SameLine();
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Paste, "Install from Clipboard".Loc()))
        {
            var text = ImGui.GetClipboardText();
            if(ScriptingProcessor.IsUrlTrusted(text))
            {
                ScriptingProcessor.DownloadScript(text, false);
            }
            else
            {
                ScriptingProcessor.CompileAndLoad(text, null, false, true);
            }
        }
        ImGuiEx.Tooltip("Installs script from clipboard. Your clipboard should contain either code of the script or link to a trusted URL (a script from Splatoon repository)");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2.05f);
        ImGui.InputTextWithHint("##search", "Search...", ref Search, 50);
        ImGui.SameLine();
        ImGuiEx.SetNextItemFullWidth();
        if(ImGui.BeginCombo("##switch", "Switch scripts to configuration profile"))
        {
            var confs = new HashSet<string>();
            var toReload = new HashSet<SplatoonScript>();
            foreach(var s in ScriptingProcessor.Scripts)
            {
                if(s.IsDisabledByUser) continue;
                if(s.TryGetAvailableConfigurations(out var confList))
                {
                    foreach(var c in confList)
                    {
                        confs.Add(c.Value);
                    }
                }
            }
            if(ImGui.Selectable("Default Configuration"))
            {
                foreach(var s in ScriptingProcessor.Scripts)
                {
                    if(s.InternalData.CurrentConfigurationKey != "")
                    {
                        s.ApplyDefaultConfiguration(out var act);
                        if(act != null) toReload.Add(s);
                    }
                }
            }
            var i = 0;
            foreach(var confName in confs.Order())
            {
                var doReload = false;
                if(ImGui.Selectable($"{confName}"))
                {
                    doReload = true;
                }
                var sb = new StringBuilder("The following scripts will be switched:\n");
                foreach(var s in ScriptingProcessor.Scripts)
                {
                    if(s.IsDisabledByUser) continue;
                    if(P.Config.DefaultScriptConfigurationNames.TryGetValue(s.InternalData.FullName, out var defConName) && defConName == confName)
                    {
                        if(s.InternalData.CurrentConfigurationKey != "")
                        {
                            if(doReload)
                            {
                                s.ApplyDefaultConfiguration(out var act);
                                if(act != null) toReload.Add(s);
                            }
                            sb.Append(s.InternalData.FullName.Replace(".", " - "));
                            sb.Append('\n');
                        }
                    }
                    else if(s.TryGetAvailableConfigurations(out var confList) && confList.FindKeysByValue(confName).TryGetFirst(out var confKey) && s.InternalData.CurrentConfigurationKey != confKey)
                    {
                        if(doReload)
                        {
                            s.ApplyConfiguration(confKey, out var act);
                            if(act != null) toReload.Add(s);
                        }
                        sb.Append(s.InternalData.FullName.Replace(".", " - "));
                        sb.Append('\n');
                    }
                }
                ImGuiEx.Tooltip(sb.ToString());
            }
            if(toReload.Count > 0)
            {
                ScriptingProcessor.ReloadScripts(toReload, false);
            }
            ImGui.EndCombo();
        }
        ImGuiEx.Tooltip("Disabled scripts won't be switched.");

        var openConfig = ScriptingProcessor.Scripts.FirstOrDefault(x => x.InternalData.ConfigOpen);

        if(openConfig != null)
        {
            RequestOpen = null;
            DrawScriptGroup([openConfig]);
        }
        else
        {
            if(RequestOpen != null)
            {
                var candidate = ScriptingProcessor.Scripts.FirstOrDefault(x => x.InternalData?.FullName == RequestOpen);
                if(candidate != null)
                {
                    candidate.InternalData.ConfigOpen = true;
                    RequestOpen = null;
                }
            }
            if(Search != "")
            {
                DrawScriptGroup(ScriptingProcessor.Scripts);
            }
            var namespaces = ScriptingProcessor.Scripts.Select(x => x.InternalData.Namespace).Distinct().Order();
            foreach(var nsp in namespaces)
            {
                ImGuiEx.TreeNodeCollapsingHeader(nsp.Replace("_", " ").Replace(".", " - "), () =>
                {
                    ImGui.PushID(nsp);
                    DrawScriptGroup(ScriptingProcessor.Scripts.Where(x => x.InternalData.Namespace == nsp).OrderBy(x => x.InternalData.Name));
                    ImGui.PopID();
                });
            }
        }

        void DrawScriptGroup(IEnumerable<SplatoonScript> scripts)
        {
            if(ImGui.BeginTable("##scriptsTable", 7, ImGuiTableFlags.BordersInner | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Configuration", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("State");
                ImGui.TableSetupColumn("##c1");
                ImGui.TableSetupColumn("##c2");
                ImGui.TableSetupColumn("##c3");
                ImGui.TableSetupColumn("##c4");
                ImGui.TableHeadersRow();
                foreach(var script in scripts)
                {
                    var searchSplot = Search.Split(",", StringSplitOptions.TrimEntries);
                    if(!(Search == "" || script.InternalData.Name.ContainsAny(StringComparison.OrdinalIgnoreCase, searchSplot) || script.InternalData.Namespace.ContainsAny(StringComparison.OrdinalIgnoreCase, searchSplot))) continue;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.PushID(script.InternalData.GUID);
                    ImGuiEx.TextV($"{script.InternalData.Name.Replace("_", " ")}");
                    if(script.Metadata?.Description == null)
                    {
                        ImGuiEx.Tooltip($"{script.InternalData.Namespace}");
                    }
                    else
                    {
                        ImGuiEx.Tooltip($"{script.InternalData.Namespace}\n{script.Metadata.Description}");
                    }
                    if(ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        ImGui.SetClipboardText($"{script.InternalData.FullName}");
                        Notify.Success("Copied to clipboard");
                    }
                    if(script.Metadata?.Version != null)
                    {
                        ImGui.SameLine();
                        ImGuiEx.Text(ImGuiColors.DalamudGrey2, $"v{script.Metadata.Version}");
                    }
                    if(script.Metadata?.Author != null)
                    {
                        ImGui.SameLine();
                        ImGuiEx.Text(ImGuiColors.DalamudGrey2, $"by {script.Metadata.Author}");
                    }

                    ImGui.TableNextColumn();

                    script.DrawConfigurationSelector();

                    ImGui.TableNextColumn();

                    if(script.InternalData.Blacklisted)
                    {
                        ImGuiEx.TextV(ImGuiColors.DalamudGrey3, "Blacklisted".Loc());
                        ImGuiComponents.HelpMarker("This script was blacklisted due to compatibility issues. Please wait for it's new version to be released.".Loc());
                    }
                    else if(!script.InternalData.Allowed)
                    {
                        ImGuiEx.TextV(ImGuiColors.ParsedGold, "Preparing".Loc());
                        ImGuiComponents.HelpMarker("This script is being prepared for enabling and will be available shortly.".Loc());
                    }
                    else if(script.IsDisabledByUser)
                    {
                        ImGuiEx.TextV(ImGuiColors.DalamudRed, "Disabled".Loc());
                        ImGuiComponents.HelpMarker("This script has been disabled by you.".Loc());
                    }
                    else if(script.IsEnabled)
                    {
                        ImGuiEx.TextV(ImGuiColors.ParsedGreen, "Active".Loc());
                        ImGuiComponents.HelpMarker("This script is currently active and being executed.".Loc());
                    }
                    else
                    {
                        ImGuiEx.TextV(ImGuiColors.DalamudYellow, "Inactive".Loc());
                        ImGuiComponents.HelpMarker("This script is currently inactive because you're not in a zone for which it was designed.".Loc());
                    }
                    ImGui.TableNextColumn();

                    if(!script.InternalData.Allowed || script.InternalData.Blacklisted)
                    {
                        if(ImGuiEx.IconButton(FontAwesomeIcon.Play))
                        {
                            script.InternalData.Allowed = true;
                            script.InternalData.Blacklisted = false;
                            script.UpdateState();
                        }
                        ImGuiEx.Tooltip("Forcefully allow this script to be enabled. Consequences of this action will be unpredictable.");
                    }
                    else
                    {

                        var e = P.Config.DisabledScripts.Contains(script.InternalData.FullName);
                        if(ImGuiEx.IconButton(e ? FontAwesomeIcon.PlayCircle : FontAwesomeIcon.PauseCircle))
                        {
                            if(e)
                            {
                                P.Config.DisabledScripts.Remove(script.InternalData.FullName);
                            }
                            else
                            {
                                P.Config.DisabledScripts.Add(script.InternalData.FullName);
                            }
                            ScriptingProcessor.Scripts.Each(x => x.UpdateState());
                        }
                        ImGuiEx.Tooltip(e ? "Enable script".Loc() : "Disable script".Loc());
                    }

                    ImGui.TableNextColumn();

                    if(script.InternalData.SettingsPresent)
                    {
                        if(ImGuiEx.IconButton(FontAwesomeIcon.Cog))
                        {
                            if(script.InternalData.ConfigOpen)
                            {
                                openConfig.Controller.SaveConfig();
                            }
                            script.InternalData.ConfigOpen = !script.InternalData.ConfigOpen;
                        }
                        ImGuiEx.Tooltip("Open script's settings".Loc());
                    }
                    else if(script.Controller.GetRegisteredElements().Count > 0)
                    {
                        if(ImGuiEx.IconButton(FontAwesomeIcon.PaintBrush))
                        {
                            if(script.InternalData.ConfigOpen)
                            {
                                openConfig.Controller.SaveConfig();
                            }
                            script.InternalData.ConfigOpen = !script.InternalData.ConfigOpen;
                        }
                        ImGuiEx.Tooltip("Open element editor".Loc());
                    }
                    else
                    {
                        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0f);
                        ImGuiEx.IconButton(FontAwesomeIcon.Cog);
                        ImGui.PopStyleVar();
                        //ImGuiEx.Tooltip("This script contains no settings");
                    }

                    ImGui.TableNextColumn();

                    if(ImGuiEx.IconButton("\uf0e2"))
                    {
                        var isOpen = script.InternalData.ConfigOpen;
                        ScriptingProcessor.ReloadScript(script, true);
                        if(isOpen)
                        {
                            RequestOpen = script.InternalData.FullName;
                        }
                    }
                    ImGuiEx.Tooltip("Reload this script");

                    ImGui.TableNextColumn();

                    if(ImGuiEx.IconButton(FontAwesomeIcon.Trash) && ImGui.GetIO().KeyCtrl)
                    {
                        if(!script.InternalData.Path.IsNullOrEmpty() && script.InternalData.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        {
                            new TickScheduler(() =>
                            {
                                script.Disable();
                                ScriptingProcessor.RemoveScript(script);
                                DeleteFileToRecycleBin(script.InternalData.Path);
                            });
                        }
                        else
                        {
                            Notify.Error("Error deleting".Loc());
                        }
                    }
                    ImGuiEx.Tooltip("Delete script. Hold CTRL + click".Loc());
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }

        if(openConfig != null)
        {
            ImGuiEx.LineCentered("ScriptConfigTitle", delegate
            {
                ImGuiEx.Text(ImGuiColors.DalamudYellow, $"{openConfig.InternalData.FullName} configuration");
            });
            ImGui.Separator();
            ImGuiEx.EzTabBar($"##scriptConfig",
                (openConfig.InternalData.SettingsPresent ? "Configuration" : null, () =>
                {
                    HandleScriptConfigHotkeys(openConfig);
                    try
                    {
                        openConfig.OnSettingsDraw();
                    }
                    catch(Exception ex)
                    {
                        ex.Log();
                    }
                    finally
                    {
                        UpdateScriptConfigUndo(openConfig);
                    }
                }, null, false),
                (openConfig.Controller.GetRegisteredElements().Count > 0 ? "Registered elements" : null, openConfig.DrawRegisteredElements, null, false),
                ("Saved Configurations", openConfig.DrawConfigurations, null, false)
                );

            ImGuiEx.LineCentered("ScriptConfig", delegate
            {
                if(ImGui.Button("Close and save configuration"))
                {
                    openConfig.InternalData.ConfigOpen = false;
                    openConfig.Controller.SaveConfig();
                }
            });
        }
    }

    private static ScriptConfigUndoState GetScriptConfigUndoState(SplatoonScript script)
    {
        var key = script.InternalData.FullName;
        if(!ScriptConfigUndoStates.TryGetValue(key, out var state))
        {
            state = new ScriptConfigUndoState();
            ScriptConfigUndoStates[key] = state;
        }
        if(state.ScriptGuid != script.InternalData.GUID || state.ConfigKey != script.InternalData.CurrentConfigurationKey)
        {
            ResetScriptConfigUndoState(state, script);
        }
        return state;
    }

    private static void ResetScriptConfigUndoState(ScriptConfigUndoState state, SplatoonScript script)
    {
        state.ScriptGuid = script.InternalData.GUID;
        state.ConfigKey = script.InternalData.CurrentConfigurationKey;
        state.Undo.Clear();
        state.Redo.Clear();
        state.LastCommittedJson = null;
        state.LastObservedJson = null;
        state.LastObservedTime = 0;
        state.IsApplying = false;
    }

    private static void HandleScriptConfigHotkeys(SplatoonScript script)
    {
        var state = GetScriptConfigUndoState(script);
        if(state.IsApplying) return;
        var io = ImGui.GetIO();
        if(io.WantTextInput) return;
        if(io.KeyCtrl)
        {
            if(io.KeyShift && ImGui.IsKeyPressed(ImGuiKey.Z, false))
            {
                RedoScriptConfigEdit(script, state);
                return;
            }
            if(ImGui.IsKeyPressed(ImGuiKey.Z, false))
            {
                UndoScriptConfigEdit(script, state);
                return;
            }
            if(ImGui.IsKeyPressed(ImGuiKey.Y, false))
            {
                RedoScriptConfigEdit(script, state);
            }
        }
    }

    private static void UpdateScriptConfigUndo(SplatoonScript script)
    {
        var state = GetScriptConfigUndoState(script);
        if(state.IsApplying) return;
        var config = script.Controller.Configuration;
        if(config == null) return;
        string currentJson;
        try
        {
            currentJson = JsonConvert.SerializeObject(config);
        }
        catch(Exception ex)
        {
            ex.Log();
            return;
        }
        var now = ImGui.GetTime();
        if(state.Undo.Count == 0)
        {
            state.Undo.Add(currentJson);
            state.LastCommittedJson = currentJson;
            state.LastObservedJson = currentJson;
            state.LastObservedTime = now;
            return;
        }
        if(state.LastObservedJson != currentJson)
        {
            state.LastObservedJson = currentJson;
            state.LastObservedTime = now;
        }
        if(state.LastCommittedJson != currentJson && now - state.LastObservedTime >= ScriptConfigUndoIdleSeconds)
        {
            PushScriptConfigUndo(state, currentJson);
            state.Redo.Clear();
        }
    }

    private static void PushScriptConfigUndo(ScriptConfigUndoState state, string json)
    {
        if(state.Undo.Count > 0 && state.Undo[^1] == json) return;
        state.Undo.Add(json);
        state.LastCommittedJson = json;
        TrimScriptConfigUndo(state);
    }

    private static void TrimScriptConfigUndo(ScriptConfigUndoState state)
    {
        while(state.Undo.Count > ScriptConfigUndoLimit + 1)
        {
            state.Undo.RemoveAt(0);
        }
    }

    private static bool CanUndoScriptConfig(ScriptConfigUndoState state) => state.Undo.Count > 1;
    private static bool CanRedoScriptConfig(ScriptConfigUndoState state) => state.Redo.Count > 0;

    private static void UndoScriptConfigEdit(SplatoonScript script, ScriptConfigUndoState state)
    {
        if(!CanUndoScriptConfig(state)) return;
        if(script.Controller.Configuration == null) return;
        var current = state.Undo[^1];
        state.Undo.RemoveAt(state.Undo.Count - 1);
        state.Redo.Add(current);
        ApplyScriptConfigSnapshot(script, state, state.Undo[^1]);
    }

    private static void RedoScriptConfigEdit(SplatoonScript script, ScriptConfigUndoState state)
    {
        if(!CanRedoScriptConfig(state)) return;
        if(script.Controller.Configuration == null) return;
        var snapshot = state.Redo[^1];
        state.Redo.RemoveAt(state.Redo.Count - 1);
        state.Undo.Add(snapshot);
        ApplyScriptConfigSnapshot(script, state, snapshot);
    }

    private static void ApplyScriptConfigSnapshot(SplatoonScript script, ScriptConfigUndoState state, string json)
    {
        var config = script.Controller.Configuration;
        if(config == null) return;
        state.IsApplying = true;
        try
        {
            JsonConvert.PopulateObject(json, config);
        }
        catch(Exception ex)
        {
            ex.Log();
        }
        finally
        {
            state.IsApplying = false;
        }
        state.LastCommittedJson = json;
        state.LastObservedJson = json;
        state.LastObservedTime = ImGui.GetTime();
    }

    private class ScriptConfigUndoState
    {
        public string ScriptGuid;
        public string ConfigKey;
        public readonly List<string> Undo = [];
        public readonly List<string> Redo = [];
        public string LastCommittedJson;
        public string LastObservedJson;
        public double LastObservedTime;
        public bool IsApplying;
    }
}
