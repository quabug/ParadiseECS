namespace Paradise.ECS;

/// <summary>
/// Specifies the namespace for the generated ComponentRegistry class.
/// Apply this attribute at the assembly level to control where ComponentRegistry is generated.
/// </summary>
/// <example>
/// <code>
/// [assembly: ComponentRegistryNamespace("MyGame.ECS")]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ComponentRegistryNamespaceAttribute : Attribute
{
    /// <summary>
    /// Gets the namespace for the generated ComponentRegistry.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Initializes a new instance with the specified namespace.
    /// </summary>
    /// <param name="namespace">The namespace for the generated ComponentRegistry class.</param>
    public ComponentRegistryNamespaceAttribute(string @namespace)
    {
        Namespace = @namespace;
    }
}
