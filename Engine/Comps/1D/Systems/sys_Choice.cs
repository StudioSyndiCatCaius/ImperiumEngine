using ImperiumEngine.Comps._2D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D.States;

public class sys_Choice : C1_GameSystem
{
    public C2_List list_choices;

    static List<TText> _choices;
    static Action<int> _on_select;

    public static void Run(List<TText> choices, Action<int> on_select)
    {
        _choices = choices;
        _on_select = on_select;
    }

    public static void Select(int index)
    {
        Action<int> cb = _on_select;
        _on_select = null;
        _choices = null;
        if (cb == null)
        {
            return;
        }
        cb(index);
    }
}
