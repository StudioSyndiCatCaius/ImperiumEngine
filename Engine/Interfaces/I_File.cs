using Engine.Assets;

namespace Engine.Interfaces;

// handled by the editor file browser
public interface I_File 
{
    public virtual TPopupOption File_GetPopupOptions() { return default; }
    
    public virtual void File_DrawEditor() {}
    
    public virtual Type[] File_GetFavoriteSubTypes() { return null; }
}