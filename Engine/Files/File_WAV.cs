using Engine.Core;
using Engine.Enums;

namespace Engine.Files;

public class File_WAV : ImpFile
{
    public File_WAV() { file_type = EFileType.Sound; }
}