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
        private readonly string legacySettingsFilePath;

        public WindowSettingsStore(string settingsFilePath)
            : this(settingsFilePath, null)
        {
        }

        public WindowSettingsStore(string settingsFilePath, string legacySettingsFilePath)
        {
            if (string.IsNullOrWhiteSpace(settingsFilePath))
            {
                throw new ArgumentException("A settings file path is required.", "settingsFilePath");
            }

            this.settingsFilePath = settingsFilePath;
            this.legacySettingsFilePath = legacySettingsFilePath;
        }

        public static WindowSettingsStore CreateDefault()
        {
            return new WindowSettingsStore(
                Path.Combine(AppPaths.LocalAppDataDirectory, "window-position.txt"),
                Path.Combine(AppPaths.LegacyLocalAppDataDirectory, "window-position.txt"));
        }

        public Rectangle? LoadBounds(Size defaultSize)
        {
            Rectangle? bounds = LoadBounds(settingsFilePath, defaultSize);
            if (bounds.HasValue)
            {
                return bounds;
            }

            Rectangle? legacyBounds = LoadBounds(legacySettingsFilePath, defaultSize);
            if (legacyBounds.HasValue)
            {
                SaveBounds(legacyBounds.Value);
            }

            return legacyBounds;
        }

        private static Rectangle? LoadBounds(string path, Size defaultSize)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                string[] parts = File.ReadAllText(path).Trim().Split(',');
                if (parts.Length != 2 && parts.Length != 4)
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

                int width = defaultSize.Width;
                int height = defaultSize.Height;
                if (parts.Length == 4 &&
                    (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out width) ||
                     !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out height)))
                {
                    return null;
                }

                width = Math.Max(defaultSize.Width, width);
                height = Math.Max(defaultSize.Height, height);
                return EnsureVisible(new Rectangle(x, y, width, height));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        public void SaveBounds(Rectangle bounds)
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
                        "{0},{1},{2},{3}",
                        bounds.X,
                        bounds.Y,
                        bounds.Width,
                        bounds.Height));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static Rectangle? EnsureVisible(Rectangle bounds)
        {
            Screen nearestScreen = null;
            int nearestDistance = int.MaxValue;
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(bounds))
                {
                    return ClampToWorkingArea(bounds, screen.WorkingArea);
                }

                int distance =
                    Math.Abs(screen.WorkingArea.Left - bounds.Left) +
                    Math.Abs(screen.WorkingArea.Top - bounds.Top);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestScreen = screen;
                }
            }

            return nearestScreen == null
                ? (Rectangle?)null
                : ClampToWorkingArea(bounds, nearestScreen.WorkingArea);
        }

        private static Rectangle ClampToWorkingArea(Rectangle bounds, Rectangle workingArea)
        {
            int width = Math.Min(bounds.Width, workingArea.Width);
            int height = Math.Min(bounds.Height, workingArea.Height);
            int x = Math.Min(Math.Max(bounds.X, workingArea.Left), workingArea.Right - width);
            int y = Math.Min(Math.Max(bounds.Y, workingArea.Top), workingArea.Bottom - height);
            return new Rectangle(x, y, width, height);
        }
    }
}
