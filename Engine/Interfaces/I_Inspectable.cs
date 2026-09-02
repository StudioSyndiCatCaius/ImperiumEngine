namespace Engine.Interfaces;

//Object that can be inspected in the inspector
public interface I_Inspectable
{
    public virtual void Inspectable_OnPropertyEdit(string name, object oldValue, object newValue) {}
}