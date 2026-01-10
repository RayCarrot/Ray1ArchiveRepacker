using System.Text;

public readonly struct Wav(byte[] data, int formatType, int channelsCount, int sampleRate, int bitsPerSample)
{
    public byte[] Data { get; } = data;
    public int FormatType { get; } = formatType;
    public int ChannelsCount { get; } = channelsCount;
    public int SampleRate { get; } = sampleRate;
    public int BitsPerSample { get; } = bitsPerSample;

    public static Wav Read(Reader reader)
    {
        if (reader.ReadString(4, Encoding.ASCII) != "RIFF")
            throw new Exception("Invalid WAV file");

        uint fileSize = reader.ReadUInt32();

        long endPosition = reader.BaseStream.Position + fileSize;

        if (reader.ReadString(4, Encoding.ASCII) != "WAVE")
            throw new Exception("Invalid WAV file");

        byte[]? data = null;
        int formatType = 0;
        int channelsCount = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;

        // Read every chunk
        while (reader.BaseStream.Position < endPosition)
        {
            string chunkId = reader.ReadString(4, Encoding.ASCII);
            uint chunkSize = reader.ReadUInt32();

            long chunkPos = reader.BaseStream.Position;

            if (chunkId == "fmt ")
            {
                formatType = reader.ReadUInt16();
                channelsCount = reader.ReadUInt16();
                sampleRate = (int)reader.ReadUInt32();
                reader.ReadUInt32(); // Byte rate
                reader.ReadUInt16(); // Block align
                bitsPerSample = reader.ReadUInt16();
            }
            else if (chunkId == "data")
            {
                data = reader.ReadBytes((int)chunkSize);
            }

            reader.BaseStream.Position = chunkPos + chunkSize;
        }

        if (data == null)
            throw new Exception("WAV file has no data");

        return new Wav(data, formatType, channelsCount, sampleRate, bitsPerSample);
    }

    public void Write(Writer writer)
    {
        // Write the RIFF header
        writer.WriteString("RIFF", 4, Encoding.ASCII);
        writer.Write((uint)(36 + Data.Length)); // File size
        writer.WriteString("WAVE", 4, Encoding.ASCII);

        // Write the format chunk
        writer.WriteString("fmt ", 4, Encoding.ASCII);
        writer.Write((uint)16); // Chunk size
        writer.Write((ushort)FormatType); // Format type
        writer.Write((ushort)ChannelsCount); // Channels count
        writer.Write((uint)SampleRate); // Sample rate
        writer.Write((uint)(SampleRate * BitsPerSample * ChannelsCount) / 8); // Byte rate
        writer.Write((ushort)((BitsPerSample * ChannelsCount) / 8)); // Block align
        writer.Write((ushort)BitsPerSample); // Bits per sample

        // Write the data chunk
        writer.WriteString("data", 4, Encoding.ASCII);
        writer.Write((uint)Data.Length); // Chunk size
        writer.Write(Data);
    }
}