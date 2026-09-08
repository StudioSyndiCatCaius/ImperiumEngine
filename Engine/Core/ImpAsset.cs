using System.Collections;
using System.Reflection;
using Engine.Assets;
using Engine.Globals;
using Engine.Interfaces;
using Engine.Structs;
using Engine;

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

    private ImpAsset progenitor; //used for CLONES, just to reference the original asset

    
    public void Save(bool force=false)
    {
        if (!force & !is_dity) return;
        if (!CanSave()) return;
        
        TTable _tbl=To_Table();
        string file_str=TTable.ToTOML(_tbl);
        
        File.WriteAllText(GFile.Make_Path_Absolute(filepath), file_str);
        is_dity=false;
        GLog.Info("Saved " + filepath);
    }

    public virtual bool CanSave()
    {
        return !string.IsNullOrEmpty(filepath);
    }
    
    // Reload native data from disk. GFile.Import already loads new files;
    // calling Reimport on first bind double-loads R3D models and can AV in LoadModelEx.
    [CallInEditor]
    [Title("Reimport")]
    public void Source_Reimport()
    {
        if (string.IsNullOrEmpty(sourcefile)) return;
        bool already = _src_file_ref != null || App.files.ContainsKey(new TFile(sourcefile));
        _src_file_ref = GFile.Import<ImpFile>(sourcefile);
        if (_src_file_ref != null)
        {
            if (already) _src_file_ref.Reimport();
            OnReimport(_src_file_ref);
        }
        else GLog.Error($"Could not find sourcefile {sourcefile}");
    }
    
    public ImpFile Source_Get()
    {
        if (_src_file_ref == null && !string.IsNullOrEmpty(sourcefile))
        {
            _src_file_ref = GFile.Import<ImpFile>(sourcefile);
            if (_src_file_ref != null) OnReimport(_src_file_ref);
            else GLog.Error($"Could not find sourcefile {sourcefile}");
        }
        return _src_file_ref;
    }
    
    public virtual void OnReimport(ImpFile src) { }

    public ImpAsset Clone(bool deep = false)
    {
        if (Activator.CreateInstance(GetType()) is not ImpAsset clone)
            return this;

        clone.From_Table(To_Table());
        clone.sourcefile = sourcefile;
        clone.filepath = filepath;
        clone.is_inlined = is_inlined;
        clone.is_dity = false;
        clone._src_file_ref = _src_file_ref;
        clone.progenitor = this;

        foreach (FieldInfo f in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!f.IsDefined(typeof(ImpVarAttribute), true)) continue;
            object val = f.GetValue(this);
            if (val is ImpAsset nested)
                f.SetValue(clone, deep ? nested.Clone(true) : nested);
            else if (deep && val is IList src_list && f.GetValue(clone) is IList dst_list)
            {
                int n = Math.Min(src_list.Count, dst_list.Count);
                for (int i = 0; i < n; i++)
                    if (src_list[i] is ImpAsset item)
                        dst_list[i] = item.Clone(true);
            }
            else if (deep && val is IDictionary src_map && f.GetValue(clone) is IDictionary dst_map)
            {
                foreach (DictionaryEntry e in src_map)
                    if (e.Key != null && e.Value is ImpAsset item)
                        dst_map[e.Key] = item.Clone(true);
            }
        }
        return clone;
    }

    public bool Matches(ImpAsset other)
    {
        if(this==other) return true;
        if(this.progenitor==other) return true;
        return false;
    }

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