using Kilo.Input.Bindings;
using Kilo.Input.Modifiers;
using Kilo.Input.Triggers;

namespace Kilo.Input.Actions;

/// <summary>
/// Internal definition of an action within an InputMap.
/// Holds the binding configuration but not runtime state.
/// </summary>
public sealed class ActionDef
{
    public required string Name { get; init; }
    public required ActionType Type { get; init; }
    public required InputBinding[] Bindings { get; init; }
    public CompositeAxis2D? Composite { get; init; }
    public IInputModifier[] Modifiers { get; init; } = [];
    public IInputTrigger Trigger { get; init; } = new PressTrigger();
}
