using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace SoundSwitch;

internal sealed class OutputOverlay : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private readonly System.Windows.Forms.Timer dismiss = new() { Interval = 1500 };
    private readonly Font deviceFont = new("Segoe UI", 13, FontStyle.Bold);
    private readonly Font labelFont = new("Segoe UI", 9);
    private string deviceName = string.Empty;
    private int dpi = 96;

    internal OutputOverlay()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(26, 28, 34);
        DoubleBuffered = true;
        dismiss.Tick += (_, _) =>
        {
            dismiss.Stop();
            Hide();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
            return parameters;
        }
    }

    internal void ShowOutput(string name)
    {
        deviceName = name;
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        if (!area.Contains(Location) || !IsHandleCreated)
            Location = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);

        dpi = (int)GetDpiForWindow(Handle);
        if (dpi == 0)
            dpi = 96;

        int width = Math.Min(ScalePixel(390), area.Width);
        int height = ScalePixel(74);
        Bounds = new Rectangle(area.Left + (area.Width - width) / 2, area.Bottom - height - ScalePixel(44), width, height);
        Invalidate();
        Show();
        dismiss.Stop();
        dismiss.Start();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0)
            return;

        int diameter = Math.Min(ScalePixel(24), Math.Min(Width, Height));
        using var outline = new GraphicsPath();
        outline.AddArc(0, 0, diameter, diameter, 180, 90);
        outline.AddArc(Width - diameter, 0, diameter, diameter, 270, 90);
        outline.AddArc(Width - diameter, Height - diameter, diameter, diameter, 0, 90);
        outline.AddArc(0, Height - diameter, diameter, diameter, 90, 90);
        outline.CloseFigure();
        Region = new Region(outline);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var accent = new SolidBrush(Color.FromArgb(43, 56, 83));
        e.Graphics.FillEllipse(accent, ScalePixel(16), ScalePixel(17), ScalePixel(40), ScalePixel(40));
        SpeakerIcon.Draw(e.Graphics, new Rectangle(ScalePixel(25), ScalePixel(26), ScalePixel(22), ScalePixel(22)),
            Color.FromArgb(171, 197, 255));

        int left = ScalePixel(68);
        int width = Math.Max(0, ClientSize.Width - left - ScalePixel(15));
        const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine
            | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(e.Graphics, "Audio output", labelFont,
            new Rectangle(left, ScalePixel(13), width, ScalePixel(20)), Color.FromArgb(181, 188, 203), flags);
        TextRenderer.DrawText(e.Graphics, deviceName, deviceFont,
            new Rectangle(left, ScalePixel(33), width, ScalePixel(26)), Color.White, flags);
    }

    private int ScalePixel(int value) => (value * dpi + 48) / 96;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            dismiss.Dispose();
            labelFont.Dispose();
            deviceFont.Dispose();
        }

        base.Dispose(disposing);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetDpiForWindow(IntPtr window);
}

internal static class SpeakerIcon
{
    internal static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(Color.FromArgb(36, 52, 83));
        graphics.FillEllipse(background, 0, 0, 31, 31);
        Draw(graphics, new Rectangle(5, 5, 22, 22), Color.FromArgb(212, 226, 255));
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    internal static void Draw(Graphics graphics, Rectangle bounds, Color color)
    {
        var state = graphics.Save();
        graphics.TranslateTransform(bounds.Left, bounds.Top);
        graphics.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, new Point[] { new(2, 9), new(6, 9), new(11, 5), new(11, 19), new(6, 15), new(2, 15) });
        using var pen = new Pen(color, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawArc(pen, 7, 7, 11, 10, -48, 96);
        graphics.DrawArc(pen, 3, 3, 19, 18, -47, 94);
        graphics.Restore(state);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
