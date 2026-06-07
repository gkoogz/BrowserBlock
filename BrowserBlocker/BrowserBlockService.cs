using System;
using System.Diagnostics;
using System.Threading;

namespace BrowserBlocker
{
    public sealed class BrowserBlockService : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly BlockStateStore stateStore;
        private Timer enforcementTimer;
        private DateTime? blockUntilUtc;
        private int enforcementInProgress;

        public BrowserBlockService(BlockStateStore stateStore)
        {
            if (stateStore == null)
            {
                throw new ArgumentNullException("stateStore");
            }

            this.stateStore = stateStore;
            blockUntilUtc = stateStore.LoadBlockUntilUtc();
            RefreshState();
        }

        public bool IsBlocked
        {
            get
            {
                RefreshState();
                lock (syncRoot)
                {
                    return blockUntilUtc.HasValue;
                }
            }
        }

        public TimeSpan Remaining
        {
            get
            {
                RefreshState();
                lock (syncRoot)
                {
                    return blockUntilUtc.HasValue
                        ? blockUntilUtc.Value - DateTime.UtcNow
                        : TimeSpan.Zero;
                }
            }
        }

        public void Start()
        {
            lock (syncRoot)
            {
                if (enforcementTimer == null)
                {
                    enforcementTimer = new Timer(Enforce, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(300));
                }
            }
        }

        public void BeginBlock(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("duration");
            }

            DateTime deadline = DateTime.UtcNow.Add(duration);
            stateStore.SaveBlockUntilUtc(deadline);

            lock (syncRoot)
            {
                blockUntilUtc = deadline;
            }

            try
            {
                DurableEnforcement.Install();
                Enforce(null);
            }
            catch
            {
                lock (syncRoot)
                {
                    blockUntilUtc = null;
                }

                stateStore.Clear();
                throw;
            }
        }

        private void RefreshState()
        {
            bool expired = false;
            lock (syncRoot)
            {
                if (blockUntilUtc.HasValue && blockUntilUtc.Value <= DateTime.UtcNow)
                {
                    blockUntilUtc = null;
                    expired = true;
                }
            }

            if (expired)
            {
                stateStore.Clear();
            }
        }

        private void Enforce(object state)
        {
            if (Interlocked.Exchange(ref enforcementInProgress, 1) != 0)
            {
                return;
            }

            try
            {
                if (!IsBlocked)
                {
                    return;
                }

                foreach (Process process in Process.GetProcesses())
                {
                    using (process)
                    {
                        try
                        {
                            if (BrowserCatalog.IsBrowserProcess(process.ProcessName))
                            {
                                process.Kill();
                            }
                        }
                        catch (InvalidOperationException)
                        {
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                        }
                        catch (NotSupportedException)
                        {
                        }
                    }
                }
            }
            finally
            {
                Volatile.Write(ref enforcementInProgress, 0);
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (enforcementTimer != null)
                {
                    enforcementTimer.Dispose();
                    enforcementTimer = null;
                }
            }
        }
    }
}
