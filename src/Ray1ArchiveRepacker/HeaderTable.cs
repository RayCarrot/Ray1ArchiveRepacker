public class HeaderTable(long exeFileOffset, List<FileEntry> fileEntries)
{
    public long ExeFileOffset { get; } = exeFileOffset;
    public List<FileEntry> FileEntries { get; } = fileEntries;
}