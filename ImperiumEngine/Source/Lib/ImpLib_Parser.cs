namespace ImperiumEngine.Source.Cores;

public class ImpParser
{
    public virtual void SetParam_Bool(string param, bool value) {}
    public virtual bool GetParam_Bool(string param) {return false;}
    
    public virtual void SetParam_Int32(string param, Int32 value) {}
    public virtual Int32 GetParam_Int32(string param) { return 0;}
    
    public virtual void SetParam_Float(string param, float value) {}
    public virtual float GetParam_Float(string param) { return 0;}
}