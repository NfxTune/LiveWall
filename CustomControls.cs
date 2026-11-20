using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiveWallpaper
{
    public static class UiColors
    {
        public static readonly Color Background = Color.FromArgb(19, 25, 30);      // #13191E
        public static readonly Color SidebarBg = Color.FromArgb(17, 22, 27);        // #11161B
        public static readonly Color CardBg = Color.FromArgb(24, 32, 38);          // #182026
        public static readonly Color ControlBg = Color.FromArgb(36, 46, 53);       // #242E35
        public static readonly Color AccentMint = Color.FromArgb(32, 201, 151);    // #20C997
        public static readonly Color AccentLight = Color.FromArgb(56, 217, 169);   // #38D9A9
        public static readonly Color TextPrimary = Color.FromArgb(230, 237, 243);  // #E6EDF3
        public static readonly Color TextSecondary = Color.FromArgb(139, 148, 158);// #8B949E
        public static readonly Color TextMuted = Color.FromArgb(110, 118, 129);    // #6E7681
        public static readonly Color BorderColor = Color.FromArgb(48, 54, 61);     // #30363D
    }

    public class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 12;
        public Color BorderColor { get; set; } = Color.Transparent;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = UiColors.CardBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = GetRoundedPath(rect, CornerRadius);

            using SolidBrush brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);

            if (BorderColor != Color.Transparent)
            {
                using Pen pen = new Pen(BorderColor, 1.5f);
                e.Graphics.DrawPath(pen, path);
            }
        }

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ModernCheckBox : Control
    {
        private bool _checked = false;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public event EventHandler? CheckedChanged;

        public ModernCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            ForeColor = UiColors.TextPrimary;
            Size = new Size(200, 26);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw label text on the left
            using SolidBrush textBrush = new SolidBrush(ForeColor);
            StringFormat sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = StringAlignment.Near
            };
            e.Graphics.DrawString(Text, Font, textBrush, new Rectangle(0, 0, Width - 32, Height), sf);

            // Draw toggle box on the right
            int boxSize = 18;
            int boxX = Width - boxSize - 4;
            int boxY = (Height - boxSize) / 2;
            Rectangle boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

            using GraphicsPath path = RoundedPanel.GetRoundedPath(boxRect, 4);

            if (Checked)
            {
                using SolidBrush bgBrush = new SolidBrush(UiColors.AccentMint);
                e.Graphics.FillPath(bgBrush, path);

                // Draw white checkmark
                using Pen checkPen = new Pen(Color.FromArgb(19, 25, 30), 2.2f);
                checkPen.StartCap = LineCap.Round;
                checkPen.EndCap = LineCap.Round;

                Point[] points = new Point[]
                {
                    new Point(boxX + 4, boxY + 9),
                    new Point(boxX + 7, boxY + 13),
                    new Point(boxX + 13, boxY + 5)
                };
                e.Graphics.DrawLines(checkPen, points);
            }
            else
            {
                using SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(245, 245, 245));
                e.Graphics.FillPath(emptyBrush, path);

                using Pen borderPen = new Pen(Color.FromArgb(160, 160, 160), 1f);
                e.Graphics.DrawPath(borderPen, path);
            }
        }
    }

    public class ModernSlider : Control
    {
        private int _value = 0;
        private int _maximum = 100;
        private bool _isDragging = false;

        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Clamp(value, 0, _maximum);
                if (_value != clamped)
                {
                    _value = clamped;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = Math.Max(1, value);
                Invalidate();
            }
        }

        public event EventHandler? ValueChanged;

        public ModernSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Size = new Size(160, 24);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                UpdateValueFromMouse(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                UpdateValueFromMouse(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isDragging = false;
        }

        private void UpdateValueFromMouse(int mouseX)
        {
            int padding = 6;
            int trackWidth = Width - padding * 2;
            float ratio = (float)(mouseX - padding) / trackWidth;
            ratio = Math.Clamp(ratio, 0f, 1f);
            Value = (int)Math.Round(ratio * _maximum);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int padding = 6;
            int trackHeight = 6;
            int trackY = (Height - trackHeight) / 2;
            int trackWidth = Width - padding * 2;

            // Track background
            Rectangle bgRect = new Rectangle(padding, trackY, trackWidth, trackHeight);
            using GraphicsPath bgPath = RoundedPanel.GetRoundedPath(bgRect, 3);
            using SolidBrush bgBrush = new SolidBrush(Color.FromArgb(42, 53, 61));
            e.Graphics.FillPath(bgBrush, path: bgPath);

            // Active track fill
            int fillWidth = (int)((float)_value / _maximum * trackWidth);
            if (fillWidth > 0)
            {
                Rectangle fillRect = new Rectangle(padding, trackY, fillWidth, trackHeight);
                using GraphicsPath fillPath = RoundedPanel.GetRoundedPath(fillRect, 3);
                using SolidBrush fillBrush = new SolidBrush(UiColors.AccentMint);
                e.Graphics.FillPath(fillBrush, fillPath);
            }

            // Circular thumb
            int thumbRadius = 7;
            int thumbX = padding + fillWidth;
            int thumbY = Height / 2;

            using SolidBrush thumbBrush = new SolidBrush(UiColors.AccentLight);
            e.Graphics.FillEllipse(thumbBrush, thumbX - thumbRadius, thumbY - thumbRadius, thumbRadius * 2, thumbRadius * 2);

            using SolidBrush innerBrush = new SolidBrush(UiColors.AccentMint);
            e.Graphics.FillEllipse(innerBrush, thumbX - 3, thumbY - 3, 6, 6);
        }
    }

    public class WallpaperCardControl : Control
    {
        public string Title { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public Color ThumbColor { get; set; } = Color.FromArgb(45, 60, 75);
        public bool IsSelected { get; set; } = false;
        public bool IsAddCard { get; set; } = false;

        public WallpaperCardControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(130, 95);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int thumbHeight = 62;
            Rectangle thumbRect = new Rectangle(1, 1, Width - 3, thumbHeight);

            if (IsAddCard)
            {
                // Dashed border for Add Card
                using GraphicsPath path = RoundedPanel.GetRoundedPath(thumbRect, 8);
                using Pen dashPen = new Pen(UiColors.TextMuted, 1.5f) { DashStyle = DashStyle.Dash };
                e.Graphics.DrawPath(dashPen, path);

                // Draw '+' icon
                int cx = thumbRect.X + thumbRect.Width / 2;
                int cy = thumbRect.Y + thumbRect.Height / 2;
                using Pen plusPen = new Pen(UiColors.TextSecondary, 2f);
                e.Graphics.DrawLine(plusPen, cx - 8, cy, cx + 8, cy);
                e.Graphics.DrawLine(plusPen, cx, cy - 8, cx, cy + 8);
            }
            else
            {
                using GraphicsPath path = RoundedPanel.GetRoundedPath(thumbRect, 8);
                using SolidBrush bgBrush = new SolidBrush(ThumbColor);
                e.Graphics.FillPath(bgBrush, path);

                // Highlight border if selected
                if (IsSelected)
                {
                    using Pen activePen = new Pen(UiColors.AccentMint, 2.5f);
                    e.Graphics.DrawPath(activePen, path);
                }
                else
                {
                    using Pen normalPen = new Pen(Color.FromArgb(40, 50, 60), 1f);
                    e.Graphics.DrawPath(normalPen, path);
                }
            }

            // Text label below
            Rectangle textRect = new Rectangle(0, thumbHeight + 4, Width, Height - thumbHeight - 4);
            using SolidBrush textBrush = new SolidBrush(IsSelected ? UiColors.TextPrimary : UiColors.TextSecondary);
            StringFormat sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            };
            e.Graphics.DrawString(Title, Font, textBrush, textRect, sf);
        }
    }
}
