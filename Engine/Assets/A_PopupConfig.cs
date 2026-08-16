namespace ImperiumEngine.Comps._1D
{
    public struct TPopupMenuOption
    {
        public string text;
        public bool is_separator;
        public bool is_disabled;
        public Action on_press;
        public List<TPopupMenuOption> suboptions;
    }
}

namespace ImperiumEngine.Assets
{
    using ImperiumEngine;
    using ImperiumEngine.Comps._1D;

    public class A_PopupConfig : ImpAsset
    {
        [ImpVar] public bool searchable;
        [ImpVar] public List<TPopupMenuOption> options = new();
    }
}
