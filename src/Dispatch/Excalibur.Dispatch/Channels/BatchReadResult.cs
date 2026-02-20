// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Result of a batch read operation.
/// </summary>
public readonly struct BatchReadResult<T>(IReadOnlyList<T> items, bool hasItems) : IEquatable<BatchReadResult<T>>
{
	/// <summary>
	/// Gets the items in this batch read result.
	/// </summary>
	/// <value>The current <see cref="Items"/> value.</value>
	public IReadOnlyList<T> Items { get; } = items;

	/// <summary>
	/// Gets a value indicating whether this result contains items.
	/// </summary>
	/// <value>The current <see cref="HasItems"/> value.</value>
	public bool HasItems { get; } = hasItems;

	/// <summary>
	/// Gets the count of items in this result.
	/// </summary>
	/// <value>The current <see cref="Count"/> value.</value>
	public int Count => Items?.Count ?? 0;

	/// <summary>
	/// Determines whether two results are equal.
	/// </summary>
	public static bool operator ==(BatchReadResult<T> left, BatchReadResult<T> right) => left.Equals(right);

	/// <summary>
	/// Determines whether two results are not equal.
	/// </summary>
	public static bool operator !=(BatchReadResult<T> left, BatchReadResult<T> right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the specified result is equal to the current result.
	/// </summary>
	public bool Equals(BatchReadResult<T> other) => ReferenceEquals(Items, other.Items) && HasItems == other.HasItems;

	/// <summary>
	/// Determines whether the specified object is equal to the current result.
	/// </summary>
	public override bool Equals(object? obj) => obj is BatchReadResult<T> other && Equals(other);

	/// <summary>
	/// Returns the hash code for this result.
	/// </summary>
	public override int GetHashCode() => HashCode.Combine(Items, HasItems);
}
