// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Represents a set of label values for a metric.
/// </summary>
public readonly struct LabelSet : IEquatable<LabelSet>
{
	private readonly string[] _values;
	private readonly int _hashCode;

	/// <summary>
	/// Initializes a new instance of the <see cref="LabelSet"/> struct.
	/// Initializes a new instance of the <see cref="LabelSet" /> class with the specified label values.
	/// </summary>
	/// <param name="values"> The label values to include in this set. </param>
	public LabelSet(params string[]? values)
	{
		_values = values ?? [];
		_hashCode = ComputeHashCode(_values);
	}

	/// <summary>
	/// Gets the label values as a read-only span.
	/// </summary>
	/// <value>The current <see cref="Values"/> value.</value>
	public ReadOnlySpan<string> Values => _values;

	/// <summary>
	/// Determines whether two label sets are equal.
	/// </summary>
	/// <param name="left"> The first label set to compare. </param>
	/// <param name="right"> The second label set to compare. </param>
	/// <returns> true if the label sets are equal; otherwise, false. </returns>
	public static bool operator ==(LabelSet left, LabelSet right) => left.Equals(right);

	/// <summary>
	/// Determines whether two label sets are not equal.
	/// </summary>
	/// <param name="left"> The first label set to compare. </param>
	/// <param name="right"> The second label set to compare. </param>
	/// <returns> true if the label sets are not equal; otherwise, false. </returns>
	public static bool operator !=(LabelSet left, LabelSet right) => !left.Equals(right);

	/// <summary>
	/// Determines whether this label set is equal to another label set.
	/// </summary>
	/// <param name="other"> The other label set to compare with. </param>
	/// <returns> true if the label sets are equal; otherwise, false. </returns>
	public bool Equals(LabelSet other)
	{
		if (_hashCode != other._hashCode)
		{
			return false;
		}

		if (_values.Length != other._values.Length)
		{
			return false;
		}

		return !_values.Where((t, i) => !string.Equals(t, other._values[i], StringComparison.Ordinal)).Any();
	}

	/// <summary>
	/// Determines whether this label set is equal to the specified object.
	/// </summary>
	/// <param name="obj"> The object to compare with this label set. </param>
	/// <returns> true if the object is a LabelSet and is equal to this label set; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is LabelSet other && Equals(other);

	/// <summary>
	/// Returns the hash code for this label set.
	/// </summary>
	/// <returns> A hash code for the current label set. </returns>
	public override int GetHashCode() => _hashCode;

	private static int ComputeHashCode(string[] values)
	{
		unchecked
		{
			return values.Aggregate(17, static (current, value) => (current * 31) + (value?.GetHashCode(StringComparison.Ordinal) ?? 0));
		}
	}
}
