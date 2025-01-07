using System.Numerics;
using ImGuiNET;

namespace ImperiumEngine.Source.Nodes._1D;

public class N1D_MouseInput : ImpObject2D
{
    // Add this field
    Vector2 windowOffset;
    private bool isDragging = false;
    private Vector2 dragStartPos;
    
    public override void OnDraw(double delta)
    {
        // In OnDraw, before the foreach loop:
        windowOffset = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        // Modify the drag selection code:
        if (isDragging)
        {
            Vector2 dragEndPos = ImGui.GetMousePos();
    
            // Convert screen coordinates to window-relative
            Vector2 relativeStart = dragStartPos - windowOffset;
            Vector2 relativeEnd = dragEndPos - windowOffset;
    
            ImGui.GetWindowDrawList().AddRect(dragStartPos, dragEndPos, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 1.0f, 0.5f)));
    
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {

        
                isDragging = false;

            }
        }
        base.OnDraw(delta);
    }
    
}