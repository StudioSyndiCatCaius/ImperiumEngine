using ImperiumCore.Const;
using ImperiumEngine.Interfaces;

namespace ImperiumCore.Classes;

public class ImpAsset : ImpObject, I_Saveable
{
    public string GetSaveExtnsion()
    {
        return ImpC_String.EXT_ASSET;
    }
    
    public void Savable_OnRead(string path)
    {
        // read ImpVar properties from a TOML file
        string _ext=GetSaveExtnsion();
    }

    public void Savable_OnWrite(string path)
    {
        // write ImpVar properties from a TOML file
        string _ext=GetSaveExtnsion();
    }
}


// ##############################################################################################################
// CORE ASSET - Material
// ##############################################################################################################

public class ImpAsset_Material : ImpAsset
{
    [ImpVar] public ImpAsset_Material ParentMaterial;
}

// ##############################################################################################################
// CORE ASSET - 2D Theme
// ##############################################################################################################

//baseclass for GUI/Slate themes. VERY similar to the them resource in Godot.
// NOTE: All iterations of ImpAssets SHOULD go in Engine/Assets. This is an exception because it is needed by ImpComponent2D
public class ImpAsset_2DTheme : ImpAsset
{
    
}

// ##############################################################################################################
// CORE ASSET - Animation
// ##############################################################################################################



public class ImpAsset_Animation : ImpAsset
{
    
}