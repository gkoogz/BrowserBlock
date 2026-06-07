using System;
using System.Threading;

namespace BrowserBlocker
{
    public static class WatchdogHost
    {
        private const string MutexName = "Local\\BrowserBlockerWatchdog";

        public static void Run()
        {
            bool ownsMutex;
            using (Mutex mutex = new Mutex(true, MutexName, out ownsMutex))
            {
                if (!ownsMutex)
                {
                    return;
                }

                using (BrowserBlockService service =
                    new BrowserBlockService(BlockStateStore.CreateDefault()))
                {
                    if (!service.IsBlocked)
                    {
                        DurableEnforcement.RemoveTask();
                        return;
                    }

                    service.Start();
                    while (service.IsBlocked)
                    {
                        Thread.Sleep(500);
                    }
                }

                DurableEnforcement.RemoveTask();
            }
        }
    }
}

