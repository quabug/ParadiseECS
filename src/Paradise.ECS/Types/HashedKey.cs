using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A dictionary key wrapper that caches the hash code for any equatable type.
/// Use this when storing values in dictionaries or hash sets where GetHashCode()
/// is expensive but Equals() is fast, enabling O(1) lookup without recomputing
/// the hash on every access.
/// </summary>
/// <typeparam name="T">The type of value to wrap. Must implement <see cref="IEquatable{T}"/>.</typeparam>
public readonly struct HashedKey<T> : IEquatable<HashedKey<T>> where T : IEquatable<T>
{
    private readonly T _value;
    private readonly int _cachedHash;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    /// <summary>
    /// Creates a new hashed key from the specified value.
    /// The hash code is computed once and cached.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HashedKey(T value)
    {
        _value = value;
        _cachedHash = EqualityComparer<T>.Default.GetHashCode(value);
    }

    /// <summary>
    /// Determines whether this key equals another key by comparing the underlying values.
    /// </summary>
    /// <param name="other">The other key to compare.</param>
    /// <returns><c>true</c> if the values are equal; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(HashedKey<T> other)
    {
        if (_value is null)
            return other._value is null;
        return _value.Equals(other._value);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is HashedKey<T> other && Equals(other);

    /// <summary>
    /// Returns the cached hash code. This is O(1) since the hash is computed once at construction.
    /// </summary>
    /// <returns>The cached hash code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => _cachedHash;

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(HashedKey<T> left, HashedKey<T> right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(HashedKey<T> left, HashedKey<T> right) => !left.Equals(right);

    /// <summary>
    /// Explicit conversion from <typeparamref name="T"/> to <see cref="HashedKey{T}"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static explicit operator HashedKey<T>(T value) => new(value);

    /// <summary>
    /// Implicit conversion from <see cref="HashedKey{T}"/> to <typeparamref name="T"/>.
    /// </summary>
    /// <param name="key">The key to convert.</param>
    public static implicit operator T(HashedKey<T> key) => key._value;

    /// <inheritdoc/>
    public override string ToString() => $"HashedKey({_value})";
}
