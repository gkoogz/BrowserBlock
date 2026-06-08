using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace BrowserBlocker
{
    public sealed class WindowSettingsStore
    {
        private readonly string settingsFilePath;

        public WindowSettingsStore(string settingsFilePath)
        {
            if (string.IsNullOrWhiteSpace(settingsFilePath))
            {
                throw new ArgumentException("A settings file path is required.", "settingsFilePath");
            }

            this.settingsFilePath = settingsFilePath;
        }

        public static WindowSettingsStore CreateDefault()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BrowserBlocker");
            return new WindowSettingsStore(Path.Combine(directory, "window-position.txt"));
        }

        public Point? LoadLocation(Size windowSize)
        {
            try
            {
                if (!File.Exists(settingsFilePath))
                {
                    return null;
                }

                string[] parts = File.ReadAllText(settingsFilePath).Trim().Split(',');
                if (parts.Length != 2)
                {
                    return null;
                }

                int x;
                int y;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                {
                    return null;
                }

                Point location = new Point(x, y);
                Rectangle windowBounds = new Rectangle(location, windowSize);
                foreach (Screen screen in Screen.AllScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(windowBounds))
                    {
                        return location;
                    }
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

        public void SaveLocation(Point location)
        {
            try
            {
                string directory = Path.GetDirectoryName(settingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    settingsFilePath,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1}",
                        location.X,
                        location.Y));
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
