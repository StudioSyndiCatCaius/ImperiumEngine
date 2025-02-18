

using System;
using System.Numerics;
using ImGuiNET;
using ImperiumEngine.Source;
using ImperiumEngine.Source.Objects._2D;
using ImperiumEngine.Source.RHI;

namespace ImperiumEngine;

public class Program()
{
    private static ImpRHI       RHI;
    private static ImpComponent RootObject;
    
    static void Main(string[] args)
    {
        RHI = new ImpRHI_Raylib("Imperium Engine", new Vector2(1280f,720f));
        
        RHI.Begin += App_Begin;
        RHI.Update += App_Update;
        RHI.Render += App_Render;
        RHI.End += App_End;

        RootObject = new ImpComponent1D();
        RootObject.Child_Add(new O2D_SceneView());
        RHI.Run();
    }
    private static void App_Begin()
    {
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        RootObject.Native_Begin();
    }
    
    private static void App_Update(double delta)
    {
        RootObject.Native_Update(delta);
    }
    
    private static void App_Render(double delta)
    {
        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport().ID);
        RootObject.Native_Draw(delta);
    }
    
    private static void App_End()
    {
        RootObject?.Dispose();
    }
}