namespace ImperiumEngine.Source.Objects._2D;

public class O2D_PropertyList: ImpComponent2D
{
    public ImpObject _object;
    public List<O2D_Property> wg_properties = new List<O2D_Property>();
}

// ========================================================================================
// ========================================================================================

public abstract class O2D_Property: ImpComponent2D
{
    public ImpObject _object { get; set; }
    public string _propertyName { get; set; }

}

public class O2D_Property_bool: O2D_Property
{
    
}

public class O2D_Property_int: O2D_Property
{
    
}

public class O2D_Property_float: O2D_Property
{
    
}

public class O2D_Property_object: O2D_Property
{
    
}

public class O2D_Property_asset: O2D_Property
{
    
}