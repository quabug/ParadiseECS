namespace Paradise.ECS;

/// <summary>
/// Interface for ECS tag types.
/// Use <see cref="TagAttribute"/> on partial structs to implement this automatically.
/// </summary>
/// <remarks>
/// <para>
/// Tags are marker types that indicate entity states or categories without storing data.
/// Unlike components, tags are stored in a per-entity bitmask within chunks, enabling
/// O(1) add/remove operations without archetype changes.
/// </para>
/// <para>
/// Tags are assigned sequential IDs (0, 1, 2...) based on alphabetical ordering of their
/// fully qualified type names, separate from component IDs.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Tag]
/// public partial struct IsPlayer;
///
/// [Tag("12345678-1234-1234-1234-123456789012")]
/// public partial struct IsEnemy;
/// </code>
/// </example>
public interface ITag
{
    /// <summary>
    /// The unique tag type ID assigned at compile time.
    /// </summary>
    static abstract TagId TagId { get; }

    /// <summary>
    /// The stable GUID for this tag type, or <see cref="System.Guid.Empty"/> if not specified.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="TagId"/> which changes based on alphabetical ordering,
    /// this GUID provides stable identification across compilations when specified
    /// via <see cref="TagAttribute"/>.
    /// </remarks>
    static abstract Guid Guid { get; }
}
