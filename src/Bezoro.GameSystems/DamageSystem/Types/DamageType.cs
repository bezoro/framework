using System;

namespace Bezoro.GameSystems.DamageSystem.Types;

/// <summary>
///     Represents a damage type identifier (physical, fire, custom, and so on).
/// </summary>
public readonly struct DamageType : IEquatable<DamageType>
{
	/// <summary>
	///     Common fire type.
	/// </summary>
	public static readonly DamageType Fire = new("fire");

	/// <summary>
	///     Common ice type.
	/// </summary>
	public static readonly DamageType Ice = new("ice");

	/// <summary>
	///     Common lightning type.
	/// </summary>
	public static readonly DamageType Lightning = new("lightning");

	/// <summary>
	///     Common magic type.
	/// </summary>
	public static readonly DamageType Magic = new("magic");

	/// <summary>
	///     Common physical type.
	/// </summary>
	public static readonly DamageType Physical = new("physical");

	/// <summary>
	///     Common poison type.
	/// </summary>
	public static readonly DamageType Poison = new("poison");

	/// <summary>
	///     A type used for damage that should bypass mitigation rules.
	/// </summary>
	public static readonly DamageType True = new("true");

	/// <summary>
	///     A default type used when no specific type is provided.
	/// </summary>
	public static readonly DamageType Unspecified = new("unspecified");
	/// <summary>
	///     Gets the unique string identifier for this damage type.
	/// </summary>
	public readonly string Id;

	/// <summary>
	///     Creates a new damage type with the specified identifier.
	/// </summary>
	/// <param name="id">Unique identifier for the damage type.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="id" /> is null or whitespace.</exception>
	public DamageType(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new ArgumentException("Damage type id cannot be null or whitespace.", nameof(id));

		Id = id;
	}

	public static bool operator ==(DamageType left, DamageType right) => left.Equals(right);

	public static bool operator !=(DamageType left, DamageType right) => !left.Equals(right);

	#region Equality

	/// <inheritdoc />
	public bool Equals(DamageType other) => string.Equals(Id, other.Id, StringComparison.Ordinal);

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is DamageType other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() => Id is null ? 0 : StringComparer.Ordinal.GetHashCode(Id);

	#endregion

	/// <inheritdoc />
	public override string ToString() => Id ?? string.Empty;
}
