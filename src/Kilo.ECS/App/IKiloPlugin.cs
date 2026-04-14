namespace Kilo.ECS;

/// <summary>
/// Plugin interface following Bevy's philosophy. Plugins register systems and resources.
/// </summary>
public interface IKiloPlugin
{
    /// <summary>Configure the plugin by adding systems, resources, and other plugins.</summary>
    void Build(KiloApp app);
}
