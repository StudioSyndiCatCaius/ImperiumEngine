using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace Editor.Windows;

public class WND_ConfigGame : EdWindow
{
    C2_Tree list_categories = new()
    {
        layout = new TLayout2
        {
            size = new Vector2(220, 0),
            size_min = new Vector2(140, 0),
            orient_V = EUIViewportAlignment.Fill,
        },
    };

    C2_Inspector inspector = new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
        allow_multi_select = false,
        include_static = true,
        declared_only = true,
        show_header = true,
        show_search = true,
    };

    Type _selected;

    public WND_ConfigGame()
    {
        name = "Config Game";

        inspector.filter_property = ImpConfig.Member_IsConfig;
        inspector.on_property_changed = _ =>
        {
            if (_selected != null)
            {
                ImpConfig.Save(_selected);
            }
        };

        list_categories.on_item_click = item =>
        {
            if (item.data is Type t)
            {
                Category_Select(t);
            }
        };

        C2_List body = new()
        {
            orentation = EUIOrentation.H,
            layout = TLayout2.FULL,
            spacing = 0,
        };
        body.Child_Add(list_categories);
        body.Child_Add(new C2_Seperator { orentation = EUIOrentation.H });
        body.Child_Add(inspector);
        Child_Add(body);

        Categories_Rebuild();
    }

    public override void OnTrySave()
    {
        ImpConfig.SaveAll();
    }

    void Categories_Rebuild()
    {
        IReadOnlyList<Type> cats = ImpConfig.Categories();
        List<TTreeItem> items = new();
        for (int i = 0; i < cats.Count; i++)
        {
            Type t = cats[i];
            items.Add(new TTreeItem
            {
                sections = new[]
                {
                    new TTreeItemSection
                    {
                        text = ImpConfig.CategoryName(t),
                        icon = C2_Tree.Class_Icon(t),
                    }
                },
                data = t,
            });
        }
        list_categories.Tree_SetItems(items);

        Type pick = _selected;
        bool still_there = false;
        if (pick != null)
        {
            for (int i = 0; i < cats.Count; i++)
            {
                if (cats[i] == pick)
                {
                    still_there = true;
                    break;
                }
            }
        }
        if (!still_there)
        {
            if (cats.Count > 0)
            {
                pick = cats[0];
            }
            else
            {
                pick = null;
            }
        }
        Category_Select(pick);
    }

    void Category_Select(Type type)
    {
        _selected = type;
        list_categories.Tree_SelectData(type);
        if (type == null)
        {
            inspector.Objects_Clear();
            return;
        }
        inspector.Select(type);
    }
}
