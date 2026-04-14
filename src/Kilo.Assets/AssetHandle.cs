namespace Kilo.Assets;

/// <summary>
/// A typed handle to a loaded asset. Handles are used to reference assets
/// without holding direct object references.
/// </summary>
/// <typeparam name="T">The asset type.</typeparam>
public readonly struct AssetHandle<T> where T : class
{
    /// <summary>
    /// The unique identifier for this asset.
    /// </summary>
    public readonly int Id;

    /// <summary>
    /// Creates a new asset handle with the given ID.
    /// </summary>
    /// <param name="id">The unique asset ID.</param>
    public AssetHandle(int id) => Id = id;

    /// <summary>
    /// Whether this handle refers to a valid asset (non-negative ID).
    /// </summary>
    public bool IsValid => Id >= 0;

    /// <summary>
    /// Creates an invalid asset handle.
    /// </summary>
    public static AssetHandle<T> Invalid => new(-1);

    public override int GetHashCode() => Id.GetHashCode();

    public override bool Equals(object? obj) =>
        obj is AssetHandle<T> other && Id == other.Id;

    public static bool operator ==(AssetHandle<T> left, AssetHandle<T> right) =>
        left.Id == right.Id;

    public static bool operator !=(AssetHandle<T> left, AssetHandle<T> right) =>
        left.Id != right.Id;
}
