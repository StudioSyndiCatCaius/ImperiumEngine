using System.Drawing;
using System.Numerics;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;
using ImperiumCore.Structs;
using Silk.NET.Input;

namespace ImperiumEngine.Objects._2D;

public class O2D_TextEdit : O2D_Text
{
    public Action<string>? OnTextChanged;

    private string _prevText = "";

    public override void OnDraw(ImpRender rhi, double dt)
    {
        base.OnDraw(rhi, dt);
        if (focused && text != _prevText)
        {
            OnTextChanged?.Invoke(text);
            _prevText = text;
        }
    }
}
