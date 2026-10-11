using C8E.Core;

namespace C8E.Graphics;

// Short phosphor persistence hides transient XOR erase/redraw states.
// The CPU framebuffer and collision semantics remain unchanged.
public sealed class Chip8Display
{
    public const double PixelHoldSeconds = 1.0 / 30;
    private readonly double[] litUntil = new double[Chip8.Width * Chip8.Height];
    private readonly bool[] pixels = new bool[Chip8.Width * Chip8.Height];

    public void Reset()
    {
        Array.Clear(litUntil);
        Array.Clear(pixels);
    }

    public void Observe(ReadOnlySpan<bool> source, double time)
    {
        if (source.Length != pixels.Length) throw new ArgumentException("Display must contain 2048 pixels.", nameof(source));
        for (int i = 0; i < source.Length; i++)
            if (source[i]) litUntil[i] = time + PixelHoldSeconds;
    }

    public ReadOnlyMemory<bool> Snapshot(double time)
    {
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = time < litUntil[i];
        return pixels;
    }
}
