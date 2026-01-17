namespace Paradise.ECS;

/// <summary>
/// Marks an <see cref="IConfig"/> implementation as the default configuration for World type alias generation.
/// When applied to a type implementing <see cref="IConfig"/>, the generator emits these global aliases:
/// <code>
/// global using ComponentMask = global::Paradise.ECS.ImmutableBitSet&lt;TBits&gt;;
/// global using ChunkManager = global::Paradise.ECS.ChunkManager&lt;TConfig&gt;;
/// global using SharedArchetypeMetadata = global::Paradise.ECS.SharedArchetypeMetadata&lt;TBits, TRegistry, TConfig&gt;;
/// global using ArchetypeRegistry = global::Paradise.ECS.ArchetypeRegistry&lt;TBits, TRegistry, TConfig&gt;;
/// global using World = global::Paradise.ECS.World&lt;TBits, TRegistry, TConfig&gt;;
/// </code>
/// Where:
/// <list type="bullet">
///   <item><description>TBits is auto-determined by component count (Bit64, Bit128, etc.)</description></item>
///   <item><description>TRegistry is the generated ComponentRegistry</description></item>
///   <item><description>TConfig is the type marked with this attribute</description></item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false)]
public sealed class DefaultConfigAttribute : Attribute { }
