public readonly struct FileEntry(int fileOffset, int fileSize, byte xorKey, byte checksum, short padding)
{
    public int FileOffset { get; } = fileOffset;
    public int FileSize { get; } = fileSize;
    public byte XorKey { get; } = xorKey;
    public byte Checksum { get; } = checksum;
    public short Padding { get; } = padding;

    public static FileEntry Read(Reader reader)
    {
        int fileOffset = reader.ReadInt32();
        int fileSize = reader.ReadInt32();
        byte xorKey = reader.ReadByte();
        byte checksum = reader.ReadByte();
        short padding = reader.ReadInt16();

        return new FileEntry(fileOffset, fileSize, xorKey, checksum, padding);
    }

    public void Write(Writer writer)
    {
        writer.Write(fileOffset);
        writer.Write(fileSize);
        writer.Write(xorKey);
        writer.Write(checksum);
        writer.Write(padding);
    }
}