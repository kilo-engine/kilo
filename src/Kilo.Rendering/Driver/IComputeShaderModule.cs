namespace Kilo.Rendering.Driver;

public interface IComputeShaderModule : IDisposable
{
    string EntryPoint { get; }
}
