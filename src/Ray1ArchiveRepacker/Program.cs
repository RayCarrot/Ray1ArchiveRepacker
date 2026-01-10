// Define constants
const string ExeFileName = "RAYMAN.EXE";
const string VignetteArchiveFileName = "VIGNET.DAT";
const string LanguageArchiveFileName = "RAY.LNG";
const string SoundBankHeadersFileName = "SNDH8B.DAT";
const string SoundBankDatasFileName = "SNDD8B.DAT";
const string SoundBankExtractedName = "SND8B";
const string VignetteSoundBankFileName = "SNDVIG.DAT";
const int SoundBankLength = 128;

void ShowHelpScreen()
{
    Console.WriteLine("Rayman 1 Archive Repacker\n" +
                      "\n" +
                      "Usage:\n" +
                      "  -e <game-path> <output-path> | Extracts all the archives to the output path\n" +
                      "  -r <game-path> <input-path>  | Repacks all the archives from the input path");
}

// Parse args
if (args.Length != 3)
{
    ShowHelpScreen();
    return;
}

string mode = args[0];
string gameDir = args[1];
string unpackedDir = args[2];

if (mode is not ("-e" or "-r") || !Directory.Exists(gameDir))
{
    ShowHelpScreen();
    return;
}

// Get the exe file path
string exeFilePath = Path.Combine(gameDir, ExeFileName);

// Read the exe file
byte[] exeBuffer = File.ReadAllBytes(exeFilePath);
using MemoryStream exeStream = new(exeBuffer);
using Reader exeReader = new(exeStream);

// Verify it's not compressed
exeReader.BaseStream.Position = 0x2730;
string type = exeReader.ReadNullDelimitedString(System.Text.Encoding.ASCII);
if (type != "LE")
{
    ConsoleHelpers.WriteError("The EXE file is compressed! Please decompress it before running this tool.");
    return;
}

// Extract
if (mode == "-e")
{
    // Extract vignette pcx files
    Archive.ExtractArchive(exeReader, Path.Combine(gameDir, VignetteArchiveFileName), unpackedDir, ".pcx", null);

    // Extract language txt files
    Archive.ExtractArchive(exeReader, Path.Combine(gameDir, LanguageArchiveFileName), unpackedDir, ".txt", null);

    // Extract 8-bit sound banks
    HeaderTable? soundBankHeadersHeaderTable = Archive.ReadHeaderTable(exeReader, Path.Combine(gameDir, SoundBankHeadersFileName));
    if (soundBankHeadersHeaderTable != null)
    {
        HeaderTable? soundBankDatasHeaderTable = Archive.ReadHeaderTable(exeReader, Path.Combine(gameDir, SoundBankDatasFileName));
        if (soundBankDatasHeaderTable != null)
        {
            using FileStream soundBankHeadersFileStream = File.OpenRead(Path.Combine(gameDir, SoundBankHeadersFileName));
            using FileStream soundBankDatasFileStream = File.OpenRead(Path.Combine(gameDir, SoundBankDatasFileName));

            // Extract each sound bank
            for (int i = 0; i < soundBankHeadersHeaderTable.FileEntries.Count; i++)
            {
                Console.WriteLine($"Extracting sound bank {i}");

                string bankOutputDir = Path.Combine(unpackedDir, SoundBankExtractedName, $"{i}");
                Directory.CreateDirectory(bankOutputDir);

                // Read the bank header and data
                byte[] soundBankHeaderBuffer = Archive.ReadFileFromArchive(soundBankHeadersFileStream, soundBankHeadersHeaderTable.FileEntries[i]);
                byte[] soundBankDataBuffer = Archive.ReadFileFromArchive(soundBankDatasFileStream, soundBankDatasHeaderTable.FileEntries[i]);

                using MemoryStream soundBankHeaderStream = new(soundBankHeaderBuffer);
                using Reader soundBankHeaderReader = new(soundBankHeaderStream);

                // Extract each sound from the bank
                for (int j = 0; j < SoundBankLength; j++)
                {
                    // Read the header entry
                    SoundBankEntry soundBankEntry = SoundBankEntry.Read(soundBankHeaderReader);

                    // Skip if empty
                    if (soundBankEntry.FileSize == 0)
                        continue;

                    // Read the data
                    byte[] dataBuffer = new byte[soundBankEntry.FileSize];
                    Array.Copy(soundBankDataBuffer, soundBankEntry.FileOffset, dataBuffer, 0, soundBankEntry.FileSize);

                    // Write as a wav file
                    using FileStream wavFileStream = File.OpenWrite(Path.Combine(bankOutputDir, $"{j}.wav"));
                    using Writer wavWriter = new(wavFileStream);
                    new Wav(dataBuffer, 1, 1, 11025, 8).Write(wavWriter);

                    // Write loop data if there is any
                    if (soundBankEntry.LoopLength != 0)
                        File.WriteAllText(Path.Combine(bankOutputDir, $"{j}_Loop.txt"), $"{soundBankEntry.LoopStart}-{soundBankEntry.LoopLength}");
                }

                Console.WriteLine("Finished extracting");
            }
        }
    }

    // Extract vignette sound banks
    Archive.ExtractArchive(exeReader, Path.Combine(gameDir, VignetteSoundBankFileName), unpackedDir, ".wav", data =>
    {
        using MemoryStream wavStream = new();
        using Writer wavWriter = new(wavStream);

        new Wav(data, 1, 1, 11025, 16).Write(wavWriter);

        return wavStream.ToArray();
    });
}
// Repack
else if (mode == "-r")
{
    // Verify the files exist
    if (!Directory.Exists(Path.Combine(unpackedDir, Path.GetFileNameWithoutExtension(VignetteArchiveFileName))) ||
        !Directory.Exists(Path.Combine(unpackedDir, Path.GetFileNameWithoutExtension(LanguageArchiveFileName))) ||
        !Directory.Exists(Path.Combine(unpackedDir, Path.GetFileNameWithoutExtension(SoundBankExtractedName))) ||
        !Directory.Exists(Path.Combine(unpackedDir, Path.GetFileNameWithoutExtension(VignetteSoundBankFileName))))
    {
        ConsoleHelpers.WriteWarning($"WARNING: No extracted files found in {unpackedDir}");
        return;
    }

    using FileStream exeFileStream = File.OpenWrite(exeFilePath);
    using Writer exeWriter = new(exeFileStream);

    // Repack vignette pcx files
    Archive.RepackArchive(exeReader, exeWriter, Path.Combine(gameDir, VignetteArchiveFileName), unpackedDir, ".pcx", null);

    // Repack language txt files
    Archive.RepackArchive(exeReader, exeWriter, Path.Combine(gameDir, LanguageArchiveFileName), unpackedDir, ".txt", null);

    // Repack 8-bit sound banks
    HeaderTable? soundBankHeadersHeaderTable = Archive.ReadHeaderTable(exeReader, Path.Combine(gameDir, SoundBankHeadersFileName));
    if (soundBankHeadersHeaderTable != null)
    {
        HeaderTable? soundBankDatasHeaderTable = Archive.ReadHeaderTable(exeReader, Path.Combine(gameDir, SoundBankDatasFileName));
        if (soundBankDatasHeaderTable != null)
        {
            string tempSoundBankHeadersFilePath = $"{Path.Combine(gameDir, SoundBankHeadersFileName)}.tmp";
            string tempSoundBankDatasFilePath = $"{Path.Combine(gameDir, SoundBankDatasFileName)}.tmp";

            using (FileStream tempSoundBankHeadersFileStream = File.OpenWrite(tempSoundBankHeadersFilePath))
            {
                using (FileStream tempSoundBankDatasFileStream = File.OpenWrite(tempSoundBankDatasFilePath))
                {
                    // Repack each sound bank
                    for (int i = 0; i < soundBankHeadersHeaderTable.FileEntries.Count; i++)
                    {
                        Console.WriteLine($"Repacking sound bank {i}");

                        string bankOutputDir = Path.Combine(unpackedDir, SoundBankExtractedName, $"{i}");

                        using MemoryStream soundBankHeaderStream = new();
                        using Writer soundBankHeaderWriter = new(soundBankHeaderStream);

                        using MemoryStream soundBankDataStream = new();

                        // Repack each sound to the bank
                        for (int j = 0; j < SoundBankLength; j++)
                        {
                            string wavFilePath = Path.Combine(bankOutputDir, $"{j}.wav");
                            string wavLoopFilePath = Path.Combine(bankOutputDir, $"{j}_Loop.txt");

                            if (File.Exists(wavFilePath))
                            {
                                byte[]? dataBuffer = null;

                                using (FileStream wavFileStream = File.OpenRead(wavFilePath))
                                {
                                    using Reader wavReader = new(wavFileStream);
                                    Wav wav = Wav.Read(wavReader);

                                    if (wav.FormatType != 1)
                                        ConsoleHelpers.WriteError("ERROR: WAV file must contain uncompressed PCM audio");
                                    else if (wav.ChannelsCount != 1)
                                        ConsoleHelpers.WriteError("ERROR: WAV audio must be in mono");
                                    else if (wav.SampleRate != 11025)
                                        ConsoleHelpers.WriteError("ERROR: WAV audio sample rate must be 11025");
                                    else if (wav.BitsPerSample != 8)
                                        ConsoleHelpers.WriteError("ERROR: WAV sfx audio must be 8-bit");
                                    else
                                        dataBuffer = wav.Data;
                                }

                                if (dataBuffer != null)
                                {
                                    // Read loop data if there is any
                                    int loopStart = 0;
                                    int loopLength = 0;
                                    if (File.Exists(wavLoopFilePath))
                                    {
                                        string loopTxt = File.ReadAllText(wavLoopFilePath);
                                        string[] strValues = loopTxt.Split('-');
                                        loopStart = Int32.TryParse(strValues[0], out int v1) ? v1 : 0;
                                        loopLength = Int32.TryParse(strValues[1], out int v2) ? v2 : 0;
                                    }

                                    // Write new entry
                                    new SoundBankEntry((int)soundBankDataStream.Position, dataBuffer.Length, loopStart, loopLength).Write(soundBankHeaderWriter);

                                    // Write data
                                    soundBankDataStream.Write(dataBuffer);
                                }
                                else
                                {
                                    // Write empty entry
                                    new SoundBankEntry(0, 0, 0, 0).Write(soundBankHeaderWriter);
                                }
                            }
                            else
                            {
                                // Write empty entry
                                new SoundBankEntry(0, 0, 0, 0).Write(soundBankHeaderWriter);
                            }
                        }

                        // Write the sound bank files and update the file entries
                        soundBankHeadersHeaderTable.FileEntries[i] = Archive.WriteFileToArchive(tempSoundBankHeadersFileStream, soundBankHeaderStream.ToArray(), soundBankHeadersHeaderTable.FileEntries[i].XorKey);
                        soundBankDatasHeaderTable.FileEntries[i] = Archive.WriteFileToArchive(tempSoundBankDatasFileStream, soundBankDataStream.ToArray(), soundBankDatasHeaderTable.FileEntries[i].XorKey);

                        Console.WriteLine("Finished repacking");
                    }
                }
            }

            // Update the header tables
            Archive.WriteHeaderTable(exeWriter, soundBankHeadersHeaderTable);
            Archive.WriteHeaderTable(exeWriter, soundBankDatasHeaderTable);

            // Replace the archive
            File.Move(tempSoundBankHeadersFilePath, Path.Combine(gameDir, SoundBankHeadersFileName), true);
            File.Move(tempSoundBankDatasFilePath, Path.Combine(gameDir, SoundBankDatasFileName), true);

            Console.WriteLine("Finished repacking sound banks");
        }
    }

    // Repack vignette sound banks
    Archive.RepackArchive(exeReader, exeWriter, Path.Combine(gameDir, VignetteSoundBankFileName), unpackedDir, ".wav", data =>
    {
        using MemoryStream wavStream = new(data);
        using Reader wavReader = new(wavStream);

        Wav wav = Wav.Read(wavReader);

        if (wav.FormatType != 1)
        {
            ConsoleHelpers.WriteError("ERROR: WAV file must contain uncompressed PCM audio");
            return null;
        }

        if (wav.ChannelsCount != 1)
        {
            ConsoleHelpers.WriteError("ERROR: WAV audio must be in mono");
            return null;
        }

        if (wav.SampleRate != 11025)
        {
            ConsoleHelpers.WriteError("ERROR: WAV audio sample rate must be 11025");
            return null;
        }

        if (wav.BitsPerSample != 16)
        {
            ConsoleHelpers.WriteError("ERROR: WAV vignette audio must be 16-bit");
            return null;
        }

        return wav.Data;
    });
}
else
{
    ShowHelpScreen();
}

ConsoleHelpers.WriteSuccess("Complete");