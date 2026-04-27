namespace Kilo.ECS;

/// <summary>
/// Threading mode for system execution.
/// </summary>
public enum ThreadingMode
{
    /// <summary>Automatically determine based on CPU count.</summary>
    Auto,
    /// <summary>Force single-threaded execution.</summary>
    Single,
    /// <summary>Enable multi-threaded execution.</summary>
    Multi
}
