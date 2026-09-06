using Engine.Assets;
using Engine.Globals;
using Engine.Interfaces;
using Engine.Structs;

namespace Engine.Core;

public class ImpAsset : I_Property, I_Inspectable, I_File
{
    
    // ==============================================================================================================
    // CLASS
    // ==============================================================================================================
    [ImpVar] public string sourcefile="";
    
    public string filepath="";
    public bool is_dity=false;
    public bool is_inlined=false;
    
    private ImpFile _src_file_ref;

    
    public void Save(bool force=false)
    {
        if (!force & !is_dity) return;
        if (string.IsNullOrEmpty(filepath)) return;
        
        TTable _tbl=To_Table();
        string file_str=TTable.ToTOML(_tbl);
        
        File.WriteAllText(GFile.Make_Path_Absolute(filepath), file_str);
        is_dity=false;
        GLog.Info("Saved " + filepath);
    }
    
    // Reimports the data from the sourcefile
    [CallInEditor]
    [Title("Reimport")]
    public void Source_Reimport()
    {
        //get or import sourcefile if not already 
        if (string.IsNullOrEmpty(sourcefile)) return;
        _src_file_ref = GFile.Import<ImpFile>(sourcefile);
        if (_src_file_ref != null)
        {
            _src_file_ref.Reimport();
            OnReimport(_src_file_ref);
        }
        else GLog.Error($"Could not find sourcefile {sourcefile}");
    }
    
    public ImpFile Source_Get()
    {
        if (_src_file_ref == null && !string.IsNullOrEmpty(sourcefile)) Source_Reimport();
        return _src_file_ref;
    }
    
    public virtual void OnReimport(ImpFile src) { }

    // --------------------------------------------------
    // Table Read/Write
    // --------------------------------------------------
    
    public virtual void From_Table(TTable tbl)
    {
        TTable vars=tbl.get_Table("vars");
        if(vars!=null) TTable.PopulateObject(vars, this);
    }
    
    public virtual TTable To_Table() { return TTable.FromObject(this); }

    // --------------------------------------------------
    // File
    // --------------------------------------------------
    protected virtual string File_GetExtension() { return "ImpAsset"; }
    
    public string GetFileExtension() { return "."+File_GetExtension(); }
    
    // --------------------------------------------------
    // Property
    // --------------------------------------------------
    public bool Property_IsCustomParse() { return true; }

    public void Property_Read(object value)
    {
        if (value is TTable inline_table)
        {
            is_inlined = true;
            filepath = "";
            TTable vars = inline_table.get_Table("vars") ?? inline_table;
            TTable.PopulateObject(vars, this);
            return;
        }

        is_inlined = false;
        string reference = value as string ?? "";
        int colon = reference.IndexOf(':');
        filepath = colon >= 0 ? reference[(colon + 1)..] : reference;
    }

    public object Property_Write()
    {
        if (is_inlined)
            return TTable.FromObject(this);

        GAsset.Builtin_Is(this);
        return $"${GetType().Name}:{filepath}";
    }
    
    // --------------------------------------------------
    // Editor_Vibe
    // --------------------------------------------------
    public virtual bool Editor_UseCustomEditor() { return false; }
    public virtual void Editor_DrawEditor() { }

    public Type[] File_GetFavoriteSubTypes()
    {
        return new[]
        {
            typeof(A_Texture),
            typeof(A_Mesh),
            typeof(A_Sound),
            typeof(A_Scene),
            typeof(A_Font),
        };
    }
}