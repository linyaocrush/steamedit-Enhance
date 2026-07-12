#nullable enable
using Common;

namespace steamedit_Enhance.Data;

internal interface IModificationService
{
    bool UseCompleted { get; set; }
    bool UseHidden { get; set; }
    IFileSystem FileSystem { get; set; }
}
