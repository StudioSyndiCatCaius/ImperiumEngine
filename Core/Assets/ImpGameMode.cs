using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumCore.Assets;

public class ImpGameMode : ImpAsset
{
    [ImpVar][Exposed] List<TClass<ImpGameState>> States_Preload;
    [ImpVar][Exposed] TClass<ImpGameState> States_Load;
    [ImpVar][Exposed] List<TClass<ImpGameState>> States_Postload;
    [ImpVar][Exposed] List<TClass<ImpGameState>> States_Persistent;
    [ImpVar][Exposed] float PersistentStateFrequancy = 0.2f; // Frequnecy in seconds the persistent states will try to be activated
}