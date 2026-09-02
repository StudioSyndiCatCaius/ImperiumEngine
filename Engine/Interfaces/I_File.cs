using Engine.Assets;

namespace Engine.Interfaces;

// handled by the editor file browser
public class I_File 
{
    public virtual TPopupOption File_GetPopupOptions() { return default; }
    
    public virtual void File_DrawEditor() {}
}