using Editor.Panels;

namespace Editor.Windows;

public class WND_ConfigGame : EdWindow
{
    public PNL_Inspector inspector = new();

    public override void OnDraw()
    {
        base.OnDraw();
    }
}