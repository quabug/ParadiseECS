namespace Paradise.ECS;

/// <summary>
/// A lightweight handle to an entity in the ECS world.
/// Entities are unique identifiers that tie components together.
/// </summary>
/// <param name="Id">The unique identifier of the entity.</param>
/// <param name="Version">Incrementing version for destroyed entity detection.</param>
public readonly record struct Entity(uint Id, uint Version)
{
    /// <summary>
    /// The Invalid entity handle.
    /// </summary>
    public static readonly Entity Invalid = new(uint.MaxValue, 0);

    /// <summary>
    /// Gets whether this entity handle is valid (not the Invalid entity).
    /// Note: Does not check if the entity is still alive in the manager.
    /// </summary>
    public bool IsValid => Id != uint.MaxValue;

    public override string ToString() =>
        IsValid ? $"Entity(Id: {Id}, Ver: {Version})" : "Entity(Invalid)";
}
