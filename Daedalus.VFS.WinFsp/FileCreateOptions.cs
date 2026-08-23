using Fsp;

namespace Daedalus.VFS.WinFsp;

[Flags]
public enum FileCreateOptions : uint
{
    DirectoryFile = FileSystemBase.FILE_DIRECTORY_FILE,
    WriteThrough = FileSystemBase.FILE_WRITE_THROUGH,
    SequentialOnly = FileSystemBase.FILE_SEQUENTIAL_ONLY,
    NoIntermediateBuffering = FileSystemBase.FILE_NO_INTERMEDIATE_BUFFERING,
    SynchronousIoAlert = FileSystemBase.FILE_SYNCHRONOUS_IO_ALERT,
    SynchronousIoNonAlert = FileSystemBase.FILE_SYNCHRONOUS_IO_NONALERT,
    NonDirectoryFile = FileSystemBase.FILE_NON_DIRECTORY_FILE,
    CreateTreeConnection = FileSystemBase.FILE_CREATE_TREE_CONNECTION,
    CompleteIfOplocked = FileSystemBase.FILE_COMPLETE_IF_OPLOCKED,
    NoEAKnowledge = FileSystemBase.FILE_NO_EA_KNOWLEDGE,
    OpenRemoteInstance = FileSystemBase.FILE_OPEN_REMOTE_INSTANCE,
    RandomAccess = FileSystemBase.FILE_RANDOM_ACCESS,
    DeleteOnClose = FileSystemBase.FILE_DELETE_ON_CLOSE,
    OpenByFileID = FileSystemBase.FILE_OPEN_BY_FILE_ID,
    OpenForBackupIntent = FileSystemBase.FILE_OPEN_FOR_BACKUP_INTENT,
    NoCompression = FileSystemBase.FILE_NO_COMPRESSION,
    OpenRequiringOplock = FileSystemBase.FILE_OPEN_REQUIRING_OPLOCK,
    ReserveOpfilter = FileSystemBase.FILE_RESERVE_OPFILTER,
    OpenReparsePoint = FileSystemBase.FILE_OPEN_REPARSE_POINT,
    OpenNoRecall = FileSystemBase.FILE_OPEN_NO_RECALL,
    OpenForFreeSpaceQuery = FileSystemBase.FILE_OPEN_FOR_FREE_SPACE_QUERY,
}
