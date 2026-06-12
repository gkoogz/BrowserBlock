using System;
using System.Linq;
using System.Windows.Forms;

namespace BrowserBlocker
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Any(argument =>
                string.Equals(argument, "--watchdog", StringComparison.OrdinalIgnoreCase)))
            {
                WatchdogHost.Run();
                return;
            }

            StartupManager.RegisterCurrentUserStartup();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
