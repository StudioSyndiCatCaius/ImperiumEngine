using System.Numerics;
using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._2D;

// lays out a series of buttons corresponding to an enum,. toggling between buttons changed the selected enum
[ImpClass(Hidden = true)]
public class C2_EnumOption : Imp2D
{
    [ImpVar] public bool show_name = true;
    [ImpVar] public bool show_icon = true;
    [ImpVar] public EUIOrentation orentation = EUIOrentation.H;

    public Type base_enum;
    public Enum selected_enum;

    public Action<Enum> on_change;

    C2_List _list;
    Type _built_enum;
    bool _built_name;
    bool _built_icon;
    EUIOrentation _built_orentation;

    static readonly Dictionary<string, A_Texture?> _icon_cache = new();

    public C2_EnumOption()
    {
        option_button = null;
        cursor_filter = ECursorFilter.Pass;
        layout.size = new Vector2(48, 22);
        layout.size_min = new Vector2(22, 22);
        _list = new C2_List
        {
            orentation = orentation,
            spacing = 2,
            layout = TLayout2.FULL,
        };
        Child_Add(_list);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        cursor_filter = ECursorFilter.Pass;
        _list.orentation = orentation;

        if (base_enum != _built_enum || show_name != _built_name || show_icon != _built_icon || orentation != _built_orentation)
            Rebuild();

        for (int i = 0; i < _list.children.Count; i++)
        {
            if (_list.children[i] is not C2_Button btn) continue;
            bool on = selected_enum != null && btn.name == selected_enum.ToString();
            btn.style.style_unhovered = on ? UiStyle_Box.STYLE_BTN_PRESS : UiStyle_Box.STYLE_BTN_IDLE;
            btn.style.style_hovered = on ? UiStyle_Box.STYLE_BTN_PRESS : UiStyle_Box.STYLE_BTN_HOVER;
        }
    }

    void Rebuild()
    {
        _built_enum = base_enum;
        _built_name = show_name;
        _built_icon = show_icon;
        _built_orentation = orentation;

        while (_list.children.Count > 0)
            _list.children[0].Destroy();

        if (base_enum == null || !base_enum.IsEnum) return;

        bool horizontal = orentation == EUIOrentation.H;
        Array values = Enum.GetValues(base_enum);
        float main = 0;
        float cross = 22;

        for (int i = 0; i < values.Length; i++)
        {
            Enum value = (Enum)values.GetValue(i);
            string key = value.ToString();
            A_Texture icon = show_icon ? Icon_Try(base_enum, key) : null;
            bool has_icon = icon != null && icon.texture.Id != 0;
            bool has_text = show_name || !has_icon;
            string label = has_text ? Label(base_enum, key) : "";
            float w = has_icon && has_text ? 64 : has_icon ? 22 : 48;

            C2_Button btn = new()
            {
                name = key,
                text = label,
                icon = has_icon ? icon : null,
                button_layout = EButtonLayout.Icon_Text_H,
                style = new UI_Button(),
                text_style = UI_Text.LIGHT,
                override_font_size = 11,
                content_pad = 2,
                icon_size = 14,
                gap = 3,
                layout = new TLayout2
                {
                    size = horizontal ? new Vector2(w, 22) : new Vector2(64, 22),
                    size_min = new Vector2(22, 22),
                    orient_H = horizontal ? EUIViewportAlignment.Start : EUIViewportAlignment.Fill,
                    orient_V = horizontal ? EUIViewportAlignment.Fill : EUIViewportAlignment.Start,
                },
                on_click = () =>
                {
                    if (selected_enum != null && Equals(selected_enum, value)) return;
                    selected_enum = value;
                    on_change?.Invoke(value);
                },
            };
            _list.Child_Add(btn);
            main += (horizontal ? w : 22) + (i > 0 ? _list.spacing : 0);
        }

        if (values.Length > 0)
        {
            if (horizontal) layout.size = new Vector2(main, cross);
            else layout.size = new Vector2(64, main);
            layout.size_min = layout.size;
        }
    }

    static string Label(Type type, string value)
    {
        FieldInfo field = type.GetField(value);
        TitleAttribute title = field?.GetCustomAttribute<TitleAttribute>();
        if (!string.IsNullOrEmpty(title?.Name)) return title.Name;
        return value;
    }

    static A_Texture? Icon_Try(Type type, string value)
    {
        string cache_key = type.FullName + "." + value;
        if (_icon_cache.TryGetValue(cache_key, out A_Texture? cached)) return cached;

        string[] folders = { "{engine}/Icons/type", "{engine}/Icons/Types" };
        string[] names =
        {
            type.Name + "/" + value,
            type.Name + "_" + value,
            type.Name + "." + value,
            value,
        };
        for (int f = 0; f < folders.Length; f++)
        for (int n = 0; n < names.Length; n++)
        {
            string path = folders[f] + "/" + names[n] + ".png";
            if (!File.Exists(ImpFile.Path_Resolve(path))) continue;
            A_Texture tex = ImpAsset.Import<A_Texture>(path);
            if (tex == null || tex.texture.Id == 0) continue;
            _icon_cache[cache_key] = tex;
            return tex;
        }

        _icon_cache[cache_key] = null;
        return null;
    }
}
