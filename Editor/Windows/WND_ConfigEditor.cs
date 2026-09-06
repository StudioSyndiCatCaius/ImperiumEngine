using Editor.Panels;
using Engine;

namespace Editor.Windows;

/*
 *  Config the editor for this project. ONLY edits config vars in this class, read & writing them from Editor.Toml in the projects `Config` folder
 */
[Title("Editor Config")]
public class WND_ConfigEditor : EdWindow
{
    public PNL_Inspector inspector = new();

    public WND_ConfigEditor()
    {
        inspector.config_only = true;
        inspector.on_changed = () => EdConfig.SaveEditorVars(this);
    }

    public override void OnDraw()
    {
        base.OnDraw();
        inspector.selected_object = this;
        inspector.OnDrawPanel();
    }
}
