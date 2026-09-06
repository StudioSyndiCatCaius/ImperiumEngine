using System.Numerics;
using Engine;
using Engine.Core;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;

namespace Editor.Dialog;

public class EDLG_SaveAsset : EdDialog
{
    const string PopupId = "Save Asset##ed_save_asset";

    static bool _want_open;
    static ImpAsset? _asset;
    static Action<string>? _on_close;
    static string _path = "";

    public static void Run(ImpAsset asset, Action<string> on_close)
    {
        _asset = asset;
        _on_close = on_close;
        if (asset != null && !string.IsNullOrEmpty(asset.filepath))
            _path = asset.filepath;
        else
        {
            string ext = asset?.GetFileExtension() ?? ".ImpAsset";
            string type = asset?.GetType().Name ?? "Asset";
            _path = "{game}/" + type + ext;
        }
        _want_open = true;
    }

    public static void DrawPending()
    {
        if (_want_open)
        {
            ImGui.OpenPopup(PopupId);
            _want_open = false;
        }

        ImGui.SetNextWindowSize(new Vector2(480, 0), ImGuiCond.FirstUseEver);
        bool open = true;
        if (!ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextUnformatted(_asset != null ? "Save " + _asset.GetType().Name : "Save Asset");
        ImGui.SetNextItemWidth(420);
        ImGui.InputText("##path", ref _path, 512);
        ImGui.TextDisabled("{game} / {engine} paths are allowed");
        ImGui.Spacing();

        if (ImGui.Button("Save", new Vector2(80, 0)))
            Confirm();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(80, 0)) || !open)
            Cancel();

        ImGui.EndPopup();
    }

    static void Confirm()
    {
        if (_asset == null || string.IsNullOrWhiteSpace(_path))
        {
            Cancel();
            return;
        }

        string path = _path.Trim();
        string abs = GFile.Make_Path_Absolute(path);
        string? dir = Path.GetDirectoryName(abs);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _asset.filepath = path;
        _asset.is_inlined = false;
        App.assets[new TFile(path)] = _asset;
        _asset.Save(true);

        ImGui.CloseCurrentPopup();
        Action<string>? cb = _on_close;
        _on_close = null;
        _asset = null;
        cb?.Invoke(path);
    }

    static void Cancel()
    {
        ImGui.CloseCurrentPopup();
        _on_close = null;
        _asset = null;
    }
}
