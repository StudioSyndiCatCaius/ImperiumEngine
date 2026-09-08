using Engine.Core;

namespace Editor.Dialog;

public class EDLG_SaveAsset : EdDialog
{
    public static void Run(ImpAsset asset, Action<string> on_close)
    {
        EDLG_FileAction.SaveAsset(asset, on_close);
    }

    public static void DrawPending()
    {
    }
}
