using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiveWallpaper
{
    public static class UiColors
    {
        public static readonly Color Background = Color.FromArgb(17, 22, 27);      // #11161B
        public static readonly Color SidebarBg = Color.FromArgb(13, 17, 21);        // #0D1115
        public static readonly Color CardBg = Color.FromArgb(22, 31, 38);          // #161F26
        public static readonly Color CardBgHover = Color.FromArgb(28, 39, 48);     // #1C2730
        public static readonly Color ControlBg = Color.FromArgb(32, 44, 53);       // #202C35
        public static readonly Color ControlHover = Color.FromArgb(42, 57, 68);    // #2A3944
        public static readonly Color AccentMint = Color.FromArgb(32, 201, 151);    // #20C997
        public static readonly Color AccentHover = Color.FromArgb(48, 225, 173);   // #30E1AD
        public static readonly Color AccentLight = Color.FromArgb(56, 217, 169);   // #38D9A9
        public static readonly Color TextPrimary = Color.FromArgb(240, 246, 252);  // #F0F6FC
        public static readonly Color TextSecondary = Color.FromArgb(145, 155, 166);// #919BA6
        public static readonly Color TextMuted = Color.FromArgb(100, 110, 120);    // #646E78
        public static readonly Color BorderColor = Color.FromArgb(38, 50, 60);     // #26323C
        public static readonly Color BorderLight = Color.FromArgb(55, 70, 84);     // #374654
        public static readonly Color DangerRed = Color.FromArgb(248, 81, 73);      // #F85149
    }

    public class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 12;
        public Color BorderColor { get; set; } = UiColors.BorderColor;
        public float BorderWidth { get; set; } = 1f;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = UiColors.CardBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = GetRoundedPath(rect, CornerRadius);

            using SolidBrush brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);

            if (BorderColor != Color.Transparent && BorderWidth > 0)
            {
                using Pen pen = new Pen(BorderColor, BorderWidth);
                e.Graphics.DrawPath(pen, path);
            }
        }

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Công tắc gạt hiện đại phong cách Fluent UI / iOS
    /// </summary>
    public class ModernToggleSwitch : Control
    {
        private bool _checked = false;
        private bool _isHovered = false;

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

        public ModernToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            ForeColor = UiColors.TextPrimary;
            Size = new Size(220, 28);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Nhãn chữ bên trái
            using (SolidBrush textBrush = new SolidBrush(_isHovered ? UiColors.TextPrimary : UiColors.TextSecondary))
            {
                StringFormat sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near
                };
                e.Graphics.DrawString(Text, Font, textBrush, new Rectangle(0, 0, Width - 48, Height), sf);
            }

            // Công tắc pill bên phải (Width: 40, Height: 22)
            int switchW = 40;
            int switchH = 22;
            int switchX = Width - switchW - 4;
            int switchY = (Height - switchH) / 2;
            Rectangle switchRect = new Rectangle(switchX, switchY, switchW, switchH);

            using GraphicsPath trackPath = RoundedPanel.GetRoundedPath(switchRect, switchH / 2);

            Color trackColor = Checked ? UiColors.AccentMint : UiColors.ControlBg;
            using (SolidBrush trackBrush = new SolidBrush(trackColor))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            if (!Checked)
            {
                using Pen borderPen = new Pen(UiColors.BorderColor, 1.2f);
                e.Graphics.DrawPath(borderPen, trackPath);
            }

            // Nút tròn thumb trượt
            int thumbDiameter = 16;
            int thumbY = switchY + (switchH - thumbDiameter) / 2;
            int thumbX = Checked ? (switchX + switchW - thumbDiameter - 3) : (switchX + 3);

            using (SolidBrush thumbBrush = new SolidBrush(Checked ? Color.FromArgb(17, 22, 27) : UiColors.TextPrimary))
            {
                e.Graphics.FillEllipse(thumbBrush, thumbX, thumbY, thumbDiameter, thumbDiameter);
            }
        }
    }

    /// <summary>
    /// Slider trượt hiện đại hiển thị mức % trực quan
    /// </summary>
    public class ModernSlider : Control
    {
        private int _value = 40;
        private int _maximum = 100;
        private bool _isDragging = false;
        private bool _isHovered = false;

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
            Size = new Size(180, 26);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

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
            if (_isDragging) UpdateValueFromMouse(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isDragging = false;
        }

        private void UpdateValueFromMouse(int mouseX)
        {
            int labelWidth = 42;
            int trackWidth = Width - labelWidth - 12;
            float ratio = (float)mouseX / trackWidth;
            ratio = Math.Clamp(ratio, 0f, 1f);
            Value = (int)Math.Round(ratio * _maximum);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int labelWidth = 40;
            int padding = 4;
            int trackHeight = 6;
            int trackY = (Height - trackHeight) / 2;
            int trackWidth = Width - labelWidth - padding * 2;

            // Track nền xám
            Rectangle bgRect = new Rectangle(padding, trackY, trackWidth, trackHeight);
            using GraphicsPath bgPath = RoundedPanel.GetRoundedPath(bgRect, 3);
            using (SolidBrush bgBrush = new SolidBrush(UiColors.ControlBg))
            {
                e.Graphics.FillPath(bgBrush, bgPath);
            }

            // Track đang kích hoạt (Màu Mint)
            int fillWidth = (int)((float)_value / _maximum * trackWidth);
            if (fillWidth > 0)
            {
                Rectangle fillRect = new Rectangle(padding, trackY, fillWidth, trackHeight);
                using GraphicsPath fillPath = RoundedPanel.GetRoundedPath(fillRect, 3);
                using (SolidBrush fillBrush = new SolidBrush(UiColors.AccentMint))
                {
                    e.Graphics.FillPath(fillBrush, fillPath);
                }
            }

            // Nút Thumb tròn
            int thumbRadius = _isHovered || _isDragging ? 8 : 7;
            int thumbX = padding + fillWidth;
            int thumbY = Height / 2;

            using (SolidBrush thumbGlow = new SolidBrush(Color.FromArgb(80, UiColors.AccentMint)))
            {
                e.Graphics.FillEllipse(thumbGlow, thumbX - thumbRadius - 2, thumbY - thumbRadius - 2, (thumbRadius + 2) * 2, (thumbRadius + 2) * 2);
            }

            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(thumbBrush, thumbX - thumbRadius, thumbY - thumbRadius, thumbRadius * 2, thumbRadius * 2);
            }

            // Nhãn hiển thị giá trị % bên phải
            using (SolidBrush textBrush = new SolidBrush(UiColors.TextSecondary))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString($"{_value}%", Font, textBrush, new Rectangle(Width - labelWidth, 0, labelWidth, Height), sf);
            }
        }
    }

    /// <summary>
    /// Thẻ hiển thị hình nền với viền phát sáng, thumbnail màu mượt mà và nút xoá
    /// </summary>
    public class WallpaperCardControl : Control
    {
        public string Title { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public Color ThumbColor { get; set; } = Color.FromArgb(45, 60, 75);
        public bool IsSelected { get; set; } = false;
        public bool IsAddCard { get; set; } = false;

        private bool _isHovered = false;
        private bool _deleteHovered = false;
        private Rectangle _deleteRect;

        public event EventHandler? DeleteRequested;

        public WallpaperCardControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(130, 100);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; _deleteHovered = false; Invalidate(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!IsAddCard && _isHovered)
            {
                bool prev = _deleteHovered;
                _deleteHovered = _deleteRect.Contains(e.Location);
                if (prev != _deleteHovered) Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (!IsAddCard && _deleteRect.Contains(e.Location))
            {
                DeleteRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int thumbHeight = 65;
            Rectangle thumbRect = new Rectangle(2, 2, Width - 5, thumbHeight);
            _deleteRect = new Rectangle(thumbRect.Right - 18, thumbRect.Top + 3, 15, 15);

            if (IsAddCard)
            {
                // Thẻ Thêm mới [+]
                using GraphicsPath path = RoundedPanel.GetRoundedPath(thumbRect, 8);
                Color dashColor = _isHovered ? UiColors.AccentMint : UiColors.TextMuted;
                using Pen dashPen = new Pen(dashColor, 1.5f) { DashStyle = DashStyle.Dash };
                e.Graphics.DrawPath(dashPen, path);

                int cx = thumbRect.X + thumbRect.Width / 2;
                int cy = thumbRect.Y + thumbRect.Height / 2;
                using Pen plusPen = new Pen(dashColor, 2f);
                e.Graphics.DrawLine(plusPen, cx - 8, cy, cx + 8, cy);
                e.Graphics.DrawLine(plusPen, cx, cy - 8, cx, cy + 8);
            }
            else
            {
                // Thẻ Video thông thường
                using GraphicsPath path = RoundedPanel.GetRoundedPath(thumbRect, 8);

                // Nền gradient tinh tế
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                    thumbRect,
                    Color.FromArgb(255, ThumbColor),
                    Color.FromArgb(200, ThumbColor.R / 2, ThumbColor.G / 2, ThumbColor.B / 2),
                    LinearGradientMode.Vertical))
                {
                    e.Graphics.FillPath(bgBrush, path);
                }

                // Viền chọn hoặc hover
                if (IsSelected)
                {
                    using Pen activePen = new Pen(UiColors.AccentMint, 2.5f);
                    e.Graphics.DrawPath(activePen, path);
                }
                else if (_isHovered)
                {
                    using Pen hoverPen = new Pen(UiColors.BorderLight, 1.5f);
                    e.Graphics.DrawPath(hoverPen, path);
                }
                else
                {
                    using Pen borderPen = new Pen(UiColors.BorderColor, 1f);
                    e.Graphics.DrawPath(borderPen, path);
                }

                // Nút xoá [✕] khi hover
                if (_isHovered)
                {
                    using (SolidBrush delBg = new SolidBrush(_deleteHovered ? UiColors.DangerRed : Color.FromArgb(160, 17, 22, 27)))
                    {
                        e.Graphics.FillEllipse(delBg, _deleteRect);
                    }
                    using (Pen xPen = new Pen(Color.White, 1.5f))
                    {
                        e.Graphics.DrawLine(xPen, _deleteRect.X + 4, _deleteRect.Y + 4, _deleteRect.Right - 4, _deleteRect.Bottom - 4);
                        e.Graphics.DrawLine(xPen, _deleteRect.Right - 4, _deleteRect.Y + 4, _deleteRect.X + 4, _deleteRect.Bottom - 4);
                    }
                }
            }

            // Nhãn chữ bên dưới
            Rectangle textRect = new Rectangle(0, thumbHeight + 5, Width, Height - thumbHeight - 5);
            Color titleColor = IsSelected ? UiColors.TextPrimary : (_isHovered ? UiColors.TextPrimary : UiColors.TextSecondary);
            using (SolidBrush textBrush = new SolidBrush(titleColor))
            {
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

    /// <summary>
    /// Nút chuyển tab ở Sidebar với hiệu ứng Active Pill
    /// </summary>
    public class ModernTabButton : Control
    {
        private bool _isActive = false;
        private bool _isHovered = false;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    Invalidate();
                }
            }
        }

        public string IconSymbol { get; set; } = "●";

        public ModernTabButton(string text, string icon, bool active = false)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Text = text;
            IconSymbol = icon;
            IsActive = active;
            Size = new Size(140, 38);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = RoundedPanel.GetRoundedPath(rect, 8);

            if (IsActive)
            {
                using SolidBrush activeBg = new SolidBrush(UiColors.CardBg);
                e.Graphics.FillPath(activeBg, path);

                using Pen activeBorder = new Pen(UiColors.BorderLight, 1f);
                e.Graphics.DrawPath(activeBorder, path);

                // Dải highlight nhỏ bên trái
                using SolidBrush pill = new SolidBrush(UiColors.AccentMint);
                e.Graphics.FillRoundedRectangle(pill, 3, 8, 3, Height - 16, 2);
            }
            else if (_isHovered)
            {
                using SolidBrush hoverBg = new SolidBrush(UiColors.ControlBg);
                e.Graphics.FillPath(hoverBg, path);
            }

            Color textColor = IsActive ? UiColors.TextPrimary : (_isHovered ? UiColors.TextPrimary : UiColors.TextSecondary);
            using (SolidBrush brush = new SolidBrush(textColor))
            {
                // Vẽ Icon
                Font iconFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                e.Graphics.DrawString(IconSymbol, iconFont, brush, new Point(14, 8));

                // Vẽ Text
                e.Graphics.DrawString(Text, Font, brush, new Point(38, 9));
            }
        }
    }

    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int width, int height, int radius)
        {
            using GraphicsPath path = RoundedPanel.GetRoundedPath(new Rectangle(x, y, width, height), radius);
            g.FillPath(brush, path);
        }
    }
}
