using Engine.Assets;

namespace Editor.Panels;

/*
 *  Graph for editing a script (usually for an ImpScene, but will probably add support for other types later.
 * LAYOUT:
 * Left: properties (functions, vars, and signals)
 *  - Tabs: Custom (custom properties added to this script) | Native : (properties inherited from the script type/parent class)
 * Center: script node graph
 *  - right click opens placable properties avaialble for this class
 *  - drag of a node pin opens placable properties contextual to what is dragged (E.g drag off an object var, it shows funcs/vars that object can call)
 * Left: inspector tabs
 *  - tab 1: shows the config for whatever property (function, var, or singla) you have currently selected (like name, min/max, input/output arbs for functions, and other specifics)
 *  - tab 2: shows config for this script, such as parent type (note trying to change that should come with a big warning)
 * 
 */
public class PNL_ScriptGraph : EdPanel
{
    public A_Script? script;
}