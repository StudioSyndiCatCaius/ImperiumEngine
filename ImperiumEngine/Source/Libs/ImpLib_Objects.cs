namespace ImperiumEngine.Source.Libs;

public static class ImpLib_Objects
{
    // ===============================================================================================
    // OBJECTS
    // ===============================================================================================
    
    // Filter By Tag
    public static List<ImpObject> Object_FilterByTags(List<ImpObject> objects, List<string> tags, 
        bool bRequireAll=false, bool bExclude=false)
    {
        List<ImpObject> output = new();
        foreach (var o in objects)
        {
            // if object has ALL tags
            if (bRequireAll)
            {
                bool success = true;
                foreach (var tag in tags)
                {
                    if (o.Tags.Contains(tag)!=bExclude)
                    {
                        success = false;
                        break;
                    }
                }
                if (success)
                {
                    output.Add(o);
                }
            }
            else
            {
                foreach (var tag in tags)
                {
                    if (o.Tags.Contains(tag)!=bExclude)
                    {
                        output.Add(o);
                        break;
                    }
                }
            }
        }

        return output;
    }
}