namespace Paradise.ECS;

/// <summary>
/// Compile-time metadata about a system's component access patterns.
/// Used by the scheduler to determine execution order and parallelization.
/// </summary>
/// <typeparam name="TMask">The component mask type.</typeparam>
public readonly record struct SystemMetadata<TMask>
    where TMask : unmanaged, IBitSet<TMask>
{
    /// <summary>
    /// Gets the unique system identifier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the system name for debugging.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the components this system reads.
    /// Includes all components from all Queryable parameters.
    /// </summary>
    public TMask ReadMask { get; init; }

    /// <summary>
    /// Gets the components this system writes.
    /// Only includes components from <c>ref</c> Queryable parameters
    /// where the component's IsReadOnly = false in the Queryable definition.
    /// </summary>
    public TMask WriteMask { get; init; }

    /// <summary>
    /// Gets the query description for matching entities.
    /// Combined from all Queryable parameters' All/None/Any masks.
    /// </summary>
    public HashedKey<ImmutableQueryDescription<TMask>> QueryDescription { get; init; }

    /// <summary>
    /// Gets the optional group ID this system belongs to. -1 if ungrouped.
    /// </summary>
    public int GroupId { get; init; }

    /// <summary>
    /// Creates system metadata.
    /// </summary>
    /// <param name="id">Unique system identifier.</param>
    /// <param name="name">System name for debugging.</param>
    /// <param name="readMask">Components this system reads.</param>
    /// <param name="writeMask">Components this system writes.</param>
    /// <param name="queryDescription">Query description for matching entities.</param>
    /// <param name="groupId">Optional group ID (-1 if ungrouped).</param>
    public SystemMetadata(
        int id,
        string name,
        TMask readMask,
        TMask writeMask,
        HashedKey<ImmutableQueryDescription<TMask>> queryDescription,
        int groupId = -1)
    {
        Id = id;
        Name = name;
        ReadMask = readMask;
        WriteMask = writeMask;
        QueryDescription = queryDescription;
        GroupId = groupId;
    }

    /// <summary>
    /// Checks if this system has a data dependency conflict with another system.
    /// Returns true if they cannot safely run in parallel.
    /// </summary>
    /// <param name="other">The other system to check against.</param>
    /// <returns>True if there is a conflict, false if they can run in parallel.</returns>
    public bool ConflictsWith(in SystemMetadata<TMask> other)
    {
        // Write-Read: this writes something other reads
        if (!WriteMask.And(other.ReadMask).IsEmpty)
            return true;

        // Read-Write: this reads something other writes
        if (!ReadMask.And(other.WriteMask).IsEmpty)
            return true;

        // Write-Write: both write same component
        if (!WriteMask.And(other.WriteMask).IsEmpty)
            return true;

        return false;
    }
}

/// <summary>
/// Represents a dependency edge in the system DAG.
/// </summary>
public readonly record struct SystemDependency
{
    /// <summary>
    /// Gets the system ID that must run first.
    /// </summary>
    public int Before { get; init; }

    /// <summary>
    /// Gets the system ID that must run after.
    /// </summary>
    public int After { get; init; }

    /// <summary>
    /// Gets the reason for the dependency.
    /// </summary>
    public DependencyReason Reason { get; init; }

    /// <summary>
    /// Creates a dependency edge.
    /// </summary>
    /// <param name="before">System that must run first.</param>
    /// <param name="after">System that must run after.</param>
    /// <param name="reason">Reason for the dependency.</param>
    public SystemDependency(int before, int after, DependencyReason reason)
    {
        Before = before;
        After = after;
        Reason = reason;
    }
}

/// <summary>
/// Reason why two systems have a dependency.
/// </summary>
public enum DependencyReason : byte
{
    /// <summary>
    /// System A writes a component that System B reads.
    /// </summary>
    WriteRead,

    /// <summary>
    /// System A reads a component that System B writes.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// Both systems write the same component.
    /// </summary>
    WriteWrite,

    /// <summary>
    /// Explicit [After] attribute on the dependent system.
    /// </summary>
    ExplicitAfter,

    /// <summary>
    /// Explicit [Before] attribute on the prerequisite system.
    /// </summary>
    ExplicitBefore,

    /// <summary>
    /// Ordering due to system group constraints.
    /// </summary>
    GroupOrder
}
