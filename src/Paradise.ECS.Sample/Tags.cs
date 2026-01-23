namespace Paradise.ECS.Sample;

// ============================================================================
// Tag Definitions - Zero-size markers using the Tag system
// ============================================================================

/// <summary>
/// Tag for entities that are currently active/enabled.
/// </summary>
[Tag("11111111-1111-1111-1111-111111111111")]
public partial struct IsActive;

/// <summary>
/// Tag for entities that are visible/renderable.
/// </summary>
[Tag("22222222-2222-2222-2222-222222222222")]
public partial struct IsVisible;

/// <summary>
/// Tag for entities that can be damaged.
/// </summary>
[Tag("33333333-3333-3333-3333-333333333333")]
public partial struct IsDamageable;

/// <summary>
/// Tag for entities marked for destruction at end of frame.
/// </summary>
[Tag("44444444-4444-4444-4444-444444444444")]
public partial struct IsMarkedForDestroy;
