// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Model.ValueObjects;

/// <summary>
/// Base class for value objects in domain-driven design.
/// </summary>
public abstract class ValueObjectBase : IValueObject, IEquatable<ValueObjectBase>
{
	/// <summary>
	/// Determines whether two value objects are equal.
	/// </summary>
	public static bool operator ==(ValueObjectBase? left, ValueObjectBase? right)
	{
		if (ReferenceEquals(left, right))
		{
			return true;
		}

		if (left is null || right is null)
		{
			return false;
		}

		return left.Equals(right);
	}

	/// <summary>
	/// Determines whether two value objects are not equal.
	/// </summary>
	public static bool operator !=(ValueObjectBase? left, ValueObjectBase? right) => !(left == right);

	/// <inheritdoc />
	public abstract IEnumerable<object?> GetEqualityComponents();

	/// <inheritdoc />
	public override bool Equals(object? obj)
	{
		if (obj == null || obj.GetType() != GetType())
		{
			return false;
		}

		var other = (ValueObjectBase)obj;
		return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
	}

	/// <inheritdoc />
	public bool Equals(ValueObjectBase? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		return GetType() == other.GetType() && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
	}

	/// <inheritdoc />
	public override int GetHashCode()
	{
		var components = GetEqualityComponents().ToList();
		if (components.Count == 0)
		{
			return 0;
		}

		return components
			.Select(static x => x?.GetHashCode() ?? 0)
			.Aggregate(static (x, y) => x ^ y);
	}
}
