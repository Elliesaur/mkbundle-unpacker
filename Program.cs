using System.Text;

namespace MkBundleExtractor
{
    internal class Program
    {
        const string WELL_KNOWN_MKBUNDLE_PATTERN = "xmonkeysloveplay";

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("YOU DIDN'T GIVE ME A FILE!");
                return;
            }

            string file = args[0];
            var fileInfo = new FileInfo(file);

            if (!fileInfo.Exists)
            {
                Console.WriteLine("File does not exist.");
                return;
            }
            string dirNameToUse = Path.GetFileNameWithoutExtension(file);
            if (!Directory.Exists(dirNameToUse))
            {
                // Outputs to current location + dirNameToUse.
                Directory.CreateDirectory(dirNameToUse);
            }

            UnpackBundle(file, dirNameToUse);
        }

        private static void UnpackBundle(string file, string dirNameToUse)
        {
            // Doesn't support compressed (-z) files, easy to add, just didn't see a need right now.

            using (FileStream fs = File.OpenRead(file))
            {
                // Go backwards.
                fs.Seek(-16, SeekOrigin.End);
                using (BinaryReader br = new BinaryReader(fs))
                {
                    string pattern = Encoding.UTF8.GetString(br.ReadBytes(16));
                    if (pattern != WELL_KNOWN_MKBUNDLE_PATTERN)
                    {
                        Console.WriteLine("Pattern not recognised", pattern);
                        return;
                    }
                    // Seek back long to get index start.
                    br.BaseStream.Seek(-24, SeekOrigin.End);
                    long indexStart = br.ReadInt64();

                    br.BaseStream.Seek(indexStart, SeekOrigin.Begin);

                    int locationCount = br.ReadInt32();

                    // Key = entry name.
                    // Key is bytes length + 1 then the bytes then a 0 byte then Long, Int from tuple.
                    // Value = tuple below.
                    // Tuple<long, int> - Long = package position (aligned to 4096 on Linux), int = size of the file.
                    for (int i = 0; i < locationCount; i++)
                    {

                        int keyByteLength = br.ReadInt32(); // This is +1
                        if (keyByteLength == 0)
                        {
                            continue;
                        }
                        byte[] entryKeyBytes = br.ReadBytes(keyByteLength - 1);
                        string entryKey = Encoding.UTF8.GetString(entryKeyBytes);

                        // Discard
                        br.ReadByte();

                        long packagePosition = br.ReadInt64();
                        int fileSize = br.ReadInt32();

                        // Restore this after reading bytes.
                        long currentIndexPosition = br.BaseStream.Position;

                        br.BaseStream.Seek(packagePosition, SeekOrigin.Begin);

                        byte[] fileBytes = br.ReadBytes(fileSize);

                        // Dump if assembly.
                        if (entryKey.StartsWith("assembly") || entryKey.StartsWith("library"))
                        {
                            string fileName = entryKey.Split(new[] { ':' }, 2)[1];
                            File.WriteAllBytes(dirNameToUse + "\\" + fileName, fileBytes);
                        }
                        else
                        {
                            string fileName = entryKey.Replace(':', '-');
                            File.WriteAllBytes(dirNameToUse + "\\" + fileName, fileBytes);
                        }

                        // Reset.
                        br.BaseStream.Seek(currentIndexPosition, SeekOrigin.Begin);
                    }

                }
            }
        }
    }
}