using System;
using System.IO;

namespace BrowserBlocker
{
    internal static class AppPaths
    {
        public const string AppName = "BrowserBlock";
        public const string LegacyAppName = "BrowserBlocker";

        public static string LocalAppDataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppName);
            }
        }

        public static string LegacyLocalAppDataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    LegacyAppName);
            }
        }
    }
}
