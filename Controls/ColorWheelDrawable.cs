using Microsoft.Maui.Graphics;

namespace FinancialTracker.Controls;

public sealed class ColorWheelDrawable : IDrawable
{
    private float hue = 248f;
    private float saturation = 0.70f;
    private float brightness = 0.89f;

    public float Brightness
    {
        get => brightness;
        set => brightness = Math.Clamp(value, 0f, 1f);
    }

    public string SelectedHex => ToHex(HsvToColor(hue, saturation, brightness));

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var radius = (MathF.Min(dirtyRect.Width, dirtyRect.Height) / 2f) - 5f;
        if (radius <= 0)
        {
            return;
        }

        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;
        const float cellSize = 3f;
        var clipPath = new PathF();
        clipPath.AppendCircle(centerX, centerY, radius);

        canvas.SaveState();
        canvas.ClipPath(clipPath);

        for (var y = -radius; y < radius; y += cellSize)
        {
            for (var x = -radius; x < radius; x += cellSize)
            {
                var sampleX = x + (cellSize / 2f);
                var sampleY = y + (cellSize / 2f);
                var distance = MathF.Sqrt((sampleX * sampleX) + (sampleY * sampleY));
                if (distance > radius)
                {
                    continue;
                }

                var sampleHue = MathF.Atan2(sampleY, sampleX) * 180f / MathF.PI;
                if (sampleHue < 0)
                {
                    sampleHue += 360f;
                }

                canvas.FillColor = HsvToColor(sampleHue, distance / radius, brightness);
                canvas.FillRectangle(centerX + x, centerY + y, cellSize + 0.6f, cellSize + 0.6f);
            }
        }

        canvas.RestoreState();
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(centerX, centerY, radius);

        var angle = hue * MathF.PI / 180f;
        var selectorX = centerX + (MathF.Cos(angle) * saturation * radius);
        var selectorY = centerY + (MathF.Sin(angle) * saturation * radius);

        canvas.FillColor = HsvToColor(hue, saturation, brightness);
        canvas.FillCircle(selectorX, selectorY, 8f);
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 4f;
        canvas.DrawCircle(selectorX, selectorY, 8f);
        canvas.StrokeColor = Colors.Black;
        canvas.StrokeSize = 1.5f;
        canvas.DrawCircle(selectorX, selectorY, 10f);
    }

    public bool Select(PointF point, float width, float height)
    {
        var radius = (MathF.Min(width, height) / 2f) - 5f;
        if (radius <= 0)
        {
            return false;
        }

        var deltaX = point.X - (width / 2f);
        var deltaY = point.Y - (height / 2f);
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        saturation = Math.Clamp(distance / radius, 0f, 1f);
        hue = MathF.Atan2(deltaY, deltaX) * 180f / MathF.PI;
        if (hue < 0)
        {
            hue += 360f;
        }

        return true;
    }

    public bool SetColor(string? hex)
    {
        if (!TryParseHex(hex, out var red, out var green, out var blue))
        {
            return false;
        }

        RgbToHsv(red, green, blue, out hue, out saturation, out brightness);
        brightness = Math.Clamp(brightness, 0f, 1f);
        return true;
    }

    private static Color HsvToColor(float hue, float saturation, float value)
    {
        var chroma = value * saturation;
        var hueSection = hue / 60f;
        var intermediate = chroma * (1f - MathF.Abs((hueSection % 2f) - 1f));
        var offset = value - chroma;

        var (red, green, blue) = hueSection switch
        {
            < 1f => (chroma, intermediate, 0f),
            < 2f => (intermediate, chroma, 0f),
            < 3f => (0f, chroma, intermediate),
            < 4f => (0f, intermediate, chroma),
            < 5f => (intermediate, 0f, chroma),
            _ => (chroma, 0f, intermediate)
        };

        return Color.FromRgb(
            (byte)Math.Clamp(MathF.Round((red + offset) * 255f), 0f, 255f),
            (byte)Math.Clamp(MathF.Round((green + offset) * 255f), 0f, 255f),
            (byte)Math.Clamp(MathF.Round((blue + offset) * 255f), 0f, 255f));
    }

    private static void RgbToHsv(
        byte red,
        byte green,
        byte blue,
        out float outputHue,
        out float outputSaturation,
        out float outputValue)
    {
        var redValue = red / 255f;
        var greenValue = green / 255f;
        var blueValue = blue / 255f;
        var maximum = MathF.Max(redValue, MathF.Max(greenValue, blueValue));
        var minimum = MathF.Min(redValue, MathF.Min(greenValue, blueValue));
        var delta = maximum - minimum;

        outputHue = delta switch
        {
            0f => 0f,
            _ when maximum == redValue => 60f * (((greenValue - blueValue) / delta) % 6f),
            _ when maximum == greenValue => 60f * (((blueValue - redValue) / delta) + 2f),
            _ => 60f * (((redValue - greenValue) / delta) + 4f)
        };

        if (outputHue < 0)
        {
            outputHue += 360f;
        }

        outputSaturation = maximum == 0f ? 0f : delta / maximum;
        outputValue = maximum;
    }

    private static bool TryParseHex(string? hex, out byte red, out byte green, out byte blue)
    {
        var value = hex?.Trim().TrimStart('#') ?? string.Empty;
        if (value.Length == 3 && value.All(Uri.IsHexDigit))
        {
            value = string.Concat(value.Select(character => $"{character}{character}"));
        }

        if (value.Length == 6 && value.All(Uri.IsHexDigit))
        {
            red = Convert.ToByte(value[..2], 16);
            green = Convert.ToByte(value.Substring(2, 2), 16);
            blue = Convert.ToByte(value.Substring(4, 2), 16);
            return true;
        }

        red = green = blue = 0;
        return false;
    }

    private static string ToHex(Color color) =>
        $"#{(byte)MathF.Round(color.Red * 255f):X2}" +
        $"{(byte)MathF.Round(color.Green * 255f):X2}" +
        $"{(byte)MathF.Round(color.Blue * 255f):X2}";
}
