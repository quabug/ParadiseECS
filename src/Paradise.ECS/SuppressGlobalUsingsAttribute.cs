namespace Paradise.ECS;

/// <summary>
/// Suppresses the generation of all global using aliases by the source generator.
/// Use this attribute when your project has multiple ECS libraries that define types
/// with the same names (e.g., World, Query, ComponentMask).
/// </summary>
/// <remarks>
/// When this attribute is applied at the assembly level, the source generator will NOT emit any global using aliases:
/// <code>
/// global using World = ...;
/// global using Query = ...;
/// global using SharedArchetypeMetadata = ...;
/// global using ArchetypeRegistry = ...;
/// global using ComponentMask = ...;
/// global using ComponentMaskBits = ...;
/// global using QueryBuilder = ...;
/// </code>
/// Instead, you must use the fully qualified types or define your own local using aliases.
/// </remarks>
/// <example>
/// <code>
/// [assembly: SuppressGlobalUsings]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class SuppressGlobalUsingsAttribute : Attribute { }
