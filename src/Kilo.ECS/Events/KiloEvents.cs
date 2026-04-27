using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kilo.ECS;

/// <summary>
/// Typed event bus with double-buffered storage (Bevy's <c>Events&lt;T&gt;</c>).
/// Store as a resource in KiloWorld via <see cref="KiloWorld.Events{T}()"/>.
/// <para>
/// Lifecycle: send events → read in same frame → <see cref="Update"/> swaps buffers.
/// Events are readable for 2 frames then auto-dropped.
/// </para>
/// </summary>
public sealed class KiloEvents<T> where T : struct
{
    private List<T> _current = []; // readable this frame
    private List<T> _previous = []; // readable last frame (2-frame retention)

    /// <summary>Number of events sent this frame.</summary>
    public int CurrentCount => _current.Count;

    /// <summary>Send an event (Bevy's <c>EventWriter&lt;T&gt;::send()</c>). Readable in the same frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Send(T evt) => _current.Add(evt);

    /// <summary>
    /// Read events from the current frame's buffer (Bevy's <c>EventReader&lt;T&gt;</c>).
    /// Events are available for 2 frames: the frame they were sent and the next frame.
    /// </summary>
    public ReadOnlySpan<T> Read() => CollectionsMarshal.AsSpan(_current);

    /// <summary>
    /// Read events from the previous frame's buffer (for 2-frame retention).
    /// </summary>
    public ReadOnlySpan<T> ReadPrevious() => CollectionsMarshal.AsSpan(_previous);

    /// <summary>
    /// Swap buffers. Call once per frame after systems run.
    /// Current becomes previous; previous is dropped; new current starts empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update()
    {
        // Rotate: previous dropped, current → previous, new empty current
        (_previous, _current) = (_current, _previous);
        _current.Clear();
    }
}
