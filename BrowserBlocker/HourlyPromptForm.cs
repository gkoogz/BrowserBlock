using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrowserBlocker
{
    internal sealed class HourlyPromptForm : Form
    {
        private static readonly Color DarkBackground = Color.FromArgb(24, 26, 31);
        private static readonly Color DarkText = Color.White;
        private static readonly Color DarkMutedText = Color.FromArgb(165, 170, 180);
        private static readonly Color RedColor = Color.FromArgb(224, 67, 67);
        private static readonly Color DismissColor = Color.FromArgb(91, 96, 106);

        private readonly Label countdownLabel;
        private readonly Button blockButton;
        private readonly Button dismissButton;

        public HourlyPromptForm()
        {
            Text = AppPaths.AppName;
            ClientSize = new Size(380, 220);
            BackColor = DarkBackground;
            ForeColor = DarkText;
            Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;

            Label titleLabel = new Label
            {
                Text = AppPaths.AppName,
                Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
                ForeColor = DarkText,
                AutoSize = true,
                Location = new Point(119, 12)
            };

            Label questionLabel = new Label
            {
                Text = "Would you like to block your browser for an hour?",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DarkText,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                Size = new Size(332, 42),
                Location = new Point(24, 56)
            };

            blockButton = new Button
            {
                Text = "Block Browsers",
                FlatStyle = FlatStyle.Flat,
                BackColor = RedColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                Size = new Size(158, 50),
                Location = new Point(24, 111),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            blockButton.FlatAppearance.BorderSize = 0;
            blockButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(236, 78, 78);
            blockButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(194, 52, 52);
            blockButton.Click += delegate { BlockRequested?.Invoke(this, EventArgs.Empty); };

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
                UseVisualStyleBackColor = false
            };
            dismissButton.FlatAppearance.BorderSize = 0;
            dismissButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(108, 114, 125);
            dismissButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(73, 78, 87);
            dismissButton.Click += delegate { DismissRequested?.Invoke(this, EventArgs.Empty); };

            countdownLabel = new Label
            {
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DarkMutedText,
                Font = new Font("Segoe UI", 9F),
                Size = new Size(332, 24),
                Location = new Point(24, 174)
            };

            Controls.Add(titleLabel);
            Controls.Add(questionLabel);
            Controls.Add(blockButton);
            Controls.Add(dismissButton);
            Controls.Add(countdownLabel);
        }

        public event EventHandler BlockRequested;

        public event EventHandler DismissRequested;

        public void SetSecondsRemaining(int secondsRemaining)
        {
            countdownLabel.Text = string.Format(
                "This prompt will dismiss in {0} second{1}",
                secondsRemaining,
                secondsRemaining == 1 ? string.Empty : "s");
        }

        public void SetBlockButtonEnabled(bool enabled)
        {
            blockButton.Enabled = enabled;
        }
    }
}
