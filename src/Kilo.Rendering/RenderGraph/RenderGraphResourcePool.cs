using Kilo.Rendering.Driver;

namespace Kilo.Rendering.RenderGraph;

public sealed class RenderGraphResourcePool
{
    private readonly List<(TextureDescriptor Descriptor, ITexture Texture)> _texturePool = [];
    private readonly List<(BufferDescriptor Descriptor, IBuffer Buffer)> _bufferPool = [];

    public ITexture GetTexture(IRenderDriver driver, TextureDescriptor descriptor)
    {
        for (int i = 0; i < _texturePool.Count; i++)
        {
            if (_texturePool[i].Descriptor.Equals(descriptor))
            {
                var tex = _texturePool[i].Texture;
                _texturePool.RemoveAt(i);
                return tex;
            }
        }
        return driver.CreateTexture(descriptor);
    }

    public IBuffer GetBuffer(IRenderDriver driver, BufferDescriptor descriptor)
    {
        for (int i = 0; i < _bufferPool.Count; i++)
        {
            if (_bufferPool[i].Descriptor.Equals(descriptor))
            {
                var buf = _bufferPool[i].Buffer;
                _bufferPool.RemoveAt(i);
                return buf;
            }
        }
        return driver.CreateBuffer(descriptor);
    }

    public void ReturnTexture(ITexture texture, TextureDescriptor descriptor)
    {
        _texturePool.Add((descriptor, texture));
    }

    public void ReturnBuffer(IBuffer buffer, BufferDescriptor descriptor)
    {
        _bufferPool.Add((descriptor, buffer));
    }

    public void InvalidateForSize(int width, int height)
    {
        for (int i = _texturePool.Count - 1; i >= 0; i--)
        {
            var d = _texturePool[i].Descriptor;
            if (d.Width != width || d.Height != height)
            {
                _texturePool[i].Texture.Dispose();
                _texturePool.RemoveAt(i);
            }
        }
    }

    public void Clear()
    {
        foreach (var t in _texturePool)
            t.Texture.Dispose();
        foreach (var b in _bufferPool)
            b.Buffer.Dispose();
        _texturePool.Clear();
        _bufferPool.Clear();
    }
}
