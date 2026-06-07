using System;
using System.Globalization;
using System.IO;

namespace BrowserBlocker
{
    public sealed class BlockStateStore
    {
        private readonly string stateFilePath;

        public BlockStateStore(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
            {
                throw new ArgumentException("A state file path is required.", "stateFilePath");
            }

            this.stateFilePath = stateFilePath;
        }

        public static BlockStateStore CreateDefault()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BrowserBlocker");
            return new BlockStateStore(Path.Combine(directory, "block-until.txt"));
        }

        public DateTime? LoadBlockUntilUtc()
        {
            try
            {
                if (!File.Exists(stateFilePath))
                {
                    return null;
                }

                DateTime parsed;
                if (DateTime.TryParseExact(
                    File.ReadAllText(stateFilePath).Trim(),
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out parsed))
                {
                    return parsed.ToUniversalTime();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        public void SaveBlockUntilUtc(DateTime blockUntilUtc)
        {
            string directory = Path.GetDirectoryName(stateFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                stateFilePath,
                blockUntilUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }

        public void Clear()
        {
            try
            {
                if (File.Exists(stateFilePath))
                {
                    File.Delete(stateFilePath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

