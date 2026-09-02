using Engine.Core;
using Engine.Enums;

namespace Engine.Files;

public class File_MP3 : ImpFile
{
    public File_MP3() { file_type = EFileType.Sound; }
}