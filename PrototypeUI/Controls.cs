using System.Drawing.Drawing2D;

namespace LiveWall.UI;

static class Theme
{
    static Color C(int rgb) => Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);

    public static readonly Color
        Bg = C(0x1B1D22), TitleBar = C(0x15171B), Side = C(0x1F2227), Card = C(0x23262D),
        Active = C(0x2B2F37), Border = C(0x3A3F49), Text = C(0xE8EAED), Muted = C(0xA3A9B3),
        Accent = C(0x4CC2A8), AccentHover = C(0x66D1BA), OnAccent = C(0x0E1F1B), PreviewFill = C(0x2A3342);

    public static readonly Color[] TilePalette = { C(0x3B4A63), C(0x2F5249), C(0x4A3B5C), C(0x5C4A3B) };

    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font Small = new("Segoe UI", 9f);
    public static readonly Font Tiny = new("Segoe UI", 8.25f);
    public static readonly Font Semibold = new("Segoe UI Semibold", 9.5f);
    public static readonly Font H1 = new("Segoe UI Semibold", 15f);
    public static readonly Font H3 = new("Segoe UI Semibold", 10.5f);
    // Windows 11 có "Segoe Fluent Icons", Windows 10 có "Segoe MDL2 Assets"
    public static readonly Font Icons = new(
        FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets", 10f);

    const TextFormatFlags Base = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter |
                                 TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
    public const TextFormatFlags TxtLeft = Base | TextFormatFlags.Left;
    public const TextFormatFlags TxtCenter = Base | TextFormatFlags.HorizontalCenter;
    public const TextFormatFlags TxtRight = Base | TextFormatFlags.Right;

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Hình chữ nhật phủ kín box mà giữ đúng tỉ lệ ảnh (cắt phần thừa), như object-fit: cover.</summary>
    public static Rectangle Cover(Size img, Rectangle box)
    {
        float s = Math.Max((float)box.Width / img.Width, (float)box.Height / img.Height);
        int w = (int)(img.Width * s), h = (int)(img.Height * s);
        return new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
    }
}

/// <summary>Control tự vẽ: chống nháy, nền lấy theo parent.</summary>
abstract class BufferedControl : Control
{
    protected bool Hover;

    protected BufferedControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected Color Back => Parent?.BackColor ?? Theme.Bg;
    protected abstract void Draw(Graphics g);

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Back);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Draw(e.Graphics);
    }

    protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { Hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected void DrawFocusRing(Graphics g, Rectangle r, int radius)
    {
        if (!Focused || !ShowFocusCues) return;
        using var path = Theme.Round(r, radius);
        using var pen = new Pen(Theme.Accent, 1.5f);
        g.DrawPath(pen, path);
    }
}

enum ButtonStyle { Primary, Outline, Link }

sealed class RoundedButton : Button
{
    bool _hover;
    public ButtonStyle Style { get; set; } = ButtonStyle.Outline;

    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = Theme.Body;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Bg);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.Round(rect, 6);
        Color text;
        var font = Theme.Body;

        if (!Enabled)
        {
            using var b = new SolidBrush(Theme.Active);
            g.FillPath(b, path);
            text = Theme.Muted;
        }
        else switch (Style)
        {
            case ButtonStyle.Primary:
                using (var b = new SolidBrush(_hover ? Theme.AccentHover : Theme.Accent)) g.FillPath(b, path);
                text = Theme.OnAccent;
                font = Theme.Semibold;
                break;
            case ButtonStyle.Outline:
                if (_hover) using (var b = new SolidBrush(Theme.Active)) g.FillPath(b, path);
                using (var pen = new Pen(_hover ? Theme.Muted : Theme.Border)) g.DrawPath(pen, path);
                text = Theme.Text;
                break;
            default:
                text = _hover ? Theme.AccentHover : Theme.Accent;
                font = Theme.Small;
                break;
        }

        TextRenderer.DrawText(g, Text, font, ClientRectangle, text, Theme.TxtCenter);

        if (Focused && ShowFocusCues)
        {
            using var ring = Theme.Round(new Rectangle(1, 1, Width - 3, Height - 3), 6);
            using var pen = new Pen(Theme.Accent, 1.5f);
            g.DrawPath(pen, ring);
        }
    }
}

/// <summary>Thẻ nền bo góc chứa các cài đặt.</summary>
sealed class Card : Panel
{
    public Card()
    {
        DoubleBuffered = true;
        BackColor = Theme.Card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        using var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 8);
        using var b = new SolidBrush(Theme.Card);
        g.FillPath(b, path);
    }
}

/// <summary>Ô tích: nhãn bên trái, hộp tích bên phải.</summary>
sealed class AccentCheck : BufferedControl
{
    bool _checked;
    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public AccentCheck() { Size = new Size(300, 22); Cursor = Cursors.Hand; }

    protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void Draw(Graphics g)
    {
        TextRenderer.DrawText(g, Text, Theme.Body, new Rectangle(0, 0, Width - 26, Height), Theme.Text, Theme.TxtLeft);

        var box = new Rectangle(Width - 17, (Height - 16) / 2, 16, 16);
        using var path = Theme.Round(box, 4);
        if (_checked)
        {
            using var b = new SolidBrush(Theme.Accent);
            g.FillPath(b, path);
            using var pen = new Pen(Theme.OnAccent, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[] { new PointF(box.X + 3.5f, box.Y + 8.5f), new PointF(box.X + 6.5f, box.Y + 11.5f), new PointF(box.X + 12.5f, box.Y + 4.5f) });
        }
        else
        {
            using var pen = new Pen(Hover ? Theme.Muted : Theme.Border, 1.5f);
            g.DrawPath(pen, path);
        }
        DrawFocusRing(g, Rectangle.Inflate(box, 3, 3), 6);
    }
}

/// <summary>Ô video trong thư viện (hoặc ô "Thêm mới").</summary>
sealed class Tile : BufferedControl
{
    Bitmap? _thumb;
    public string Title { get; set; } = "";
    public bool IsAdd { get; set; }
    public bool Selected { get; set; }
    public Color Fill { get; set; } = Theme.TilePalette[0];

    public Bitmap? Thumb
    {
        get => _thumb;
        set { _thumb?.Dispose(); _thumb = value; Invalidate(); }
    }

    public Tile() { Size = new Size(120, 92); Cursor = Cursors.Hand; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void Draw(Graphics g)
    {
        var box = new Rectangle(1, 1, Width - 3, 66);
        using var path = Theme.Round(box, 6);

        if (IsAdd)
        {
            using var pen = new Pen(Hover ? Theme.Muted : Theme.Border, 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawPath(pen, path);
            TextRenderer.DrawText(g, "\uE710", Theme.Icons, box, Theme.Muted, Theme.TxtCenter);
        }
        else
        {
            if (_thumb != null)
            {
                var state = g.Save();
                g.SetClip(path);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(_thumb, Theme.Cover(_thumb.Size, box));
                g.Restore(state);
            }
            else
            {
                using var b = new SolidBrush(Fill);
                g.FillPath(b, path);
            }
            if (Selected)
            {
                using var pen = new Pen(Theme.Accent, 2f);
                g.DrawPath(pen, path);
            }
        }

        TextRenderer.DrawText(g, Title, Theme.Small, new Rectangle(0, 70, Width, 18),
            Selected ? Theme.Text : Theme.Muted, Theme.TxtLeft);
        DrawFocusRing(g, Rectangle.Inflate(box, 2, 2), 8);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _thumb?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Khung xem trước video: ảnh thumbnail, nút play, nhãn thông số.</summary>
sealed class PreviewBox : BufferedControl
{
    Bitmap? _image;
    public bool HasVideo { get; private set; }
    public string Chip { get; set; } = "";

    public PreviewBox() { Size = new Size(288, 162); }

    public void SetVideo(Bitmap? image, bool hasVideo)
    {
        _image?.Dispose();
        _image = image;
        HasVideo = hasVideo;
        Invalidate();
    }

    protected override void Draw(Graphics g)
    {
        var box = new Rectangle(0, 0, Width, Height);
        using var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 8);
        using (var b = new SolidBrush(Theme.PreviewFill)) g.FillPath(b, path);

        if (_image != null)
        {
            var state = g.Save();
            g.SetClip(path);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_image, Theme.Cover(_image.Size, box));
            g.Restore(state);
        }

        if (!HasVideo)
        {
            TextRenderer.DrawText(g, "Chưa có video", Theme.Body, box, Theme.Muted, Theme.TxtCenter);
            return;
        }

        var c = new Rectangle(Width / 2 - 22, Height / 2 - 22, 44, 44);
        using (var b = new SolidBrush(Color.FromArgb(115, 0, 0, 0))) g.FillEllipse(b, c);
        using (var b = new SolidBrush(Theme.Text))
            g.FillPolygon(b, new[] { new Point(c.X + 17, c.Y + 13), new Point(c.X + 31, c.Y + 22), new Point(c.X + 17, c.Y + 31) });

        if (Chip.Length > 0)
        {
            var size = TextRenderer.MeasureText(Chip, Theme.Tiny);
            var chip = new Rectangle(8, Height - 30, size.Width + 16, 22);
            using var cp = Theme.Round(chip, 4);
            using (var b = new SolidBrush(Color.FromArgb(153, 0, 0, 0))) g.FillPath(b, cp);
            TextRenderer.DrawText(g, Chip, Theme.Tiny, chip, Theme.Text, Theme.TxtCenter);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _image?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Mục điều hướng ở thanh bên.</summary>
sealed class NavItem : BufferedControl
{
    public string Glyph { get; set; } = "";
    public bool Active { get; set; }

    public NavItem() { Size = new Size(144, 36); Cursor = Cursors.Hand; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void Draw(Graphics g)
    {
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (Active || Hover)
        {
            using var path = Theme.Round(rect, 6);
            using var b = new SolidBrush(Active ? Theme.Active : Color.FromArgb(255, 0x26, 0x29, 0x30));
            g.FillPath(b, path);
        }
        TextRenderer.DrawText(g, Glyph, Theme.Icons, new Rectangle(8, 0, 24, Height), Active ? Theme.Accent : Theme.Muted, Theme.TxtCenter);
        TextRenderer.DrawText(g, Text, Theme.Body, new Rectangle(40, 0, Width - 44, Height), Active ? Theme.Text : Theme.Muted, Theme.TxtLeft);
        DrawFocusRing(g, rect, 6);
    }
}

/// <summary>Nút nhỏ ở thanh tiêu đề (thu nhỏ, đóng).</summary>
sealed class CaptionButton : BufferedControl
{
    public bool IsClose { get; set; }
    public Color HoverBack { get; set; } = Theme.Active;

    public CaptionButton()
    {
        Size = new Size(44, 36);
        Cursor = Cursors.Hand;
    }

    protected override void Draw(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (Hover)
        {
            Color bg = IsClose ? Color.FromArgb(232, 17, 35) : HoverBack;
            using var b = new SolidBrush(bg);
            g.FillRectangle(b, ClientRectangle);
        }

        int cx = Width / 2;
        int cy = Height / 2;
        Color iconColor = Hover ? Color.White : Theme.Text;

        using var pen = new Pen(iconColor, 1.3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        if (IsClose)
        {
            // Vẽ dấu ✕
            int r = 5;
            g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
            g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
        }
        else
        {
            // Vẽ dấu —
            int r = 5;
            g.DrawLine(pen, cx - r, cy + 1, cx + r, cy + 1);
        }
    }
}
