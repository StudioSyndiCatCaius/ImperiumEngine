namespace ImperiumEngine.Source.Cores;



public static class ImpLib_Assets
{
    static public Dictionary<Guid, string> AssetPaths;
    static public List<ImpAsset> AssetRegistry_List;
    
    public static ImpAsset GetAsset_FromPath(string path)
    {
        foreach (var asset in AssetRegistry_List)
        {
            if (asset != null && AssetPaths[asset.AssetGuid]==path)
            {
                return asset;
            }
        }
        return null;
    }
    
    public static ImpAsset GetAsset_FromGUID(Guid guid)
    {
        foreach (var asset in AssetRegistry_List)
        {
            if (asset != null && asset.AssetGuid==guid)
            {
                return asset;
            }
        }
        return null;
    }
    
    public static List<ImpAsset> GetAssets_InPath(string path, bool bRecursive)
    {
        return null;
    }
}