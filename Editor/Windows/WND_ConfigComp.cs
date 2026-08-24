using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace Editor.Windows;

/*
 *  Edit instance [ImpVar][Config] defaults for ImpComp classes.
 *  Static [Config] vars are Game Config (ImpConfig), not here.
 *  - left: tree of ImpComp classes that have instance [Config] vars
 *  - right: inspector for those vars on the selected class
 *
 *  Saved to {game}/Config/Comps/{Type}.json. Applied on new() / spawn.
 *  Does not change comps already placed in a scene.
 */
public class WND_ConfigComp : EdWindow
{
    C2_SearchBar search_bar = new()
    {
        placeholder = "Search",
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
        },
    };

    C2_Tree list_classes = new()
    {
        layout = TLayout2.FULL,
    };

    C2_Inspector inspector = new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
        allow_multi_select = false,
        show_header = true,
        show_search = true,
        show_components = false,
    };

    Type _selected;

    public WND_ConfigComp()
    {
        name = "Comp Config";

        inspector.filter_property = ImpClassDefaults.Member_IsConfig;
        inspector.on_property_changed = _ =>
        {
            if (_selected != null)
            {
                ImpClassDefaults.Save(_selected);
            }
        };

        search_bar.on_search = q =>
        {
            list_classes.Tree_FilterClasses(q);
        };

        list_classes.on_item_click = item =>
        {
            if (item.is_disabled || item.data is not Type t)
            {
                return;
            }
            Class_Select(t);
        };

        C2_List left = new()
        {
            orentation = EUIOrentation.V,
            is_scrollable = false,
            spacing = 0,
            layout = new TLayout2
            {
                size = new Vector2(220, 0),
                size_min = new Vector2(140, 0),
                orient_V = EUIViewportAlignment.Fill,
            },
        };
        left.Child_Add(search_bar);
        left.Child_Add(list_classes);

        C2_List body = new()
        {
            orentation = EUIOrentation.H,
            layout = TLayout2.FULL,
            spacing = 0,
        };
        body.Child_Add(left);
        body.Child_Add(new C2_Seperator { orentation = EUIOrentation.H });
        body.Child_Add(inspector);
        Child_Add(body);

        Classes_Rebuild();
    }

    public override void OnTrySave()
    {
        ImpClassDefaults.SaveAll();
    }

    void Classes_Rebuild()
    {
        list_classes.Tree_Populate_FromClasses(typeof(ImpComp), ImpClassDefaults.Type_HasConfig);
        Type pick = _selected;
        if (pick == null || pick.IsAbstract || ImpClassDefaults.IsHidden(pick) || !ImpClassDefaults.Type_HasConfig(pick))
        {
            pick = null;
        }
        Class_Select(pick);
    }

    void Class_Select(Type type)
    {
        if (type != null && type.IsAbstract)
        {
            return;
        }
        _selected = type;
        list_classes.Tree_SelectData(type);
        if (type == null)
        {
            inspector.Objects_Clear();
            return;
        }
        ImpComp proto = ImpClassDefaults.Prototype(type);
        if (proto == null)
        {
            inspector.Objects_Clear();
            return;
        }
        inspector.Select(proto);
    }
}