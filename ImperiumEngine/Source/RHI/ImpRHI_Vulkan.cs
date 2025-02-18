using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace ImperiumEngine.Source.RHI;

public class ImpRHI_Vulkan : ImpRHI_SILK
{
    public ImpRHI_Vulkan(string title, Vector2 windowSize) : base(title, windowSize) { }
    
    private Vk      _vk;
    protected override void On_PreBegin()
    {
        _vk = Vk.GetApi();
        base.Native_Begin();
    }

    public override void SILK_End()
    {
        _vk.Dispose();
        base.SILK_End();
    }
    
}