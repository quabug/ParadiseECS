namespace Paradise.ECS;

/// <summary>
/// Interface implemented by generated system code.
/// Provides static methods for system execution and metadata access.
/// </summary>
/// <typeparam name="TMask">The component mask type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public interface ISystem<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    /// <summary>
    /// Gets the unique system identifier assigned at compile time.
    /// </summary>
    static abstract int SystemId { get; }

    /// <summary>
    /// Gets the human-readable system name for debugging.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Executes this system on all matching entities in the world.
    /// </summary>
    /// <typeparam name="TWorld">The world type implementing IWorld.</typeparam>
    /// <param name="world">The world to execute on.</param>
    static abstract void Run<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>;
}
