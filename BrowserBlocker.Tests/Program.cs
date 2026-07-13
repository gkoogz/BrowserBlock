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
            Rectangle expectedBounds = new Rectangle(25, 30, 380, 220);
            settingsStore.SaveBounds(expectedBounds);
            Rectangle? loadedBounds = settingsStore.LoadBounds(new Size(380, 220));
            Assert(loadedBounds == expectedBounds, "Window bounds round-trip");
            Directory.Delete(settingsDirectory, true);

            string deployDirectory = Path.Combine(
                Path.GetTempPath(),
                "BrowserBlocker.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(deployDirectory);
            string sourceExecutable = Path.Combine(deployDirectory, "source.exe");
            string watchdogExecutable = Path.Combine(deployDirectory, "watchdog.exe");
            File.WriteAllText(sourceExecutable, "new watchdog");
            File.WriteAllText(watchdogExecutable, "running watchdog");
            using (FileStream lockedWatchdog = new FileStream(
                watchdogExecutable,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                DurableEnforcement.DeployWatchdogExecutable(
                    sourceExecutable,
                    watchdogExecutable);
                Assert(
                    File.ReadAllText(watchdogExecutable) == "running watchdog",
                    "Running watchdog executable is reused when locked");
            }

            DurableEnforcement.DeployWatchdogExecutable(
                sourceExecutable,
                watchdogExecutable);
            Assert(
                File.ReadAllText(watchdogExecutable) == "new watchdog",
                "Stopped watchdog executable is updated");
            Directory.Delete(deployDirectory, true);

            string renewalDirectory = Path.Combine(
                Path.GetTempPath(),
                "BrowserBlocker.Tests",
                Guid.NewGuid().ToString("N"));
            string renewalStatePath = Path.Combine(renewalDirectory, "block-until.txt");
            BlockStateStore renewalStore = new BlockStateStore(renewalStatePath);
            renewalStore.SaveBlockUntilUtc(DateTime.UtcNow.AddMilliseconds(100));
            using (BrowserBlockService renewalService = new BrowserBlockService(renewalStore))
            {
                renewalStore.SaveBlockUntilUtc(DateTime.UtcNow.AddMinutes(5));
                System.Threading.Thread.Sleep(150);
                Assert(
                    renewalService.IsBlocked,
                    "Running watchdog adopts a renewed persisted deadline");
            }
            renewalStore.Clear();
            Directory.Delete(renewalDirectory, true);

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
