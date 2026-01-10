public static class Archive
{
    private static void XorFileBuffer(byte[] fileBuffer, byte xorKey)
    {
        for (int i = 0; i < fileBuffer.Length; i++)
            fileBuffer[i] ^= xorKey;
    }

    private static byte CalculateFileBufferChecksum(byte[] fileBuffer)
    {
        byte checksum = 0;
        for (int i = 0; i < fileBuffer.Length; i++)
            checksum += fileBuffer[i];
        return checksum;
    }

    public static HeaderTable? ReadHeaderTable(Reader exeReader, string archiveFilePath)
    {
        long archiveFileSize = new FileInfo(archiveFilePath).Length;

        exeReader.BaseStream.Position = 0;
        while (exeReader.BaseStream.Position < exeReader.BaseStream.Length - 12)
        {
            long streamPos = exeReader.BaseStream.Position;

            List<FileEntry> fileEntries = new();
            long fileOffset = 0;
            while (exeReader.BaseStream.Position < exeReader.BaseStream.Length - 12)
            {
                FileEntry fileEntry = FileEntry.Read(exeReader);
                if (fileEntry.FileOffset == fileOffset && fileEntry.FileSize > 0 && fileEntry.Padding == 0)
                {
                    fileEntries.Add(fileEntry);
                    fileOffset += fileEntry.FileSize;

                    if (fileOffset == archiveFileSize)
                    {
                        Console.WriteLine($"Found header table at 0x{streamPos:X8} for {archiveFilePath}");
                        return new HeaderTable(streamPos, fileEntries);
                    }
                }
                else
                {
                    break;
                }
            }

            exeReader.BaseStream.Position = streamPos + 4;
        }

        ConsoleHelpers.WriteError($"ERROR: Failed to find header table for {archiveFilePath}");

        return null;
    }

    public static void WriteHeaderTable(Writer exeWriter, HeaderTable headerTable)
    {
        exeWriter.BaseStream.Position = headerTable.ExeFileOffset;
        foreach (FileEntry fileEntry in headerTable.FileEntries)
            fileEntry.Write(exeWriter);
    }

    public static byte[] ReadFileFromArchive(Stream archiveStream, FileEntry fileEntry)
    {
        // Read the file
        byte[] fileBuffer = new byte[fileEntry.FileSize];
        archiveStream.Position = fileEntry.FileOffset;
        archiveStream.ReadExactly(fileBuffer);

        // Verify the checksum
        byte checksum = CalculateFileBufferChecksum(fileBuffer);
        if (checksum != fileEntry.Checksum)
            ConsoleHelpers.WriteWarning("Warning: Checksum doesn't match!");

        // Decode the file
        XorFileBuffer(fileBuffer, fileEntry.XorKey);

        return fileBuffer;
    }

    public static FileEntry WriteFileToArchive(Stream archiveFileStream, byte[] fileBuffer, byte xorKey)
    {
        // Encode the file
        XorFileBuffer(fileBuffer, xorKey);

        // Calculate the checksum
        byte checksum = CalculateFileBufferChecksum(fileBuffer);

        // Update the file entry
        FileEntry fileEntry = new((int)archiveFileStream.Position, fileBuffer.Length, xorKey, checksum, 0);

        // Write the file to the archive
        archiveFileStream.Write(fileBuffer);

        return fileEntry;
    }

    public static void ExtractArchive(Reader exeReader, string archiveFilePath, string outputDir, string fileExtension, Func<byte[], byte[]?>? dataProcessor)
    {
        // Read the header table in the exe
        HeaderTable? headerTable = ReadHeaderTable(exeReader, archiveFilePath);

        if (headerTable == null)
            return;

        Console.WriteLine($"Extracting {headerTable.FileEntries.Count} files from {archiveFilePath}");

        outputDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(archiveFilePath));
        Directory.CreateDirectory(outputDir);

        using FileStream archiveFileStream = File.OpenRead(archiveFilePath);

        // Extract every file
        for (int i = 0; i < headerTable.FileEntries.Count; i++)
        {
            FileEntry fileEntry = headerTable.FileEntries[i];

            // Read the file
            byte[]? fileBuffer = ReadFileFromArchive(archiveFileStream, fileEntry);

            // Optionally process the data
            if (dataProcessor != null)
                fileBuffer = dataProcessor(fileBuffer);

            if (fileBuffer == null)
                return;

            // Write the file
            File.WriteAllBytes(Path.Combine(outputDir, $"{i}{fileExtension}"), fileBuffer);
        }

        Console.WriteLine("Finished extracting");
    }

    public static void RepackArchive(Reader exeReader, Writer exeWriter, string archiveFilePath, string inputDir, string fileExtension, Func<byte[], byte[]?>? dataProcessor)
    {
        // Read the header table in the exe
        HeaderTable? headerTable = ReadHeaderTable(exeReader, archiveFilePath);

        if (headerTable == null)
            return;

        Console.WriteLine($"Repacking {headerTable.FileEntries.Count} files to {archiveFilePath}");

        inputDir = Path.Combine(inputDir, Path.GetFileNameWithoutExtension(archiveFilePath));
        Directory.CreateDirectory(inputDir);

        string tempArchiveFilePath = $"{archiveFilePath}.tmp";

        // Repack every file
        using (FileStream archiveFileStream = File.OpenWrite(tempArchiveFilePath))
        {
            for (int i = 0; i < headerTable.FileEntries.Count; i++)
            {
                string inputFilePath = Path.Combine(inputDir, $"{i}{fileExtension}");

                if (!File.Exists(inputFilePath))
                {
                    ConsoleHelpers.WriteError($"Error: File {inputFilePath} not found");
                    return;
                }

                byte[]? fileBuffer = File.ReadAllBytes(inputFilePath);

                // Optionally process the data
                if (dataProcessor != null)
                    fileBuffer = dataProcessor(fileBuffer);

                if (fileBuffer == null)
                    return;

                // Write the file and update the file entry
                headerTable.FileEntries[i] = WriteFileToArchive(archiveFileStream, fileBuffer, headerTable.FileEntries[i].XorKey);
            }
        }

        // Update the header table
        WriteHeaderTable(exeWriter, headerTable);

        // Replace the archive
        File.Move(tempArchiveFilePath, archiveFilePath, true);

        Console.WriteLine("Finished repacking");
    }
}