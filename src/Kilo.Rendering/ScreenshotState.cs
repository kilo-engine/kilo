using Kilo.Rendering.Driver;

namespace Kilo.Rendering;

public sealed class ScreenshotState
{
    public bool Requested { get; set; }
    public bool HasPending { get; set; }
    public IBuffer? Buffer { get; set; }
    public uint AlignedBytesPerRow { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
