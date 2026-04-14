namespace Kilo.Rendering.Driver;

public interface IShaderModule : IDisposable
{
    string EntryPoint { get; }
}
