namespace Kilo.Input.Actions;

/// <summary>
/// The value type produced by an input action.
/// </summary>
public enum ActionType
{
    /// <summary>Boolean — jump, interact, toggle.</summary>
    Button,
    /// <summary>Single float — throttle, zoom, trigger pressure.</summary>
    Axis1D,
    /// <summary>2D vector — movement, aiming.</summary>
    Axis2D,
}
