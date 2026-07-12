using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using Serilog;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using static FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkHistory.Delegates;
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Lumina.Excel.Sheets.Experimental;
using SamplePlugin;
using System.Runtime.InteropServices;



namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string goatImagePath;
    private readonly Plugin plugin;

    public static float arrowOffset = 0.75f;


    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("POV+ Alpha 0.1.1", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.goatImagePath = goatImagePath;
        this.plugin = plugin;
    }

    private static bool ResetSliderFloat(string id, ref float val, float min, float max, float reset, string format)
    {
        var save = false;


        ///This is the Reset button
        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{FontAwesomeIcon.UndoAlt.ToIconString()}##{id}"))
        {
            val = reset;
            save = true;
            //Service.Log.Information($"===UI===");
        }
        ImGui.PopFont();

        ///This is the Slider
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 160 * ImGuiHelpers.GlobalScale);
        save |= ImGui.SliderFloat(id, ref val, min, max, format);

        return save;
        //    if (CurrentPreset == PresetManager.CurrentPreset)
        //        CurrentPreset.Apply();
    }


    public void Dispose() { }

    //TEST VARIABLES
    public bool MyTest { get; set; } = false;

    public override void Draw()
    {


        ///Left Box
        ImGui.BeginChild("LeftPanel", new Vector2(250 * ImGuiHelpers.GlobalScale, 0), true);

        ImGui.Text("Profiles coming soon (maybe)");

        ImGui.EndChild();


        //Right Box
        ImGui.SameLine();
        ImGui.BeginChild("RightPanel", Vector2.Zero, true);

        if (ImGui.Checkbox("Mod Enabled?", ref Plugin.P.Configuration.Setting_ModEnabled))
            plugin.Configuration.Save();

        ImGui.Spacing();

        ImGui.TextUnformatted($" While using this mod I reccomend doing the following \n Character Configuration>General>1st Person Camera Auto-adjustment - Set this to Never" +
            $"\n This is not essential but it prevents the overwriting of the camera X and Z rotation that the First Person Auto Adjustment overwrites");
        ImGui.Dummy(new Vector2(0, 20));

        if (ImGui.Checkbox("Plugin Disabled when in Third Person", ref Plugin.P.Configuration.Setting_FirstPersonOnly))
            plugin.Configuration.Save();

        ImGui.Spacing();


        ImGui.TextUnformatted($"Camera Offset:");



        ImGui.Spacing();
        if (ResetSliderFloat("FOV", ref Plugin.P.Configuration.Setting_FOV, 0, 5, 0.75f, "%.2f"))
            plugin.Configuration.Save();


        ImGui.Spacing();
        if (ResetSliderFloat("X Offset (Forwards/Back)", ref Plugin.P.Configuration.OffsetX, -1, 1, 0f, "%1f"))
            plugin.Configuration.Save();


        ImGui.Spacing();
        if (ResetSliderFloat("Y Offset (Up Down)", ref Plugin.P.Configuration.OffsetY, -1, 1, 0f, "%1f"))
            plugin.Configuration.Save();

        ImGui.Spacing();
        if (ResetSliderFloat("Z Offset (Left Right)", ref Plugin.P.Configuration.OffsetZ, -1, 1, 0f, "%1f"))
            plugin.Configuration.Save();


        ImGui.Spacing();
        ImGui.Dummy(new Vector2(0, 20));

        // --- Camera bone picker -----------------------------------------------------------------
        ImGui.TextUnformatted("Camera Bone (which bone the camera is pinned to):");
        ImGui.TextUnformatted($"Current [{Plugin.P.Configuration.BoneToBind}]: {GlobalVars.BoneToBindName}");

        var boneMax = Math.Max(GlobalVars.PlayerBoneCount, 1);

        if (ImGui.Button("<##bonePrev") && Plugin.P.Configuration.BoneToBind > 0)
        {
            Plugin.P.Configuration.BoneToBind--;
            plugin.Configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button(">##boneNext") && Plugin.P.Configuration.BoneToBind < boneMax)
        {
            Plugin.P.Configuration.BoneToBind++;
            plugin.Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 160 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderInt("Bone##boneToBind", ref Plugin.P.Configuration.BoneToBind, 0, boneMax))
            plugin.Configuration.Save();

        ImGui.TextWrapped("Tip: scrub until the camera sits on your head (look for a bone named j_kao), " +
                          "then use X Offset to push forward to your eyes. Binding to the head bone also " +
                          "makes the camera bob with the run animation.");

        ImGui.Spacing();
        ImGui.Dummy(new Vector2(0, 20));

        ImGui.TextUnformatted($"EXPERIMENTAL BELOW - Causes rotation issues when moving while holding right mouse button");

        ImGui.Spacing();

        if (ImGui.Checkbox("Bind Camera Rotation X (Left Right) to the Head Bone", ref Plugin.P.Configuration.RotationBindBoolX))
            plugin.Configuration.Save();

        ImGui.Spacing();

        if (ImGui.Checkbox("Bind Camera Rotation Z (Up Down) to the Head Bone - (this also allows you to rotate the camera 360 degrees up and down , just to account for backflips flips etc)", ref Plugin.P.Configuration.RotationBindBoolZ))
            plugin.Configuration.Save(); ;

        ImGui.Spacing();
        ImGui.Dummy(new Vector2(0, 20));

        ImGui.TextUnformatted($"VERY BROKEN BELOW - Fine if you move with just the keyboard");


        if (ImGui.Checkbox("Camera Rotates when player rotates (emulates 1st person camera auto adjustment when moving - but different) VERY GLITCHY WHEN MOVING/ROTATING WITH RIGHT MOUSE CLICK", ref Plugin.P.Configuration.Setting_RotateWithplayer))
            plugin.Configuration.Save();


        ImGui.EndChild();
    }
}
