using System;
using System.Globalization;
using System.IO;

namespace BrowserBlocker
{
    public sealed class BlockStateStore
    {
        private readonly string stateFilePath;
        private readonly string legacyStateFilePath;

        public BlockStateStore(string stateFilePath)
            : this(stateFilePath, null)
        {
        }

        public BlockStateStore(string stateFilePath, string legacyStateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
            {
                throw new ArgumentException("A state file path is required.", "stateFilePath");
            }

            this.stateFilePath = stateFilePath;
            this.legacyStateFilePath = legacyStateFilePath;
        }

        public static BlockStateStore CreateDefault()
        {
            return new BlockStateStore(
                Path.Combine(AppPaths.LocalAppDataDirectory, "block-until.txt"),
                Path.Combine(AppPaths.LegacyLocalAppDataDirectory, "block-until.txt"));
        }

        public DateTime? LoadBlockUntilUtc()
        {
            DateTime? current = LoadBlockUntilUtc(stateFilePath);
            if (current.HasValue)
            {
                return current;
            }

            DateTime? legacy = LoadBlockUntilUtc(legacyStateFilePath);
            if (legacy.HasValue)
            {
                SaveBlockUntilUtc(legacy.Value);
            }

            return legacy;
        }

        private static DateTime? LoadBlockUntilUtc(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                DateTime parsed;
                if (DateTime.TryParseExact(
                    File.ReadAllText(path).Trim(),
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

            try
            {
                if (!string.IsNullOrWhiteSpace(legacyStateFilePath) && File.Exists(legacyStateFilePath))
                {
                    File.Delete(legacyStateFilePath);
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

