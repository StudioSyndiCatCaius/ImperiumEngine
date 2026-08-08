using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// A row of numeric fields sharing one line, each behind a one-letter tag.
//
// This is what a vector wants to look like. Left to the generic struct expansion a Vector3
// becomes a collapsible group of three full-width rows, which costs four lines to say what
// fits on one - and a transform, which is three vectors, would take twelve.
public class C2_VectorEdit : ImpComp2D
{
    //separation between columns
    const float COLUMN_GAP = 4f;

    public readonly C2_Progresser[] fields;
    public string[] tags = { "X", "Y", "Z", "W" };

    // Axis tint, kept muted: this is a tag on a field, not a control of its own, and a
    // transform puts nine of them on screen at once. W has no axis colour to borrow.
    public static readonly Color[] axis_colors =
    {
        new Color(198, 82, 92, 255),   // X
        new Color(124, 176, 84, 255),  // Y
        new Color(78, 130, 208, 255),  // Z
        new Color(150, 150, 160, 255), // W
    };

    //multi-selection disagreement; fields blank rather than showing one target's value
    public bool is_mixed;

    public Action<C2_VectorEdit>? on_changed;

    readonly Rectangle[] rect_tags;
    float tag_width = 12f;

    public int Count => fields.Length;

    public C2_VectorEdit(int count, bool integers = false)
    {
        cursor_filter = ECursorFilter.Pass; //the fields take the cursor, not the row

        fields = new C2_Progresser[count];
        rect_tags = new Rectangle[count];

        for (int i = 0; i < count; i++)
        {
            var field = new C2_Progresser
            {
                mouse_can_edit = true,
                can_type_edit = true,
                text_style = EProgresserTextStyle.Value,
                step_amount = integers ? 1f : 0f,
                decimals = integers ? 0 : 3,
                drag_sensitivity = integers ? 0.1f : 0.01f,
                text_inset = 4f, //three fields on one line have no room for theme padding
                accent_color = axis_colors[Math.Min(i, axis_colors.Length - 1)],
            };

            field.on_value_changed = _ => on_changed?.Invoke(this);

            fields[i] = field;
            Child_Add(field);
        }
    }

    // ---------------------------------------------------
    // value
    // ---------------------------------------------------

    public float Value_Get(int index)
    {
        return index >= 0 && index < fields.Length ? fields[index].value : 0f;
    }

    public void Values_SetQuiet(ReadOnlySpan<float> values)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            fields[i].Value_SetQuiet(i < values.Length ? values[i] : 0f);
            fields[i].text_style = is_mixed ? EProgresserTextStyle.None : EProgresserTextStyle.Value;
        }
    }

    // ---------------------------------------------------
    // layout
    // ---------------------------------------------------

    public override Vector2 Size_GetContentMin() => new Vector2(0, Theme_Get().item_height);

    protected override void Layout_Children(Rectangle content)
    {
        var th = Theme_Get();

        //the tag column is measured, not guessed, so a larger theme font still fits
        tag_width = ImpUI.TextMeasure("W", th.style_text_dim).X + 4f;

        float column = (content.Width - COLUMN_GAP * (fields.Length - 1)) / fields.Length;

        for (int i = 0; i < fields.Length; i++)
        {
            float x = content.X + i * (column + COLUMN_GAP);

            rect_tags[i] = new Rectangle(x, content.Y, tag_width, content.Height);

            fields[i].OnLayout_Exact(new Rectangle(
                x + tag_width, content.Y, MathF.Max(0, column - tag_width), content.Height));
        }
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var th = Theme_Get();

        for (int i = 0; i < fields.Length && i < tags.Length; i++)
        {
            ImpUI.TextInRect(tags[i], rect_tags[i], th.style_text_dim, 0.5f);
        }
    }
}
