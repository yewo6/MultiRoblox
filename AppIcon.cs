using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MultiRoblox;

internal static class AppIcon
{
    private static Icon? _idle;
    private static Icon? _active;

    public static Icon Create() => _idle ??= Build(active: false);
    public static Icon CreateActive() => _active ??= Build(active: true);

    private static Icon Build(bool active)
    {
        using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.Clear(Color.Transparent);

            var fill = active
                ? Color.FromArgb(40, 120, 60)
                : Color.FromArgb(28, 28, 28);
            var edge = active
                ? Color.FromArgb(160, 220, 160)
                : Color.FromArgb(180, 180, 180);

            using (var brush = new SolidBrush(fill))
                g.FillRectangle(brush, 2, 2, 28, 28);

            using (var pen = new Pen(edge, 2f))
            {
                g.DrawRectangle(pen, 2, 2, 27, 27);
                g.DrawRectangle(pen, 8, 8, 10, 10);
                g.DrawRectangle(pen, 14, 14, 10, 10);
            }
        }

        var hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
