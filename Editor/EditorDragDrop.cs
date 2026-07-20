using ImGuiNET;

namespace Editor;

// Shared drag-and-drop state for the editor. ImGui.NET's payload marshalling is awkward for
// strings, so we register a lightweight typed payload as the "signal" and carry the actual
// value (an asset's file path) in a static field. Only one drag is ever in flight at a time.
public static class EditorDragDrop
{
    public const string AssetPayload = "IMP_ASSET";

    static string _assetPath = "";

    // Call between BeginDragDropSource / EndDragDropSource to start dragging an asset file.
    public static unsafe void SetAsset(string path)
    {
        _assetPath = path;
        byte tag = 1;   // ImGui copies the bytes during this call, so a stack local is fine
        ImGui.SetDragDropPayload(AssetPayload, (IntPtr)(&tag), 1);
    }

    // Call between BeginDragDropTarget / EndDragDropTarget. Returns true (with the dragged
    // file path) only on the frame the payload is released over the target.
    public static unsafe bool AcceptAsset(out string path)
    {
        path = "";
        var payload = ImGui.AcceptDragDropPayload(AssetPayload);
        if (payload.NativePtr == null) return false;
        path = _assetPath;
        return true;
    }
}
