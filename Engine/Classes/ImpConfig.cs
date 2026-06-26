using ImperiumCore.Const;
using ImperiumEngine.Interfaces;

namespace ImperiumCore.Classes;

//baseclasse for a settings category file. ANY changes to a property marked with `ImpVar` will be saved automatically to disk
public abstract class ImpConfig : ImpObject, I_Saveable
{
    // ----------------------------------------------------------------------------------------------------
    // STATICS
    // ----------------------------------------------------------------------------------------------------

    // A config's state lives in its TOML file at all times, so this just binds an instance to that file:
    // construct, create the file with defaults if missing, then load the current ImpVars off disk.
    public static T Get<T>() where T : ImpConfig, new()
    {
        var _cfg = new T();
        var _path = _cfg.Config_GetSavePath();

        if (!File.Exists(_path)) ((I_Saveable)_cfg).Savable_OnWrite(_path);
        else                     ((I_Saveable)_cfg).Savable_OnRead(_path);

        return _cfg;
    }

    // ----------------------------------------------------------------------------------------------------
    // Implementation
    // ----------------------------------------------------------------------------------------------------

    /*
     * returns the directory where the config file should be saved.
     * E.G. return "C:\MyProject\Config" with classname "CFG_Test" means the output file will be "C:\MyProject\Config\CFG_Test.TOML"
     */
    public virtual string Config_GetSaveDir()
    {
        return ImpFile.Dir_ProjectConfig();
    }

    // full path of this config's backing TOML file (e.g. ".../Config/CFG_Test.TOML")
    public string Config_GetSavePath()
    {
        return Path.Combine(Config_GetSaveDir(), GetType().Name + ImpC_String.EXT_CONFIG);
    }

    public void Savable_OnRead(string path)  => ImpSave.Read(this, path);
    public void Savable_OnWrite(string path) => ImpSave.Write(this, path);
}