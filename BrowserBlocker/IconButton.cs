using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrowserBlocker
{
    public enum IconButtonKind
    {
        Pin,
        Sun,
        Moon,
        Minimize,
        Close
    }

    public sealed class IconButton : Control
    {
        private bool hovered;
        private bool pressed;
        private bool highlighted;
        private IconButtonKind iconKind;

        public IconButton()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Size = new Size(30, 30);
            TabStop = false;
        }

        public IconButtonKind IconKind
        {
            get { return iconKind; }
            set
            {
                iconKind = value;
                Invalidate();
            }
        }

        public Color IconColor { get; set; } = Color.White;

        public Color HoverColor { get; set; } = Color.FromArgb(38, Color.White);

        public Color PressedColor { get; set; } = Color.FromArgb(64, Color.White);

        public Color HighlightColor { get; set; } = Color.FromArgb(54, 224, 67, 67);

        public bool Highlighted
        {
            get { return highlighted; }
            set
            {
                highlighted = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color effectiveIconColor = Enabled
                ? IconColor
                : Color.FromArgb(105, IconColor);

            Color background = !Enabled
                ? Color.Transparent
                : pressed
                ? PressedColor
                : highlighted
                    ? HighlightColor
                    : hovered ? HoverColor : Color.Transparent;

            if (background.A > 0)
            {
                using (Brush brush = new SolidBrush(background))
                {
                    graphics.FillEllipse(brush, 1, 1, Width - 2, Height - 2);
                }
            }

            using (Pen pen = new Pen(effectiveIconColor, 1.8F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                DrawIcon(graphics, pen);
            }
        }

        private void DrawIcon(Graphics graphics, Pen pen)
        {
            switch (iconKind)
            {
                case IconButtonKind.Pin:
                    DrawPin(graphics, pen);
                    break;
                case IconButtonKind.Sun:
                    DrawSun(graphics, pen);
                    break;
                case IconButtonKind.Moon:
                    DrawMoon(graphics, pen);
                    break;
                case IconButtonKind.Minimize:
                    graphics.DrawLine(pen, 9, 18, 21, 18);
                    break;
                case IconButtonKind.Close:
                    graphics.DrawLine(pen, 10, 10, 20, 20);
                    graphics.DrawLine(pen, 20, 10, 10, 20);
                    break;
            }
        }

        private static void DrawPin(Graphics graphics, Pen pen)
        {
            graphics.DrawLine(pen, 11, 8, 19, 8);
            graphics.DrawLine(pen, 12, 8, 12, 13);
            graphics.DrawLine(pen, 18, 8, 18, 13);
            graphics.DrawLine(pen, 10, 13, 20, 13);
            graphics.DrawLine(pen, 10, 13, 15, 18);
            graphics.DrawLine(pen, 20, 13, 15, 18);
            graphics.DrawLine(pen, 15, 18, 15, 23);
        }

        private static void DrawSun(Graphics graphics, Pen pen)
        {
            graphics.DrawEllipse(pen, 11, 11, 8, 8);
            for (int index = 0; index < 8; index++)
            {
                double angle = index * Math.PI / 4;
                float innerX = 15F + (float)Math.Cos(angle) * 6F;
                float innerY = 15F + (float)Math.Sin(angle) * 6F;
                float outerX = 15F + (float)Math.Cos(angle) * 9F;
                float outerY = 15F + (float)Math.Sin(angle) * 9F;
                graphics.DrawLine(pen, innerX, innerY, outerX, outerY);
            }
        }

        private static void DrawMoon(Graphics graphics, Pen pen)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(9, 7, 15, 16, 70, 220);
                path.AddArc(12, 7, 12, 13, 245, -205);
                path.CloseFigure();
                using (Brush brush = new SolidBrush(pen.Color))
                {
                    graphics.FillPath(brush, path);
                }
            }
        }
    }
}
