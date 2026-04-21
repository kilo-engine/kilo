using System.Numerics;
using Kilo.Input.Actions;

namespace Kilo.Input.Contexts;

/// <summary>
/// Manages a stack of InputMaps with priority-based evaluation.
/// Provides a Stride-style query API for game code.
/// Inspired by Unreal's EnhancedInputLocalPlayerSubsystem.
/// </summary>
public sealed class InputMapStack
{
    private readonly Dictionary<string, InputMap> _maps = new();
    private readonly HashSet<string> _enabled = new();
    private readonly Dictionary<string, InputAction> _actions = new();
    private readonly HashSet<string> _consumed = new();

    /// <summary>Register an InputMap. Does not activate it.</summary>
    public void Register(InputMap map) => _maps[map.Name] = map;

    /// <summary>Enable a registered map by name.</summary>
    public void Enable(string name) => _enabled.Add(name);

    /// <summary>Disable a map by name.</summary>
    public void Disable(string name) => _enabled.Remove(name);

    /// <summary>Get a registered map by name (for rebinding).</summary>
    public InputMap? GetMap(string name) => _maps.GetValueOrDefault(name);

    /// <summary>Active maps ordered by descending priority.</summary>
    public IReadOnlyList<InputMap> ActiveMaps =>
        _maps.Values
            .Where(m => _enabled.Contains(m.Name))
            .OrderByDescending(m => m.Priority)
            .ToList()
            .AsReadOnly();

    // ── Action state management (called by InputMapSystem) ──

    /// <summary>Save previous state for all actions (call at frame start).</summary>
    public void BeginFrame()
    {
        _consumed.Clear();
        foreach (var action in _actions.Values)
            action.SavePrevious();
    }

    /// <summary>Set the current state for an action (called by InputMapSystem).</summary>
    public void UpdateActionState(string name, ActionType type, bool active, Vector2 vec2, float floatValue)
    {
        if (!_actions.TryGetValue(name, out var action))
        {
            action = new InputAction { Name = name, Type = type };
            _actions[name] = action;
        }
        action.IsActive = active;
        action.FloatValue = floatValue;
        action.Vector2Value = vec2;
    }

    /// <summary>Mark an action as consumed by a higher-priority map.</summary>
    public void ConsumeAction(string name) => _consumed.Add(name);

    /// <summary>Check if an action was consumed this frame.</summary>
    public bool IsActionConsumed(string name) => _consumed.Contains(name);

    // ── Query API (Stride-style, used by game code) ──

    public bool IsPressed(string name) => _actions.TryGetValue(name, out var a) && a.IsPressed;
    public bool JustPressed(string name) => _actions.TryGetValue(name, out var a) && a.JustPressed;
    public bool JustReleased(string name) => _actions.TryGetValue(name, out var a) && a.JustReleased;
    public float GetFloat(string name) => _actions.TryGetValue(name, out var a) ? a.FloatValue : 0f;
    public Vector2 GetVector2(string name) => _actions.TryGetValue(name, out var a) ? a.Vector2Value : Vector2.Zero;
    public InputAction? GetAction(string name) => _actions.GetValueOrDefault(name);
}
