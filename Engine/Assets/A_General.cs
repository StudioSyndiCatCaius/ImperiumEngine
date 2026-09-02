using Engine.Interfaces;
using Engine;
using Engine.Core;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Assets;

public class A_General : ImpAsset, I_General
{
    [ImpVar][Category("General")] public string title;
    [ImpVar][Category("General")] public A_Texture icon;
    [ImpVar][Category("General")] public string description;
    [ImpVar][Category("General")] public Color color;
    [ImpVar][Category("General")] public TTagSet tags = new();
    
    public string getTitle() { return title; }
    public A_Texture getIcon() { return icon; }
    public string getDescription() { return description; }
    public TTagSet getTags() { return tags; }
    public Color getColor() { return color; }
    public TLabel getLabel() { return ""; } //replace with default as filename (E.G. if saved_file is "{game}/Assets/MyAsset.ImpAsset" then the label is "MyAsset"
}