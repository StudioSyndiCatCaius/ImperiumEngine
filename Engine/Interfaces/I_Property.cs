namespace Engine.Interfaces;

public interface I_Property
{
    
    public virtual bool Property_IsCustomParse() { return false; }
    public virtual void Property_Read(object value) { } // reads this string value (gotten from a file) and makes it the current value
    public virtual object Property_Write() { return ""; } // converts this value to a string that can be written to a file
    /* EXAMPLE:
     *  - ImpAsset (reference) : "&A_Scene:{game}/Path/To/Scene.ImpScene"
     *  - ImpAsset (inline) : "!A_Scene:{game}/Path/To/Scene.ImpScene"
     */
    
    public virtual void Property_DrawInspector() {}
}