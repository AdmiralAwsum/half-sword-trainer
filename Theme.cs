using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace HalfSwordTrainer;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(17, 20, 18);
    public static readonly Color Surface = Color.FromArgb(27, 31, 26);
    public static readonly Color Border = Color.FromArgb(66, 65, 47);
    public static readonly Color Gold = Color.FromArgb(202, 172, 105);
    public static readonly Color Parchment = Color.FromArgb(235, 228, 209);
    public static readonly Color Muted = Color.FromArgb(150, 155, 137);
    public static readonly Color Green = Color.FromArgb(151, 185, 113);
    public static readonly Color Red = Color.FromArgb(191, 118, 102);

    public static float Scale(Control control) => control.DeviceDpi / 96f;

    public static GraphicsPath Rounded(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void DarkTitleBar(nint handle)
    {
        int dark = 1;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        int caption = ColorTranslator.ToWin32(Background);
        DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}

internal sealed class SurfacePanel : Panel
{
    public SurfacePanel()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
        Padding = new Padding(12);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

internal sealed class ActionButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Primary { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Danger { get; set; }

    public ActionButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        bool hover = Enabled && ClientRectangle.Contains(PointToClient(MousePosition));
        Color background = Danger && Enabled ? hover ? Color.FromArgb(83, 48, 42) : Color.FromArgb(61, 39, 35)
            : Primary && Enabled ? hover ? Color.FromArgb(224, 194, 130) : Theme.Gold
            : hover ? Color.FromArgb(49, 56, 38) : Theme.Surface;
        using var fill = new SolidBrush(background);
        e.Graphics.FillRectangle(fill, ClientRectangle);
        using var border = new Pen(Danger && Enabled ? Theme.Red : Primary && Enabled ? Theme.Gold : Theme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
            !Enabled ? Theme.Muted : Danger ? Theme.Parchment : Primary ? Theme.Background : ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), Theme.Gold, background);
    }
}

internal sealed class FeatureToggle : CheckBox
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string Description { get; set; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Compact { get; set; }

    public FeatureToggle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        Height = 72;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 8, 8);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 10, FontStyle.Bold);
        BackColor = Theme.Surface;
    }

    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        float s = Theme.Scale(this);
        bool hover = ClientRectangle.Contains(PointToClient(MousePosition)) && Enabled;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(Checked ? Color.FromArgb(36, 44, 31) : hover ? Color.FromArgb(35, 39, 31) : Theme.Surface);
        g.FillRectangle(fill, ClientRectangle);
        using var border = new Pen(Checked ? Color.FromArgb(105, 121, 71) : Theme.Border);
        if (!Compact) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        if (Checked && !Compact)
        {
            using var stripe = new SolidBrush(Theme.Gold);
            g.FillRectangle(stripe, 0, 12 * s, 2 * s, Height - 24 * s);
        }
        int inset = (int)((Compact ? 5 : 15) * s);
        int switchWidth = (int)(35 * s);
        int switchHeight = (int)(19 * s);
        var track = new RectangleF(Width - inset - switchWidth, (Height - switchHeight) / 2f, switchWidth, switchHeight);
        using var trackPath = Theme.Rounded(track, switchHeight / 2f);
        using var trackBrush = new SolidBrush(Checked ? Color.FromArgb(122, 145, 78) : Color.FromArgb(61, 65, 54));
        g.FillPath(trackBrush, trackPath);
        using var knob = new SolidBrush(Enabled ? Checked ? Theme.Parchment : Theme.Muted : Color.FromArgb(90, 93, 82));
        float circle = switchHeight - 6 * s;
        g.FillEllipse(knob, Checked ? track.Right - circle - 3 * s : track.Left + 3 * s, track.Top + 3 * s, circle, circle);
        int available = (int)track.Left - inset - (int)(12 * s);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(inset, (int)(13 * s), available, (int)(23 * s)),
            Enabled ? Theme.Parchment : Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        using var detail = new Font("Segoe UI", 8.25f);
        TextRenderer.DrawText(g, Description, detail, new Rectangle(inset, (int)(39 * s), available, (int)(21 * s)),
            Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -4, -4), Theme.Gold, BackColor);
    }
}

internal sealed class VitalDisplay : Control
{
    private double? value;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color Accent { get; init; } = Theme.Green;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal double? Value
    {
        get => value;
        set { this.value = value; AccessibleName = $"{Text}: {(value.HasValue ? $"{value:F1}" : "unavailable")}"; Invalidate(); }
    }

    public VitalDisplay()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 8, 0);
        BackColor = Theme.Surface;
        AccessibleRole = AccessibleRole.StaticText;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        float s = Theme.Scale(this);
        var g = e.Graphics;
        using var border = new Pen(Theme.Border);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using var label = new Font("Segoe UI", 8, FontStyle.Bold);
        using var number = new Font("Palatino Linotype", 23);
        int inset = (int)(14 * s);
        TextRenderer.DrawText(g, Text.ToUpperInvariant(), label, new Point(inset, (int)(11 * s)), Theme.Muted);
        TextRenderer.DrawText(g, value.HasValue ? value.Value.ToString("0.0") : "--", number,
            new Rectangle(inset - (int)(2 * s), (int)(28 * s), Width - inset * 2, (int)(41 * s)),
            Theme.Parchment, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        using var track = new SolidBrush(Color.FromArgb(53, 58, 46));
        using var progress = new SolidBrush(Accent);
        float width = Width - inset * 2;
        g.FillRectangle(track, inset, Height - 10 * s, width, 2 * s);
        if (value.HasValue) g.FillRectangle(progress, inset, Height - 10 * s, (float)Math.Clamp(value.Value / 100, 0, 1) * width, 2 * s);
    }
}

internal sealed class PortraitPanel : Control
{
    private readonly Image portrait;

    public PortraitPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        using var stream = typeof(PortraitPanel).Assembly.GetManifestResourceStream("HalfSwordTrainer.Assets.Jester.png")
            ?? throw new InvalidOperationException("The embedded jester portrait is missing.");
        using var source = Image.FromStream(stream);
        portrait = new Bitmap(source);
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 24, 0);
        AccessibleName = "The Fool's Favour. Jester portrait.";
        AccessibleRole = AccessibleRole.Graphic;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 2 || Height < 2) return;
        float s = Theme.Scale(this);
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        float factor = Math.Max((float)Width / portrait.Width, (float)Height / portrait.Height);
        float width = portrait.Width * factor, height = portrait.Height * factor;
        g.DrawImage(portrait, (Width - width) / 2, 0, width, height);
        using var tint = new SolidBrush(Color.FromArgb(20, 16, 23, 12));
        g.FillRectangle(tint, ClientRectangle);
        var lower = new Rectangle(0, Height - (int)(250 * s), Width, (int)(250 * s));
        using var fade = new LinearGradientBrush(lower, Color.Transparent, Color.FromArgb(255, 13, 18, 12), 90);
        fade.WrapMode = WrapMode.TileFlipY;
        g.FillRectangle(fade, lower);
        using var topFade = new LinearGradientBrush(new Rectangle(0, 0, Width, (int)(95 * s)),
            Color.FromArgb(215, 13, 18, 12), Color.Transparent, 90);
        topFade.WrapMode = WrapMode.TileFlipY;
        g.FillRectangle(topFade, 0, 0, Width, 95 * s);
        using var edge = new Pen(Color.FromArgb(118, 105, 64));
        g.DrawRectangle(edge, 0, 0, Width - 1, Height - 1);
        using var inner = new Pen(Color.FromArgb(85, 111, 95, 53));
        g.DrawRectangle(inner, 7 * s, 7 * s, Width - 14 * s - 1, Height - 14 * s - 1);
        int inset = (int)(24 * s);
        using var caption = new Font("Segoe UI", 8, FontStyle.Bold);
        using var title = new Font("Palatino Linotype", 25);
        using var small = new Font("Segoe UI", 9);
        TextRenderer.DrawText(g, "THE FOOL'S FAVOUR", caption, new Point(inset, (int)(26 * s)), Theme.Gold);
        int baseline = Height - (int)(185 * s);
        TextRenderer.DrawText(g, "Fortune favours\nthe foolish.", title, new Rectangle(inset, baseline, Width - inset * 2, (int)(105 * s)),
            Theme.Parchment, TextFormatFlags.NoPadding);
        using var line = new Pen(Theme.Gold);
        g.DrawLine(line, inset, Height - 64 * s, inset + 42 * s, Height - 64 * s);
        TextRenderer.DrawText(g, "Your rules. Their misfortune.", small, new Point(inset, Height - (int)(48 * s)), Theme.Muted);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) portrait.Dispose();
        base.Dispose(disposing);
    }
}
