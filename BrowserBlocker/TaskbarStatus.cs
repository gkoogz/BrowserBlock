using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BrowserBlocker
{
    internal sealed class TaskbarStatus : IDisposable
    {
        private const int IconSize = 256;
        private const int WmSetIcon = 0x0080;
        private const int IconSmall = 0;
        private const int IconBig = 1;
        private const int IconSmall2 = 2;
        private static readonly Guid TaskbarListClassId =
            new Guid("56FDF344-FD6D-11d0-958A-006097C9A090");

        private readonly Form form;
        private readonly Icon standardIcon;
        private readonly ITaskbarList3 taskbarList;

        private Icon countdownIcon;
        private int lastMinute = -1;
        private bool showingCountdown;
        private bool taskbarReady;

        public TaskbarStatus(Form form)
        {
            this.form = form;
            standardIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            try
            {
                taskbarList = (ITaskbarList3)Activator.CreateInstance(
                    Type.GetTypeFromCLSID(TaskbarListClassId));
                taskbarList.HrInit();
                taskbarReady = true;
            }
            catch (COMException)
            {
            }
            catch (InvalidCastException)
            {
            }
        }

        public void Update(bool blocked, TimeSpan remaining, TimeSpan duration)
        {
            if (!blocked)
            {
                Clear();
                return;
            }

            int minute = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            if (!showingCountdown || minute != lastMinute)
            {
                Icon nextIcon = CreateCountdownIcon(minute);
                Icon previousIcon = countdownIcon;
                countdownIcon = nextIcon;
                previousIcon?.Dispose();
                lastMinute = minute;
                showingCountdown = true;
            }

            ApplyWindowIcon(countdownIcon);

            if (taskbarReady && form.IsHandleCreated)
            {
                ulong total = Math.Max(1, (ulong)Math.Ceiling(duration.TotalSeconds));
                ulong value = Math.Min(total, (ulong)Math.Ceiling(Math.Max(0, remaining.TotalSeconds)));
                try
                {
                    taskbarList.SetProgressState(form.Handle, TaskbarProgressFlag.Error);
                    taskbarList.SetProgressValue(form.Handle, value, total);
                    taskbarList.SetOverlayIcon(
                        form.Handle,
                        countdownIcon == null ? IntPtr.Zero : countdownIcon.Handle,
                        minute.ToString());
                }
                catch (COMException)
                {
                    taskbarReady = false;
                }
            }
        }

        public void Clear()
        {
            if (showingCountdown)
            {
                ApplyWindowIcon(standardIcon);
                countdownIcon?.Dispose();
                countdownIcon = null;
                lastMinute = -1;
                showingCountdown = false;
            }

            if (taskbarReady && form.IsHandleCreated)
            {
                try
                {
                    taskbarList.SetOverlayIcon(form.Handle, IntPtr.Zero, null);
                    taskbarList.SetProgressState(form.Handle, TaskbarProgressFlag.NoProgress);
                }
                catch (COMException)
                {
                    taskbarReady = false;
                }
            }
        }

        public void Dispose()
        {
            Clear();
            countdownIcon?.Dispose();
            standardIcon?.Dispose();
            if (taskbarList != null)
            {
                Marshal.ReleaseComObject(taskbarList);
            }
        }

        private static Icon CreateCountdownIcon(int minute)
        {
            using (Bitmap bitmap = new Bitmap(IconSize, IconSize))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                string text = Math.Min(99, minute).ToString();
                using (Font font = GetCountdownFont(text))
                using (Brush brush = new SolidBrush(Color.FromArgb(224, 67, 67)))
                using (StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    RectangleF bounds = new RectangleF(0, -6, IconSize, IconSize);
                    graphics.DrawString(text, font, brush, bounds, format);
                }

                IntPtr iconHandle = bitmap.GetHicon();
                try
                {
                    using (Icon icon = Icon.FromHandle(iconHandle))
                    {
                        return (Icon)icon.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(iconHandle);
                }
            }
        }

        private void ApplyWindowIcon(Icon icon)
        {
            if (icon == null)
            {
                return;
            }

            form.Icon = icon;
            if (!form.IsHandleCreated)
            {
                return;
            }

            SendMessage(form.Handle, WmSetIcon, new IntPtr(IconSmall), icon.Handle);
            SendMessage(form.Handle, WmSetIcon, new IntPtr(IconBig), icon.Handle);
            SendMessage(form.Handle, WmSetIcon, new IntPtr(IconSmall2), icon.Handle);
        }

        private static Font GetCountdownFont(string text)
        {
            float size = text.Length > 1 ? 168F : 188F;
            try
            {
                return new Font("Patua One", size, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch (ArgumentException)
            {
                return new Font("Arial", size, FontStyle.Bold, GraphicsUnit.Pixel);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr iconHandle);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr windowHandle,
            int message,
            IntPtr parameter,
            IntPtr value);

        [ComImport]
        [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
            void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
            void SetProgressState(IntPtr hwnd, TaskbarProgressFlag flags);
            void RegisterTab(IntPtr tabHandle, IntPtr mainWindowHandle);
            void UnregisterTab(IntPtr tabHandle);
            void SetTabOrder(IntPtr tabHandle, IntPtr insertBeforeHandle);
            void SetTabActive(IntPtr tabHandle, IntPtr mainWindowHandle, uint reserved);
            void ThumbBarAddButtons(IntPtr hwnd, uint buttonCount, IntPtr buttons);
            void ThumbBarUpdateButtons(IntPtr hwnd, uint buttonCount, IntPtr buttons);
            void ThumbBarSetImageList(IntPtr hwnd, IntPtr imageList);
            void SetOverlayIcon(
                IntPtr hwnd,
                IntPtr iconHandle,
                [MarshalAs(UnmanagedType.LPWStr)] string description);
        }

        private enum TaskbarProgressFlag
        {
            NoProgress = 0,
            Indeterminate = 1,
            Normal = 2,
            Error = 4,
            Paused = 8
        }
    }
}
