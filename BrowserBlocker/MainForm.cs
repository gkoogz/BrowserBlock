using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BrowserBlocker
{
    public sealed class MainForm : Form
    {
        private const int WmNclButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;
        private const uint FlashwStop = 0x00000000;
        private const int SwMinimize = 6;

        private static readonly Color DarkBackground = Color.FromArgb(24, 26, 31);
        private static readonly Color DarkText = Color.White;
        private static readonly Color DarkMutedText = Color.FromArgb(165, 170, 180);
        private static readonly Color LightBackground = Color.FromArgb(244, 245, 248);
        private static readonly Color LightText = Color.FromArgb(30, 33, 39);
        private static readonly Color LightMutedText = Color.FromArgb(104, 109, 119);
        private static readonly Color RedColor = Color.FromArgb(224, 67, 67);
        private static readonly Color GreenColor = Color.FromArgb(65, 196, 112);
        private static readonly Color DismissColor = Color.FromArgb(91, 96, 106);

        private readonly BrowserBlockService blockService;
        private readonly Timer displayTimer;
        private readonly Panel statusDot;
        private readonly Label titleLabel;
        private readonly Label statusLabel;
        private readonly Button blockButton;
        private readonly Button dismissButton;
        private readonly Label countdownLabel;
        private readonly Label hintLabel;
        private readonly Label promptQuestionLabel;
        private readonly Label promptCountdownLabel;
        private readonly WindowSettingsStore windowSettingsStore;
        private readonly IconButton pinButton;
        private readonly IconButton themeButton;
        private readonly IconButton minimizeButton;
        private readonly IconButton closeButton;
        private readonly TaskbarStatus taskbarStatus;

        private bool isPinned;
        private bool isDarkMode = true;
        private bool isHourlyPromptActive;
        private bool wasMinimizedBeforeHourlyPrompt;
        private bool wasTopMostBeforeHourlyPrompt;
        private bool expirationPromptShownForCurrentBlock;
        private bool wasBlockedOnPreviousTick;
        private bool promptRequestedAttention;
        private bool wasForegroundBeforeHourlyPrompt;
        private bool isSessionEnding;
        private Color statusDotColor = GreenColor;
        private DateTime hourlyPromptUntilUtc;
        private DateTime promptSuppressedUntilUtc = DateTime.MinValue;
        private DateTime lastPromptHour = DateTime.MinValue;
        private IntPtr previousForegroundWindow = IntPtr.Zero;

        public MainForm()
        {
            blockService = new BrowserBlockService(BlockStateStore.CreateDefault());
            windowSettingsStore = WindowSettingsStore.CreateDefault();
            taskbarStatus = new TaskbarStatus(this);

            Text = AppPaths.AppName;
            ClientSize = new Size(380, 220);
            BackColor = DarkBackground;
            ForeColor = DarkText;
            Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            FormClosing += MainFormClosing;
            Move += MainFormMove;
            ResizeEnd += MainFormResizeEnd;
            SystemEvents.SessionEnding += SystemEventsSessionEnding;

            pinButton = CreateIconButton(IconButtonKind.Pin, new Point(10, 9), "Pin widget");
            pinButton.Click += PinButtonClick;

            themeButton = CreateIconButton(IconButtonKind.Moon, new Point(43, 9), "Toggle light mode");
            themeButton.Click += ThemeButtonClick;

            minimizeButton = CreateIconButton(
                IconButtonKind.Minimize,
                new Point(ClientSize.Width - 70, 9),
                "Minimize");
            minimizeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };

            closeButton = CreateIconButton(
                IconButtonKind.Close,
                new Point(ClientSize.Width - 37, 9),
                "Close");
            closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeButton.HoverColor = Color.FromArgb(210, 202, 63, 73);
            closeButton.PressedColor = Color.FromArgb(240, 180, 45, 55);
            closeButton.Click += delegate { Close(); };

            titleLabel = new Label
            {
                Text = AppPaths.AppName,
                Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
                ForeColor = DarkText,
                AutoSize = true,
                Location = new Point(103, 12)
            };

            statusDot = new Panel
            {
                Size = new Size(12, 12),
                Location = new Point(27, 67)
            };
            statusDot.Paint += DrawStatusDot;

            statusLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                Location = new Point(46, 63)
            };

            blockButton = new Button
            {
                Text = "Block Browsers",
                FlatStyle = FlatStyle.Flat,
                BackColor = RedColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                Size = new Size(332, 58),
                Location = new Point(24, 102),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            blockButton.FlatAppearance.BorderSize = 0;
            blockButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(236, 78, 78);
            blockButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(194, 52, 52);
            blockButton.Click += BlockButtonClick;

            dismissButton = new Button
            {
                Text = "Dismiss",
                FlatStyle = FlatStyle.Flat,
                BackColor = DismissColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                Size = new Size(158, 50),
                Location = new Point(198, 111),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Visible = false
            };
            dismissButton.FlatAppearance.BorderSize = 0;
            dismissButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(108, 114, 125);
            dismissButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(73, 78, 87);
            dismissButton.Click += delegate { DismissHourlyPrompt(); };

            countdownLabel = new Label
            {
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DarkText,
                Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold),
                Size = blockButton.Size,
                Location = blockButton.Location,
                Visible = false
            };

            promptQuestionLabel = new Label
            {
                Text = "Would you like to block your browser for an hour?",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DarkText,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                Size = new Size(332, 42),
                Location = new Point(24, 56),
                Visible = false
            };

            promptCountdownLabel = new Label
            {
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DarkMutedText,
                Font = new Font("Segoe UI", 9F),
                Size = new Size(332, 24),
                Location = new Point(24, 174),
                Visible = false
            };

            hintLabel = new Label
            {
                Text = "Blocks browser launches for one hour",
                ForeColor = DarkMutedText,
                AutoSize = true,
                Location = new Point(24, 181)
            };

            Controls.Add(pinButton);
            Controls.Add(themeButton);
            Controls.Add(minimizeButton);
            Controls.Add(closeButton);
            Controls.Add(titleLabel);
            Controls.Add(statusDot);
            Controls.Add(statusLabel);
            Controls.Add(blockButton);
            Controls.Add(dismissButton);
            Controls.Add(countdownLabel);
            Controls.Add(promptQuestionLabel);
            Controls.Add(promptCountdownLabel);
            Controls.Add(hintLabel);

            AttachDragHandler(this);

            displayTimer = new Timer { Interval = 200 };
            displayTimer.Tick += DisplayTimerTick;
            displayTimer.Start();

            blockService.Start();
            RestoreWindowLocation();
            ApplyTheme();
            UpdateDisplay();
            UpdateHourlyPrompt();
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr parameter, IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr windowHandle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr windowHandle);

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FlashWindowInfo flashInfo);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr windowHandle, int commandShow);

        [StructLayout(LayoutKind.Sequential)]
        private struct FlashWindowInfo
        {
            public uint Size;
            public IntPtr WindowHandle;
            public uint Flags;
            public uint Count;
            public uint Timeout;
        }

        private static IconButton CreateIconButton(IconButtonKind kind, Point location, string tooltip)
        {
            IconButton button = new IconButton
            {
                IconKind = kind,
                Location = location,
                AccessibleName = tooltip
            };
            new ToolTip().SetToolTip(button, tooltip);
            return button;
        }

        private void AttachDragHandler(Control parent)
        {
            if (!(parent is IconButton) && !(parent is Button))
            {
                parent.MouseDown += DragMouseDown;
            }

            foreach (Control child in parent.Controls)
            {
                AttachDragHandler(child);
            }
        }

        private void DragMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || isPinned)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, WmNclButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
        }

        private void PinButtonClick(object sender, EventArgs e)
        {
            isPinned = !isPinned;
            pinButton.Highlighted = isPinned;
            pinButton.AccessibleName = isPinned ? "Unpin widget" : "Pin widget";
        }

        private void ThemeButtonClick(object sender, EventArgs e)
        {
            isDarkMode = !isDarkMode;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            Color background = isDarkMode ? DarkBackground : LightBackground;
            Color text = isDarkMode ? DarkText : LightText;
            Color mutedText = isDarkMode ? DarkMutedText : LightMutedText;
            Color hover = isDarkMode ? Color.FromArgb(38, Color.White) : Color.FromArgb(24, Color.Black);
            Color pressed = isDarkMode ? Color.FromArgb(64, Color.White) : Color.FromArgb(42, Color.Black);

            BackColor = background;
            ForeColor = text;
            titleLabel.ForeColor = text;
            countdownLabel.ForeColor = text;
            hintLabel.ForeColor = mutedText;
            promptQuestionLabel.ForeColor = text;
            promptCountdownLabel.ForeColor = mutedText;
            statusDot.BackColor = background;

            foreach (IconButton button in new[] { pinButton, themeButton, minimizeButton, closeButton })
            {
                button.IconColor = text;
                button.HoverColor = hover;
                button.PressedColor = pressed;
                button.Invalidate();
            }

            closeButton.HoverColor = Color.FromArgb(210, 202, 63, 73);
            closeButton.PressedColor = Color.FromArgb(240, 180, 45, 55);
            themeButton.IconKind = isDarkMode ? IconButtonKind.Moon : IconButtonKind.Sun;
            themeButton.AccessibleName = isDarkMode ? "Toggle light mode" : "Toggle dark mode";
            Invalidate(true);
        }

        private void BlockButtonClick(object sender, EventArgs e)
        {
            blockButton.Enabled = false;
            try
            {
                blockService.BeginBlock(TimeSpan.FromHours(1));
                expirationPromptShownForCurrentBlock = false;
                wasBlockedOnPreviousTick = true;
                DismissHourlyPrompt();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    AppPaths.AppName + " could not start the block.\r\n\r\n" + ex.Message,
                    AppPaths.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                blockButton.Enabled = true;
                UpdateDisplay();
            }
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                isSessionEnding = true;
            }

            SaveWindowBounds();
            if (blockService.IsBlocked && e.CloseReason != CloseReason.WindowsShutDown)
            {
                e.Cancel = true;
            }
        }

        private void SystemEventsSessionEnding(object sender, SessionEndingEventArgs e)
        {
            isSessionEnding = true;
        }

        private void MainFormMove(object sender, EventArgs e)
        {
            SaveWindowBounds();
        }

        private void MainFormResizeEnd(object sender, EventArgs e)
        {
            SaveWindowBounds();
        }

        private void DisplayTimerTick(object sender, EventArgs e)
        {
            UpdateDisplay();
            UpdateHourlyPrompt();
        }

        private void UpdateDisplay()
        {
            bool blocked = blockService.IsBlocked;

            statusLabel.Text = blocked ? "Blocked" : "Unblocked";
            statusLabel.ForeColor = blocked ? RedColor : GreenColor;
            statusDotColor = blocked ? RedColor : GreenColor;
            statusDot.Invalidate();

            if (!isHourlyPromptActive)
            {
                blockButton.Visible = !blocked;
                countdownLabel.Visible = blocked;
                hintLabel.Visible = true;
            }

            closeButton.Enabled = !blocked;
            closeButton.Cursor = blocked ? Cursors.Default : Cursors.Hand;

            if (blocked)
            {
                TimeSpan remaining = blockService.Remaining;
                int totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
                countdownLabel.Text = string.Format(
                    "{0:00}:{1:00}:{2:00}",
                    totalSeconds / 3600,
                    (totalSeconds % 3600) / 60,
                    totalSeconds % 60);
                taskbarStatus.Update(blocked, remaining, TimeSpan.FromHours(1));
            }
            else
            {
                taskbarStatus.Clear();
            }
        }

        private void UpdateHourlyPrompt()
        {
            bool blocked = blockService.IsBlocked;
            DateTime localNow = DateTime.Now;

            if (blocked && !wasBlockedOnPreviousTick)
            {
                expirationPromptShownForCurrentBlock = false;
            }
            else if (!blocked)
            {
                expirationPromptShownForCurrentBlock = false;
            }

            wasBlockedOnPreviousTick = blocked;

            if (!isHourlyPromptActive &&
                DateTime.UtcNow >= promptSuppressedUntilUtc &&
                !isSessionEnding &&
                HourlyPromptSchedule.ShouldShow(localNow, lastPromptHour, blocked))
            {
                ShowHourlyPrompt(localNow, true);
            }
            else if (!isHourlyPromptActive &&
                DateTime.UtcNow >= promptSuppressedUntilUtc &&
                !isSessionEnding &&
                HourlyPromptSchedule.ShouldShowBlockExpiration(
                    blockService.Remaining,
                    blocked,
                    expirationPromptShownForCurrentBlock))
            {
                expirationPromptShownForCurrentBlock = true;
                ShowHourlyPrompt(localNow, true);
            }

            if (!isHourlyPromptActive)
            {
                return;
            }

            if (localNow.Minute == 0)
            {
                lastPromptHour = HourlyPromptSchedule.GetHourKey(localNow);
            }

            int secondsRemaining = Math.Max(
                0,
                (int)Math.Ceiling((hourlyPromptUntilUtc - DateTime.UtcNow).TotalSeconds));
            if (secondsRemaining <= 0)
            {
                DismissHourlyPrompt();
                return;
            }

            promptCountdownLabel.Text = string.Format(
                "This prompt will dismiss in {0} second{1}",
                secondsRemaining,
                secondsRemaining == 1 ? string.Empty : "s");
        }

        private void ShowHourlyPrompt(DateTime localNow, bool requestAttention)
        {
            isHourlyPromptActive = true;
            promptRequestedAttention = requestAttention;
            lastPromptHour = HourlyPromptSchedule.GetHourKey(localNow);
            hourlyPromptUntilUtc = DateTime.UtcNow.AddSeconds(60);
            promptSuppressedUntilUtc = DateTime.UtcNow.AddMinutes(5);
            previousForegroundWindow = GetForegroundWindow();
            wasForegroundBeforeHourlyPrompt = previousForegroundWindow == Handle;
            wasMinimizedBeforeHourlyPrompt = WindowState == FormWindowState.Minimized;
            wasTopMostBeforeHourlyPrompt = TopMost;

            statusDot.Visible = false;
            statusLabel.Visible = false;
            countdownLabel.Visible = false;
            hintLabel.Visible = false;
            promptQuestionLabel.Visible = true;
            promptCountdownLabel.Visible = true;
            dismissButton.Visible = true;

            blockButton.Size = new Size(158, 50);
            blockButton.Location = new Point(24, 111);
            blockButton.Text = "Block Browsers";
            blockButton.Visible = true;

            if (!requestAttention)
            {
                return;
            }

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Show();
            TopMost = true;
            BringToFront();
            Activate();
            SetForegroundWindow(Handle);
        }

        private void DismissHourlyPrompt()
        {
            if (!isHourlyPromptActive)
            {
                return;
            }

            isHourlyPromptActive = false;
            RestoreWindowZOrderAfterPrompt();
            promptRequestedAttention = false;

            statusDot.Visible = true;
            statusLabel.Visible = true;
            promptQuestionLabel.Visible = false;
            promptCountdownLabel.Visible = false;
            dismissButton.Visible = false;
            hintLabel.Visible = true;

            blockButton.Size = new Size(332, 58);
            blockButton.Location = new Point(24, 102);
            UpdateDisplay();
            StopTaskbarFlash();
        }

        private void RestoreWindowZOrderAfterPrompt()
        {
            if (!promptRequestedAttention)
            {
                return;
            }

            TopMost = wasTopMostBeforeHourlyPrompt;

            if (wasMinimizedBeforeHourlyPrompt)
            {
                WindowState = FormWindowState.Minimized;
                StopTaskbarFlashSoon();
                return;
            }

            if (previousForegroundWindow != IntPtr.Zero &&
                previousForegroundWindow != Handle &&
                IsWindow(previousForegroundWindow))
            {
                if (!wasForegroundBeforeHourlyPrompt)
                {
                    ShowWindow(Handle, SwMinimize);
                }

                SetForegroundWindow(previousForegroundWindow);
            }
            else if (!wasTopMostBeforeHourlyPrompt)
            {
                SendToBack();
            }

            StopTaskbarFlashSoon();
        }

        private void StopTaskbarFlash()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            FlashWindowInfo flashInfo = new FlashWindowInfo
            {
                Size = (uint)Marshal.SizeOf(typeof(FlashWindowInfo)),
                WindowHandle = Handle,
                Flags = FlashwStop,
                Count = 0,
                Timeout = 0
            };
            FlashWindowEx(ref flashInfo);
        }

        private void StopTaskbarFlashSoon()
        {
            StopTaskbarFlash();
            int flashStopAttempts = 0;
            Timer flashStopTimer = new Timer { Interval = 250 };
            flashStopTimer.Tick += delegate
            {
                flashStopAttempts++;
                StopTaskbarFlash();
                if (flashStopAttempts >= 6)
                {
                    flashStopTimer.Stop();
                    flashStopTimer.Dispose();
                }
            };
            flashStopTimer.Start();
        }

        private void DrawStatusDot(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Brush brush = new SolidBrush(statusDotColor))
            {
                e.Graphics.FillEllipse(brush, 0, 0, statusDot.Width - 1, statusDot.Height - 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                displayTimer.Dispose();
                blockService.Dispose();
                taskbarStatus.Dispose();
                SystemEvents.SessionEnding -= SystemEventsSessionEnding;
            }

            base.Dispose(disposing);
        }

        private void RestoreWindowLocation()
        {
            Rectangle? savedBounds = windowSettingsStore.LoadBounds(Size);
            if (savedBounds.HasValue)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = savedBounds.Value;
            }
        }

        private void SaveWindowBounds()
        {
            if (WindowState == FormWindowState.Normal)
            {
                windowSettingsStore.SaveBounds(Bounds);
            }
        }
    }
}
