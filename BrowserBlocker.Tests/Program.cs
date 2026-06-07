using System;
using System.IO;

namespace BrowserBlocker.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Assert(BrowserCatalog.IsBrowserProcess("chrome"), "Chrome is recognized");
            Assert(BrowserCatalog.IsBrowserProcess("chrome.exe"), "Executable extension is accepted");
            Assert(BrowserCatalog.IsBrowserProcess("BRAVE"), "Matching is case-insensitive");
            Assert(BrowserCatalog.IsBrowserProcess("chromium"), "Chromium is recognized");
            Assert(BrowserCatalog.IsBrowserProcess("librewolf"), "LibreWolf is recognized");
            Assert(!BrowserCatalog.IsBrowserProcess("msedgewebview2"), "WebView2 is not blocked");
            Assert(!BrowserCatalog.IsBrowserProcess("explorer"), "Windows Explorer is not blocked");

            string directory = Path.Combine(Path.GetTempPath(), "BrowserBlocker.Tests", Guid.NewGuid().ToString("N"));
            string statePath = Path.Combine(directory, "state.txt");
            BlockStateStore store = new BlockStateStore(statePath);
            DateTime deadline = DateTime.UtcNow.AddMinutes(42);
            store.SaveBlockUntilUtc(deadline);
            DateTime? loaded = store.LoadBlockUntilUtc();
            Assert(loaded.HasValue, "Saved deadline can be loaded");
            Assert(
                loaded.HasValue && Math.Abs((loaded.Value - deadline).TotalMilliseconds) < 1,
                "Deadline round-trips exactly");
            store.Clear();
            Assert(!File.Exists(statePath), "Clearing removes persisted state");
            Directory.Delete(directory, true);

            Console.WriteLine(failures == 0 ? "All tests passed." : failures + " test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void Assert(bool condition, string name)
        {
            if (condition)
            {
                Console.WriteLine("PASS: " + name);
                return;
            }

            failures++;
            Console.WriteLine("FAIL: " + name);
        }
    }
}

