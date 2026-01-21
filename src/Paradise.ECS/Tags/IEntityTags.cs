namespace Paradise.ECS;

/// <summary>
/// Interface for EntityTags component to access the tag mask.
/// </summary>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public interface IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    /// <summary>
    /// Gets or sets the tag mask for this entity.
    /// </summary>
    TTagMask Mask { get; set; }
}
