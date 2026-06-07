using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace BrowserBlocker
{
    public static class DurableEnforcement
    {
        private const string TaskName = "BrowserBlockerWatchdog";

        public static void Install()
        {
            string watchdogPath = GetWatchdogPath();
            string directory = Path.GetDirectoryName(watchdogPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(Assembly.GetExecutingAssembly().Location, watchdogPath, true);

            string taskDefinitionPath = Path.Combine(
                Path.GetTempPath(),
                "BrowserBlocker-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(
                    taskDefinitionPath,
                    CreateTaskDefinition(watchdogPath),
                    Encoding.Unicode);
                RunTaskScheduler(
                    "/Create /TN " + Quote(TaskName) +
                    " /XML " + Quote(taskDefinitionPath) +
                    " /F");
            }
            finally
            {
                try
                {
                    File.Delete(taskDefinitionPath);
                }
                catch (IOException)
                {
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = watchdogPath,
                Arguments = "--watchdog",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        public static void RemoveTask()
        {
            try
            {
                RunTaskScheduler("/Delete /TN " + Quote(TaskName) + " /F");
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static string GetWatchdogPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BrowserBlocker",
                "BrowserBlockerWatchdog.exe");
        }

        private static string CreateTaskDefinition(string watchdogPath)
        {
            string userSid = WindowsIdentity.GetCurrent().User.Value;
            string startBoundary = DateTime.Now.AddSeconds(15).ToString("s");
            return
                "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                "<RegistrationInfo><Author>BrowserBlocker</Author></RegistrationInfo>" +
                "<Triggers><TimeTrigger><Repetition><Interval>PT1M</Interval>" +
                "<StopAtDurationEnd>false</StopAtDurationEnd></Repetition>" +
                "<StartBoundary>" + startBoundary + "</StartBoundary><Enabled>true</Enabled>" +
                "</TimeTrigger></Triggers>" +
                "<Principals><Principal id=\"Author\"><UserId>" +
                SecurityElement.Escape(userSid) +
                "</UserId><LogonType>InteractiveToken</LogonType>" +
                "<RunLevel>LeastPrivilege</RunLevel></Principal></Principals>" +
                "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" +
                "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
                "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
                "<AllowHardTerminate>true</AllowHardTerminate>" +
                "<StartWhenAvailable>true</StartWhenAvailable>" +
                "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>" +
                "<IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd>" +
                "<RestartOnIdle>false</RestartOnIdle></IdleSettings>" +
                "<AllowStartOnDemand>true</AllowStartOnDemand>" +
                "<Enabled>true</Enabled><Hidden>true</Hidden>" +
                "<RunOnlyIfIdle>false</RunOnlyIfIdle>" +
                "<WakeToRun>false</WakeToRun><ExecutionTimeLimit>PT2H</ExecutionTimeLimit>" +
                "<Priority>7</Priority></Settings>" +
                "<Actions Context=\"Author\"><Exec><Command>" +
                SecurityElement.Escape(watchdogPath) +
                "</Command><Arguments>--watchdog</Arguments></Exec></Actions></Task>";
        }

        private static void RunTaskScheduler(string arguments)
        {
            using (Process process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "schtasks.exe"),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }))
            {
                string standardError = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "Windows Task Scheduler could not enable durable blocking. " +
                        standardError.Trim());
                }
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
