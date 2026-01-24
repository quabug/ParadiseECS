using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Describes which archetypes a query matches using component mask constraints.
/// Immutable after construction.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <param name="All">Components that must all be present (AND constraint).</param>
/// <param name="None">Components that must not be present (NOT constraint).</param>
/// <param name="Any">At least one of these components must be present (OR constraint). If empty, this constraint is ignored.</param>
public readonly record struct ImmutableQueryDescription<TMask>(TMask All, TMask None, TMask Any)
    where TMask : unmanaged, IBitSet<TMask>
{
    /// <summary>
    /// Creates a query description that matches all archetypes containing all specified components.
    /// </summary>
    /// <param name="all">Components that must all be present.</param>
    public ImmutableQueryDescription(TMask all)
        : this(all, TMask.Empty, TMask.Empty)
    {
    }

    /// <summary>
    /// Gets an empty query description that matches all archetypes.
    /// </summary>
    public static ImmutableQueryDescription<TMask> Empty => default;

    /// <summary>
    /// Checks if an archetype matches this query's constraints.
    /// </summary>
    /// <param name="archetypeMask">The archetype's component mask.</param>
    /// <returns>True if the archetype matches all constraints.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches(in TMask archetypeMask)
    {
        // Must contain all required components
        if (!archetypeMask.ContainsAll(All))
            return false;

        // Must not contain any excluded components
        if (!archetypeMask.ContainsNone(None))
            return false;

        // If Any constraint exists, must contain at least one
        if (!Any.IsEmpty && !archetypeMask.ContainsAny(Any))
            return false;

        return true;
    }
}
