using Engine.Core;
using Engine.Enums;

namespace Engine.Files;

public class File_PNG : ImpFile
{
    public File_PNG() { file_type = EFileType.Texture; }
}