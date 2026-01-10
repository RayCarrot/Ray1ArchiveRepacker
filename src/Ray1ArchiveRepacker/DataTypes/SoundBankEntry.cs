public readonly struct SoundBankEntry(int fileOffset, int fileSize, int loopStart, int loopLength)
{
    public int FileOffset { get; } = fileOffset;
    public int FileSize { get; } = fileSize;
    public int LoopStart { get; } = loopStart;
    public int LoopLength { get; } = loopLength;

    public static SoundBankEntry Read(Reader reader)
    {
        int fileOffset = reader.ReadInt32();
        int fileSize = reader.ReadInt32();
        int loopStart = reader.ReadInt32();
        int loopLength = reader.ReadInt32();

        return new SoundBankEntry(fileOffset, fileSize, loopStart, loopLength);
    }

    public void Write(Writer writer)
    {
        writer.Write(FileOffset);
        writer.Write(FileSize);
        writer.Write(LoopStart);
        writer.Write(LoopLength);
    }
}