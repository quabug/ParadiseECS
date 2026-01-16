namespace Paradise.ECS.Concurrent;

/// <summary>
/// Runtime information about a component type needed for layout calculation.
/// </summary>
/// <param name="Id">The component's unique identifier.</param>
/// <param name="Size">The size of the component in bytes.</param>
/// <param name="Alignment">The alignment requirement in bytes.</param>
public readonly record struct ComponentTypeInfo(ComponentId Id, int Size, int Alignment)
{
    /// <summary>
    /// Creates ComponentTypeInfo for a component type.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>Type info for the component.</returns>
    public static ComponentTypeInfo Create<T>() where T : unmanaged, IComponent
    {
        return new ComponentTypeInfo(T.TypeId, T.Size, T.Alignment);
    }
}
