using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrowserBlocker
{
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void RegisterCurrentUserStartup()
        {
            try
            {
                using (RegistryKey runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (runKey == null)
                    {
                        return;
                    }

                    runKey.SetValue(AppPaths.AppName, Quote(Application.ExecutablePath));
                    if (runKey.GetValue(AppPaths.LegacyAppName) != null)
                    {
                        runKey.DeleteValue(AppPaths.LegacyAppName, false);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
