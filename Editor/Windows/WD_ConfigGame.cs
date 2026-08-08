using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Windows;

// Test bed for C2_Inspector: the window inspects itself, so every [ImpVar] below shows up
// as a row and editing it writes straight back into these fields.
public class WD_ConfigGame : EdWindow
{
    public C2_Inspector c_inspector = new C2_Inspector();

    [ImpVar] public string test_string = "hello";
    [ImpVar] public int test_int = 3;
    [ImpVar] public bool test_bool = true;
    [ImpVar] public float test_float = 0.5f;
    [ImpVar] public EUIAlignment test_enum = EUIAlignment.Vertical;
    [ImpVar] public TMargins test_struct = new TMargins { left = 1, right = 2, top = 3, bottom = 4 };
    [ImpVar] public Vector2 test_vector = new Vector2(10, 20);

    C2_Text c_readout;

    public WD_ConfigGame()
    {
        name = "Game Config";
        cursor_filter = ECursorFilter.Pass;

        c_readout = new C2_Text("")
        {
            align = 0f,
            cursor_filter = ECursorFilter.Ignore,
        };

        Child_Add(c_inspector);
        Child_Add(c_readout);

        // echoes the live field values, so it's obvious whether an edit actually landed
        c_inspector.on_property_changed = _ => Readout_Update();
        c_inspector.Select(this); //parented first, so rows build against the editor theme

        Readout_Update();
    }

    void Readout_Update()
    {
        c_readout.text = $"string={test_string}  int={test_int}  bool={test_bool}  "
                       + $"float={test_float:0.###}  enum={test_enum}  "
                       + $"margins=({test_struct.left},{test_struct.top})  vec={test_vector}";
    }

    // Inspector takes the panel; a readout strip sits along the bottom.
    protected override void Layout_Children(Rectangle content)
    {
        float strip = Theme_Get().item_height;
        float body = MathF.Max(0, content.Height - strip);

        c_inspector.OnLayout_Exact(new Rectangle(content.X, content.Y, content.Width, body));
        c_readout.OnLayout_Exact(new Rectangle(content.X, content.Y + body, content.Width, strip));
    }
}
