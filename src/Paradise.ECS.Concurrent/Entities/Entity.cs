namespace Paradise.ECS.Concurrent;

/// <summary>
/// A lightweight handle to an entity in the ECS world.
/// Entities are unique identifiers that tie components together.
/// </summary>
/// <param name="Id">The unique identifier of the entity.</param>
/// <param name="Version">Incrementing version for destroyed entity detection.</param>
public readonly record struct Entity(int Id, uint Version)
{
    /// <summary>
    /// The Invalid entity handle. Equal to <c>default(Entity)</c>.
    /// </summary>
    public static readonly Entity Invalid = default;

    /// <summary>
    /// Gets whether this entity handle is valid (not the Invalid entity or default).
    /// Valid entities have Version >= 1; Version 0 indicates an invalid entity.
    /// Note: Does not check if the entity is still alive in the manager.
    /// </summary>
    public bool IsValid => Version > 0;

    public override string ToString() =>
        IsValid ? $"Entity(Id: {Id}, Ver: {Version})" : "Entity(Invalid)";
}
