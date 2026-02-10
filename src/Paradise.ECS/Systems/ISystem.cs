namespace Paradise.ECS;

/// <summary>
/// Base interface for all ECS systems. Provides the compile-time system ID
/// used for scheduling and dispatch. Implemented automatically by the source generator.
/// </summary>
public interface ISystem
{
    /// <summary>
    /// The unique system ID assigned at compile time by the source generator.
    /// </summary>
    static abstract int SystemId { get; }
}

/// <summary>
/// Marker interface for per-entity systems.
/// Implementing this interface on a <c>ref partial struct</c> enables source generator discovery.
/// The generator will auto-generate the constructor, SystemId, and <c>RunChunk</c> method.
/// </summary>
/// <remarks>
/// <para>
/// Fields declare component access:
/// <list type="bullet">
///   <item><c>ref T</c> where T is a [Component] — writable per-entity access</item>
///   <item><c>ref readonly T</c> where T is a [Component] — read-only per-entity access</item>
///   <item><c>ref {Prefix}Entity</c> where {Prefix} is a [Queryable] — composition access via generated Data type</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public ref partial struct GravitySystem : IEntitySystem
/// {
///     public ref Velocity Velocity;
///     public void Execute()
///     {
///         Velocity = new(Velocity.X, Velocity.Y - 9.8f);
///     }
/// }
///
/// public ref partial struct MovementSystem : IEntitySystem
/// {
///     public ref MovableEntity Movable;
///     public void Execute()
///     {
///         Movable.Position = new(Movable.Position.X + Movable.Velocity.X,
///                                Movable.Position.Y + Movable.Velocity.Y);
///     }
/// }
/// </code>
/// </example>
public interface IEntitySystem : ISystem
{
    /// <summary>
    /// Executes this system for a single entity.
    /// Called once per entity that matches the system's query.
    /// </summary>
    void Execute();
}

/// <summary>
/// Marker interface for per-chunk systems.
/// Implementing this interface on a <c>ref partial struct</c> enables source generator discovery.
/// The generator will auto-generate the constructor, SystemId, and <c>RunChunk</c> method.
/// </summary>
/// <remarks>
/// <para>
/// Fields declare component access:
/// <list type="bullet">
///   <item><c>Span&lt;T&gt;</c> where T is a [Component] — writable batch access</item>
///   <item><c>ReadOnlySpan&lt;T&gt;</c> where T is a [Component] — read-only batch access</item>
///   <item><c>ref readonly {Prefix}Chunk</c> where {Prefix} is a [Queryable] — composition access via generated ChunkData type</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public ref partial struct GravityBatchSystem : IChunkSystem
/// {
///     public Span&lt;Velocity&gt; Velocities;
///     public void ExecuteChunk()
///     {
///         for (int i = 0; i &lt; Velocities.Length; i++)
///             Velocities[i] = new(Velocities[i].X, Velocities[i].Y - 9.8f);
///     }
/// }
///
/// public ref partial struct BatchMovementSystem : IChunkSystem
/// {
///     public ref readonly MovableChunk Movable;
///     public void ExecuteChunk()
///     {
///         var positions = Movable.Positions;
///         var velocities = Movable.Velocitys;
///         for (int i = 0; i &lt; Movable.EntityCount; i++)
///             positions[i] = new(positions[i].X + velocities[i].X,
///                                positions[i].Y + velocities[i].Y);
///     }
/// }
/// </code>
/// </example>
public interface IChunkSystem : ISystem
{
    /// <summary>
    /// Executes this system for a chunk of entities.
    /// Called once per chunk that matches the system's query.
    /// </summary>
    void ExecuteChunk();
}
