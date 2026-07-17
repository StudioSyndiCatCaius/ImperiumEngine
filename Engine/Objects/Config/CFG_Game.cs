using ImperiumEngine.Classes;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects.Config;

public class CFG_Game : ImpConfig
{
    public TText title;
    public TText description;
    public Guid game_id = Guid.NewGuid();
    
    
}