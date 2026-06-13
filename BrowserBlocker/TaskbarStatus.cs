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
        private const int WmSetIcon = 0x0080;
        private const int IconSmall = 0;
        private const int IconBig = 1;
        private const int IconSmall2 = 2;
        private const int GclpHIcon = -14;
        private const int GclpHIconSmall = -34;
        private const int SmCxIcon = 11;
        private const int SmCyIcon = 12;
        private const int SmCxSmallIcon = 49;
        private const int SmCySmallIcon = 50;
        private static readonly Guid TaskbarListClassId =
            new Guid("56FDF344-FD6D-11d0-958A-006097C9A090");

        private readonly Form form;
        private readonly Icon standardIcon;
        private readonly ITaskbarList3 taskbarList;

        private IconPair countdownIcon;
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
                IconPair nextIcon = CreateCountdownIconPair(minute);
                IconPair previousIcon = countdownIcon;
                countdownIcon = nextIcon;
                if (previousIcon != null)
                {
                    previousIcon.Dispose();
                }

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
                    taskbarList.SetOverlayIcon(form.Handle, IntPtr.Zero, null);
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
                if (countdownIcon != null)
                {
                    countdownIcon.Dispose();
                }

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
            if (countdownIcon != null)
            {
                countdownIcon.Dispose();
            }

            standardIcon?.Dispose();
            if (taskbarList != null)
            {
                Marshal.ReleaseComObject(taskbarList);
            }
        }

        private static IconPair CreateCountdownIconPair(int minute)
        {
            int smallWidth = Math.Max(16, GetSystemMetrics(SmCxSmallIcon));
            int smallHeight = Math.Max(16, GetSystemMetrics(SmCySmallIcon));
            int bigWidth = Math.Max(32, GetSystemMetrics(SmCxIcon));
            int bigHeight = Math.Max(32, GetSystemMetrics(SmCyIcon));

            return new IconPair(
                CreateCountdownIcon(minute, smallWidth, smallHeight),
                CreateCountdownIcon(minute, bigWidth, bigHeight));
        }

        private static Icon CreateCountdownIcon(int minute, int width, int height)
        {
            using (Bitmap bitmap = new Bitmap(width, height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                string text = Math.Min(99, minute).ToString();
                using (GraphicsPath path = CreateCountdownTextPath(text))
                using (Brush brush = new SolidBrush(Color.FromArgb(224, 67, 67)))
                {
                    RectangleF textBounds = path.GetBounds();
                    float margin = Math.Max(1F, Math.Min(width, height) * 0.03F);
                    float scale = Math.Min(
                        (width - (margin * 2F)) / textBounds.Width,
                        (height - (margin * 2F)) / textBounds.Height);
                    using (Matrix transform = new Matrix())
                    {
                        transform.Translate(
                            (width - (textBounds.Width * scale)) / 2F - (textBounds.Left * scale),
                            (height - (textBounds.Height * scale)) / 2F - (textBounds.Top * scale));
                        transform.Scale(scale, scale);
                        path.Transform(transform);
                    }

                    graphics.FillPath(brush, path);
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

        private static GraphicsPath CreateCountdownTextPath(string text)
        {
            FontFamily fontFamily;
            try
            {
                fontFamily = new FontFamily("Patua One");
            }
            catch (ArgumentException)
            {
                fontFamily = new FontFamily("Arial");
            }

            GraphicsPath path = new GraphicsPath();
            using (fontFamily)
            using (StringFormat format = StringFormat.GenericTypographic)
            {
                path.AddString(text, fontFamily, (int)FontStyle.Regular, 100F, Point.Empty, format);
            }

            return path;
        }

        private void ApplyWindowIcon(IconPair icon)
        {
            if (icon == null)
            {
                return;
            }

            ApplyWindowIcons(icon.Small, icon.Big);
        }

        private void ApplyWindowIcon(Icon icon)
        {
            if (icon == null)
            {
                return;
            }

            ApplyWindowIcons(icon, icon);
        }

        private void ApplyWindowIcons(Icon smallIcon, Icon bigIcon)
        {
            if (smallIcon == null || bigIcon == null)
            {
                return;
            }

            form.Icon = bigIcon;
            if (!form.IsHandleCreated)
            {
                return;
            }

            form.Icon = bigIcon;
            SendMessage(form.Handle, WmSetIcon, new IntPtr(IconSmall), smallIcon.Handle);
            SendMessage(form.Handle, WmSetIcon, new IntPtr(IconBig), bigIcon.Handle);
            SendMessage(form.Handle, WmSetIcon, new IntPtr(IconSmall2), smallIcon.Handle);
            SetClassIcon(form.Handle, GclpHIcon, bigIcon.Handle);
            SetClassIcon(form.Handle, GclpHIconSmall, smallIcon.Handle);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr iconHandle);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr windowHandle,
            int message,
            IntPtr parameter,
            IntPtr value);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        private static IntPtr SetClassIcon(IntPtr windowHandle, int index, IntPtr newLong)
        {
            return IntPtr.Size == 8
                ? SetClassLongPtr64(windowHandle, index, newLong)
                : new IntPtr(SetClassLongPtr32(windowHandle, index, newLong.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "SetClassLong")]
        private static extern int SetClassLongPtr32(
            IntPtr windowHandle,
            int index,
            int newLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
        private static extern IntPtr SetClassLongPtr64(
            IntPtr windowHandle,
            int index,
            IntPtr newLong);

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

        private sealed class IconPair : IDisposable
        {
            public IconPair(Icon small, Icon big)
            {
                Small = small;
                Big = big;
            }

            public Icon Small { get; private set; }

            public Icon Big { get; private set; }

            public void Dispose()
            {
                Small?.Dispose();
                Big?.Dispose();
                Small = null;
                Big = null;
            }
        }
    }
}
