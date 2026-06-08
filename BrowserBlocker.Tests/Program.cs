using System;
using System.Drawing;
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

            DateTime tenOClock = new DateTime(2026, 6, 7, 10, 0, 0, DateTimeKind.Local);
            Assert(
                HourlyPromptSchedule.ShouldShow(tenOClock, DateTime.MinValue, false),
                "Hourly prompt appears on the hour");
            Assert(
                HourlyPromptSchedule.ShouldShow(tenOClock.AddSeconds(45), DateTime.MinValue, false),
                "Hourly prompt can appear after a chained block expires");
            Assert(
                !HourlyPromptSchedule.ShouldShow(tenOClock.AddMinutes(1), DateTime.MinValue, false),
                "Hourly prompt does not appear after the first minute");
            Assert(
                !HourlyPromptSchedule.ShouldShow(
                    tenOClock,
                    HourlyPromptSchedule.GetHourKey(tenOClock),
                    false),
                "Hourly prompt appears only once per hour");
            Assert(
                !HourlyPromptSchedule.ShouldShow(tenOClock, DateTime.MinValue, true),
                "Hourly prompt is skipped while blocked");
            Assert(
                HourlyPromptSchedule.ShouldShowBlockExpiration(
                    TimeSpan.FromSeconds(59),
                    true,
                    false),
                "Expiration prompt appears at 59 seconds");
            Assert(
                !HourlyPromptSchedule.ShouldShowBlockExpiration(
                    TimeSpan.FromSeconds(60),
                    true,
                    false),
                "Expiration prompt does not appear early");
            Assert(
                !HourlyPromptSchedule.ShouldShowBlockExpiration(
                    TimeSpan.FromSeconds(30),
                    true,
                    true),
                "Expiration prompt appears only once per block");
            Assert(
                !HourlyPromptSchedule.ShouldShowBlockExpiration(
                    TimeSpan.Zero,
                    false,
                    false),
                "Expiration prompt does not appear after expiry");

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

            string settingsDirectory = Path.Combine(
                Path.GetTempPath(),
                "BrowserBlocker.Tests",
                Guid.NewGuid().ToString("N"));
            string settingsPath = Path.Combine(settingsDirectory, "window-position.txt");
            WindowSettingsStore settingsStore = new WindowSettingsStore(settingsPath);
            Point expectedLocation = new Point(25, 30);
            settingsStore.SaveLocation(expectedLocation);
            Point? loadedLocation = settingsStore.LoadLocation(new Size(380, 220));
            Assert(loadedLocation == expectedLocation, "Window location round-trips");
            Directory.Delete(settingsDirectory, true);

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
