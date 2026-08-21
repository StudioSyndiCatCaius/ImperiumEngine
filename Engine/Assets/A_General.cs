using ImperiumEngine.Interfaces;
using ImperiumEngine;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Assets;

public class A_General : ImpAsset, I_General
{
    [ImpVar][Category("General")] public TText title;
    [ImpVar][Category("General")] public A_Texture icon;
    [ImpVar][Category("General")] public TText description;
    [ImpVar][Category("General")] public Color color;
    [ImpVar][Category("General")] public TTagSet tags = new();
    
    public virtual TText gTitle() { return title; }
    public virtual A_Texture gIcon() { return icon; }
    public virtual TText gDescription() { return description; }
    public virtual TTagSet gTags() { return tags; }
    public virtual Color gColor() { return color; }
    public virtual TLabel gLabel() { return ""; } //replace with default as filename (E.G. if saved_file is "{game}/Assets/MyAsset.ImpAsset" then the label is "MyAsset"
}