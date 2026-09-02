using System.Numerics;
using Engine.Core;
using Engine.Enums;

namespace Engine.Globals;

public static class G2D
{
    //checks a point on screen for the first Imp2D Comp it finds.
    public static Imp2D Trace2D_ForComp(Vector2 position, ImpComp root)
    {
        Imp2D TraceNode(ImpComp n)
        {
            if (n == null || !n.is_visible) return null;
            Imp2D self = n as Imp2D;
            if (self != null && self.cursor_filter == ECursorFilter.Ignore) return null;

            for (int i = n.children.Count - 1; i >= 0; i--)
            {
                Imp2D child = TraceNode(n.children[i]);
                if (child != null) return child;
            }

            if (self != null && self.cursor_filter == ECursorFilter.Hit && self.Contains(position))
                return self;
            return null;
        }

        return TraceNode(root);
    }

    public static Imp2D Trace2D_ForComp(Vector2 position)
    {
        Imp2D hit = Trace2D_ForComp(position, App.scene_current?.root);
        if (hit != null) return hit;
        for (int i = App.scenes_global.Count - 1; i >= 0; i--)
        {
            hit = Trace2D_ForComp(position, App.scenes_global[i]?.root);
            if (hit != null) return hit;
        }
        return null;
    }
}
