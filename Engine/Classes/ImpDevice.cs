namespace ImperiumCore.Classes;

public enum EDeviceType
{
    Keyboard,
    Mouse,
    Gamepad,
    Touch,
    Disk,
}

public class ImpDevice
{
    public EDeviceType type;
}