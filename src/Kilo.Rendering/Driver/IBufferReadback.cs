namespace Kilo.Rendering.Driver;

/// <summary>
/// Reads GPU buffer data back to CPU synchronously.
/// </summary>
public interface IBufferReadback
{
    byte[] ReadBufferSync(IBuffer buffer, nuint offset, nuint size);
}
