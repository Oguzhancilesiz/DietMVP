using Microsoft.Maui.Graphics;

namespace DietMVP;

public sealed class WaterRingDrawable : IDrawable
{
    public double Progress { get; set; } = 0; // 0..1
    public Color TrackColor { get; set; } = Color.FromArgb("#E5E7EB");
    public Color ProgressColor { get; set; } = Color.FromArgb("#FFFFFF"); // gradient üstünde beyaz daha net
    public float StrokeWidth { get; set; } = 8f;

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.SaveState();

        var centerX = rect.Center.X;
        var centerY = rect.Center.Y;
        var r = (float)(Math.Min(rect.Width, rect.Height) / 2f) - StrokeWidth / 2f;

        canvas.StrokeSize = StrokeWidth;
        canvas.StrokeLineCap = LineCap.Round;

        // Track
        canvas.StrokeColor = TrackColor;
        canvas.DrawCircle(centerX, centerY, r);

        // Progress arc (start at -90°, clockwise)
        var sweep = (float)(360 * Math.Clamp(Progress, 0, 1));
        if (sweep > 0.1f)
        {
            canvas.StrokeColor = ProgressColor;
            canvas.DrawArc(centerX - r, centerY - r, r * 2, r * 2, -90, sweep, true, false);
        }

        canvas.RestoreState();
    }
}
