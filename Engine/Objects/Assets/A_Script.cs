using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects.Assets;

/*
 *  a script is a class that can be attached to an OBJECT to add functionality. THere are 2 kinds of scripts:
 *      - Script_Sharp: a placeholder for running .cs scripts. NOT saved to disc, as it just points to a CS script
 *      - Script_Visual: A visual script with graph nodes. gets compile when loaded at runtime.  
 */
public class A_Script : ImpAsset
{
    
}