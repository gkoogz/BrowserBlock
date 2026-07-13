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
        private const string TaskNamePrefix = "BrowserBlockWatchdog";
        private const string LegacyTaskName = "BrowserBlockerWatchdog";

        public static void Install()
        {
            string watchdogPath = GetWatchdogPath();
            string directory = Path.GetDirectoryName(watchdogPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PrepareWatchdogExecutable(watchdogPath);

            string taskDefinitionPath = Path.Combine(
                Path.GetTempPath(),
                AppPaths.AppName + "-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(
                    taskDefinitionPath,
                    CreateTaskDefinition(watchdogPath),
                    Encoding.Unicode);
                TryRunTaskScheduler(
                    "/Create /TN " + Quote(GetCurrentUserTaskName()) +
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
            RemoveTask(GetCurrentUserTaskName());
            RemoveTask(TaskNamePrefix);
            RemoveTask(LegacyTaskName);
        }

        private static void RemoveTask(string taskName)
        {
            TryRunTaskScheduler("/Delete /TN " + Quote(taskName) + " /F");
        }

        public static void DeployWatchdogExecutable(string sourcePath, string watchdogPath)
        {
            try
            {
                File.Copy(sourcePath, watchdogPath, true);
            }
            catch (IOException)
            {
                FileInfo existingWatchdog = new FileInfo(watchdogPath);
                if (!existingWatchdog.Exists || existingWatchdog.Length == 0)
                {
                    throw;
                }

                // Windows locks a running executable. The existing watchdog is
                // already enforcing the same persisted deadline and can be reused.
            }
        }

        private static string GetWatchdogPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppPaths.AppName,
                "BrowserBlockWatchdog.exe");
        }

        private static void PrepareWatchdogExecutable(string watchdogPath)
        {
            DeployWatchdogExecutable(Assembly.GetExecutingAssembly().Location, watchdogPath);
        }

        private static string CreateTaskDefinition(string watchdogPath)
        {
            string userSid = WindowsIdentity.GetCurrent().User.Value;
            string startBoundary = DateTime.Now.AddSeconds(15).ToString("s");
            return
                "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                "<RegistrationInfo><Author>" + AppPaths.AppName + "</Author></RegistrationInfo>" +
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

        private static string GetCurrentUserTaskName()
        {
            string userSid = WindowsIdentity.GetCurrent().User.Value;
            return TaskNamePrefix + "-" + userSid;
        }

        private static bool TryRunTaskScheduler(string arguments)
        {
            try
            {
                RunTaskScheduler(arguments);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
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
