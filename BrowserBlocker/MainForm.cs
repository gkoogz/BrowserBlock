using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BrowserBlocker
{
    public sealed class MainForm : Form
    {
        private const int WmNclButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;

        private static readonly Color DarkBackground = Color.FromArgb(24, 26, 31);
        private static readonly Color DarkText = Color.White;
        private static readonly Color DarkMutedText = Color.FromArgb(165, 170, 180);
        private static readonly Color LightBackground = Color.FromArgb(244, 245, 248);
        private static readonly Color LightText = Color.FromArgb(30, 33, 39);
        private static readonly Color LightMutedText = Color.FromArgb(104, 109, 119);
        private static readonly Color RedColor = Color.FromArgb(224, 67, 67);
        private static readonly Color GreenColor = Color.FromArgb(65, 196, 112);

        private readonly BrowserBlockService blockService;
        private readonly Timer displayTimer;
        private readonly Panel statusDot;
        private readonly Label titleLabel;
        private readonly Label statusLabel;
        private readonly Button blockButton;
        private readonly Label countdownLabel;
        private readonly Label hintLabel;
        private readonly IconButton pinButton;
        private readonly IconButton themeButton;
        private readonly IconButton minimizeButton;
        private readonly IconButton closeButton;

        private bool isPinned;
        private bool isDarkMode = true;
        private Color statusDotColor = GreenColor;

        public MainForm()
        {
            blockService = new BrowserBlockService(BlockStateStore.CreateDefault());

            Text = "BrowserBlocker";
            ClientSize = new Size(380, 220);
            BackColor = DarkBackground;
            ForeColor = DarkText;
            Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            FormClosing += MainFormClosing;

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
                Text = "BrowserBlocker",
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

            countdownLabel = new Label
            {
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DarkText,
                Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold),
                Size = blockButton.Size,
                Location = blockButton.Location,
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
            Controls.Add(countdownLabel);
            Controls.Add(hintLabel);

            AttachDragHandler(this);

            displayTimer = new Timer { Interval = 200 };
            displayTimer.Tick += DisplayTimerTick;
            displayTimer.Start();

            blockService.Start();
            ApplyTheme();
            UpdateDisplay();
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr parameter, IntPtr value);

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
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "BrowserBlocker could not start the block.\r\n\r\n" + ex.Message,
                    "BrowserBlocker",
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
            if (blockService.IsBlocked && e.CloseReason != CloseReason.WindowsShutDown)
            {
                e.Cancel = true;
            }
        }

        private void DisplayTimerTick(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            bool blocked = blockService.IsBlocked;
            statusLabel.Text = blocked ? "Blocked" : "Unblocked";
            statusLabel.ForeColor = blocked ? RedColor : GreenColor;
            statusDotColor = blocked ? RedColor : GreenColor;
            statusDot.Invalidate();

            blockButton.Visible = !blocked;
            countdownLabel.Visible = blocked;
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
            }
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
            }

            base.Dispose(disposing);
        }
    }
}
