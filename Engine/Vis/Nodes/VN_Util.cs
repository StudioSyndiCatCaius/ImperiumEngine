using Engine.Sandbox;

namespace Engine.Vis.Nodes;

public class VN_Var : VisNode //Gets a reference to a variable. requires a "owner" input of the script calling this node does not already
{
    
}

public class VN_Func : VisNode //base node for a callable function (Using "ScriptCall")
{
    
}

public class VN_ClassStatic : VisNode
{
    /*
     *  this gets a reference to a static class so you can call Static functions of it. Like "GMath.V3_Interp"
     */
}

public class VN_Hook_Enter : VisNode // Entry point for when overriding a "Virtual" function with [ScriptHook]
{
    
}

public class VN_Hook_Return : VisNode // return point for when overriding a "Virtual" function with [ScriptHook]
{
    
}