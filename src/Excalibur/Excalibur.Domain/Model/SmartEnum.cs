// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Domain.Model;

/// <summary>
/// Base class for creating type-safe enumeration patterns in domain models.
/// Provides richer behavior than standard C# enums by supporting display names,
/// custom logic, and polymorphism while remaining serialization-friendly via integer IDs.
/// </summary>
/// <typeparam name="T">The concrete enumeration type deriving from this class.</typeparam>
/// <remarks>
/// <para>
/// This follows the well-known "smart enum" / enumeration pattern used in DDD.
/// Each value is a singleton instance of the derived class, identified by a unique
/// <see cref="Id"/> and a human-readable <see cref="Name"/>.
/// </para>
/// <para>
/// Usage:
/// <code>
/// public sealed class OrderStatus : SmartEnum&lt;OrderStatus&gt;
/// {
///     public static readonly OrderStatus Pending = new(0, nameof(Pending));
///     public static readonly OrderStatus Processing = new(1, nameof(Processing));
///     public static readonly OrderStatus Shipped = new(2, nameof(Shipped));
///     public static readonly OrderStatus Cancelled = new(3, nameof(Cancelled));
///
///     private OrderStatus(int id, string name) : base(id, name) { }
/// }
///
/// // Then use it:
/// var status = OrderStatus.Pending;
/// var all = OrderStatus.GetAll();
/// var found = OrderStatus.FromId(1); // OrderStatus.Processing
/// var byName = OrderStatus.FromName("Shipped"); // OrderStatus.Shipped
/// </code>
/// </para>
/// </remarks>
[RequiresUnreferencedCode("SmartEnum discovery uses reflection to find static fields on the derived type.")]
public abstract class SmartEnum<T>
	where T : SmartEnum<T>
{
	private static readonly Lazy<IReadOnlyDictionary<int, T>> FieldsById = new(
		() => GetFieldValues().ToDictionary(static e => e.Id),
		LazyThreadSafetyMode.ExecutionAndPublication);

	private static readonly Lazy<IReadOnlyDictionary<string, T>> FieldsByName = new(
		() => GetFieldValues().ToDictionary(static e => e.Name, StringComparer.OrdinalIgnoreCase),
		LazyThreadSafetyMode.ExecutionAndPublication);

	/// <summary>
	/// Initializes a new instance of the <see cref="SmartEnum{T}"/> class.
	/// </summary>
	/// <param name="id">The unique integer identifier for this enumeration value.</param>
	/// <param name="name">The human-readable name for this enumeration value.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
	protected SmartEnum(int id, string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		Id = id;
		Name = name;
	}

	/// <summary>
	/// Gets the unique integer identifier for this enumeration value.
	/// </summary>
	/// <value>The integer ID used for persistence and serialization.</value>
	public int Id { get; }

	/// <summary>
	/// Gets the human-readable name for this enumeration value.
	/// </summary>
	/// <value>The display name of the enumeration value.</value>
	public string Name { get; }

	/// <summary>
	/// Gets all defined values of the enumeration type.
	/// </summary>
	/// <returns>A read-only collection of all enumeration values.</returns>
	public static IReadOnlyCollection<T> GetAll() => FieldsById.Value.Values.ToList().AsReadOnly();

	/// <summary>
	/// Gets the enumeration value with the specified <paramref name="id"/>.
	/// </summary>
	/// <param name="id">The integer identifier to look up.</param>
	/// <returns>The enumeration value matching the specified ID.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no enumeration value exists with the specified <paramref name="id"/>.
	/// </exception>
	public static T FromId(int id)
	{
		if (FieldsById.Value.TryGetValue(id, out var value))
		{
			return value;
		}

		throw new InvalidOperationException(
			$"'{id}' is not a valid ID for {typeof(T).Name}. " +
			$"Valid IDs: {string.Join(", ", FieldsById.Value.Keys.OrderBy(static k => k))}.");
	}

	/// <summary>
	/// Tries to get the enumeration value with the specified <paramref name="id"/>.
	/// </summary>
	/// <param name="id">The integer identifier to look up.</param>
	/// <param name="result">
	/// When this method returns, contains the enumeration value if found;
	/// otherwise, <see langword="null"/>.
	/// </param>
	/// <returns><see langword="true"/> if a matching value was found; otherwise, <see langword="false"/>.</returns>
	public static bool TryFromId(int id, [NotNullWhen(true)] out T? result) =>
		FieldsById.Value.TryGetValue(id, out result);

	/// <summary>
	/// Gets the enumeration value with the specified <paramref name="name"/>.
	/// The comparison is case-insensitive.
	/// </summary>
	/// <param name="name">The name to look up.</param>
	/// <returns>The enumeration value matching the specified name.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no enumeration value exists with the specified <paramref name="name"/>.
	/// </exception>
	public static T FromName(string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		if (FieldsByName.Value.TryGetValue(name, out var value))
		{
			return value;
		}

		throw new InvalidOperationException(
			$"'{name}' is not a valid name for {typeof(T).Name}. " +
			$"Valid names: {string.Join(", ", FieldsByName.Value.Keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))}.");
	}

	/// <summary>
	/// Tries to get the enumeration value with the specified <paramref name="name"/>.
	/// The comparison is case-insensitive.
	/// </summary>
	/// <param name="name">The name to look up.</param>
	/// <param name="result">
	/// When this method returns, contains the enumeration value if found;
	/// otherwise, <see langword="null"/>.
	/// </param>
	/// <returns><see langword="true"/> if a matching value was found; otherwise, <see langword="false"/>.</returns>
	public static bool TryFromName(string name, [NotNullWhen(true)] out T? result)
	{
		if (name is null)
		{
			result = default;
			return false;
		}

		return FieldsByName.Value.TryGetValue(name, out result);
	}

	/// <inheritdoc />
	public override bool Equals(object? obj) =>
		obj is SmartEnum<T> other && GetType() == other.GetType() && Id == other.Id;

	/// <inheritdoc />
	public override int GetHashCode() =>
		HashCode.Combine(GetType(), Id);

	/// <inheritdoc />
	public override string ToString() => Name;

	[RequiresUnreferencedCode("SmartEnum discovery uses reflection to find static fields on the derived type.")]
	private static IEnumerable<T> GetFieldValues() =>
		typeof(T)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
			.Where(static f => f.FieldType == typeof(T))
			.Select(static f => (T)f.GetValue(null)!)
			.Where(static v => v is not null);
}
