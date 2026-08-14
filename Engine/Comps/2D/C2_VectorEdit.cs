using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_VectorEdit : ImpComp2D
{
    public readonly C2_Slider[] fields;
    public string[] tags = { "X", "Y", "Z", "W" };
    public static readonly Color[] axis_colors =
    {
        new Color(198, 82, 92, 255),
        new Color(124, 176, 84, 255),
        new Color(78, 130, 208, 255),
        new Color(150, 150, 160, 255),
    };

    public Action<C2_VectorEdit> on_changed;

    const float Gap = 4f;
    float _tag_w = 12f;
    readonly Rectangle[] _tags;

    public int Count => fields.Length;

    public C2_VectorEdit(int count, bool integers = false)
    {
        cursor_filter = ECursorFilter.Pass;
        fields = new C2_Slider[count];
        _tags = new Rectangle[count];
        for (int i = 0; i < count; i++)
        {
            C2_Slider field = new()
            {
                is_spinner = true,
                min = 0,
                max = 0,
                step = integers ? 1f : 0f,
                value_text_decimals = integers ? 0 : 3,
                drag_sensitivity = integers ? 0.2f : 0.05f,
                accent_color = axis_colors[Math.Min(i, axis_colors.Length - 1)],
                view_alighnment_H = EUIViewportAlignment.Start,
                view_alighnment_V = EUIViewportAlignment.Fill,
            };
            field.on_changed = _ => on_changed?.Invoke(this);
            fields[i] = field;
            Child_Add(field);
        }
    }

    public float Value_Get(int index) =>
        index >= 0 && index < fields.Length ? fields[index].value : 0f;

    public void Values_SetQuiet(ReadOnlySpan<float> values)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].IsBusy) continue;
            fields[i].Value_SetQuiet(i < values.Length ? values[i] : 0f);
        }
    }

    public bool IsBusy()
    {
        for (int i = 0; i < fields.Length; i++)
            if (fields[i].IsBusy) return true;
        return false;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || fields.Length == 0) return;

        float col = (dim.size.X - Gap * (fields.Length - 1)) / fields.Length;
        _tag_w = 12f;
        for (int i = 0; i < fields.Length; i++)
        {
            float x = i * (col + Gap);
            _tags[i] = new Rectangle(dim.position.X + x, dim.position.Y, _tag_w, dim.size.Y);
            fields[i].transform.position = new Vector2(x + _tag_w, 0);
            fields[i].size = new Vector2(MathF.Max(0, col - _tag_w), dim.size.Y);
            fields[i].size_min = new Vector2(0, 18);
            fields[i].view_alighnment_H = EUIViewportAlignment.Start;
            fields[i].view_alighnment_V = EUIViewportAlignment.Fill;
        }
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        for (int i = 0; i < fields.Length && i < tags.Length; i++)
        {
            Color prev = UiStyle_Text.MUTED.color;
            UiStyle_Text.MUTED.color = axis_colors[Math.Min(i, axis_colors.Length - 1)];
            UiStyle_Text.MUTED.Draw(tags[i], new Vector2(_tags[i].X, _tags[i].Y),
                new Vector2(_tags[i].Width, _tags[i].Height),
                0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Center);
            UiStyle_Text.MUTED.color = prev;
        }
    }
}
