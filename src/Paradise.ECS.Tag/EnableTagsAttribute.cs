namespace Paradise.ECS;

/// <summary>
/// Enables the tag feature for this assembly and changes the World alias to TaggedWorld.
/// </summary>
/// <remarks>
/// <para>
/// When this attribute is applied at the assembly level, the source generator will:
/// </para>
/// <list type="bullet">
/// <item>Generate TaggedWorld as the World alias instead of the base World class</item>
/// <item>Generate ChunkTagRegistry alias for per-chunk tag mask tracking</item>
/// </list>
/// <para>
/// This requires that:
/// </para>
/// <list type="bullet">
/// <item>At least one type is marked with [Tag] attribute</item>
/// <item>An EntityTags component is defined with [Component] attribute</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// [assembly: EnableTags]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class EnableTagsAttribute : Attribute { }
