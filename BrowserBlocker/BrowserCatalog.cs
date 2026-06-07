using System;
using System.Collections.Generic;

namespace BrowserBlocker
{
    public static class BrowserCatalog
    {
        private static readonly HashSet<string> ProcessNames = new HashSet<string>(
            new[]
            {
                "chrome",
                "chromium",
                "brave",
                "msedge",
                "firefox",
                "opera",
                "opera_gx_splash",
                "vivaldi",
                "iexplore",
                "arc",
                "zen",
                "librewolf",
                "waterfox",
                "floorp",
                "palemoon",
                "basilisk",
                "thorium",
                "duckduckgo",
                "browser",
                "maxthon",
                "seamonkey",
                "sidekick",
                "wavebox",
                "yandex",
                "slimjet",
                "avastbrowser",
                "avg_browser",
                "epic",
                "centbrowser",
                "coccoc",
                "iridium",
                "sputnik",
                "whale",
                "qutebrowser",
                "falkon",
                "otter-browser",
                "midori",
                "mullvadbrowser"
            },
            StringComparer.OrdinalIgnoreCase);

        public static bool IsBrowserProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return false;
            }

            string name = processName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            return ProcessNames.Contains(name);
        }
    }
}

