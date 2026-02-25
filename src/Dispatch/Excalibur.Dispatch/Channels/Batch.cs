// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Represents a batch of items.
/// </summary>
/// <typeparam name="T"> The item type contained in the batch. </typeparam>
public readonly struct Batch<T>(IReadOnlyList<T> items) : IEquatable<Batch<T>>
{
	/// <summary>
	/// Gets the items in this batch.
	/// </summary>
	/// <value>
	/// The items in this batch.
	/// </value>
	public IReadOnlyList<T> Items { get; } = items ?? throw new ArgumentNullException(nameof(items));

	/// <summary>
	/// Gets the timestamp when this batch was created.
	/// </summary>
	/// <value>The current <see cref="Timestamp"/> value.</value>
	public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the count of items in this batch.
	/// </summary>
	/// <value>The current <see cref="Count"/> value.</value>
	public int Count => Items?.Count ?? 0;

	/// <summary>
	/// Determines whether two batches are equal.
	/// </summary>
	public static bool operator ==(Batch<T> left, Batch<T> right) => left.Equals(right);

	/// <summary>
	/// Determines whether two batches are not equal.
	/// </summary>
	public static bool operator !=(Batch<T> left, Batch<T> right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the specified batch is equal to the current batch.
	/// </summary>
	public bool Equals(Batch<T> other) => ReferenceEquals(Items, other.Items) && Timestamp.Equals(other.Timestamp);

	/// <summary>
	/// Determines whether the specified object is equal to the current batch.
	/// </summary>
	public override bool Equals(object? obj) => obj is Batch<T> other && Equals(other);

	/// <summary>
	/// Returns the hash code for this batch.
	/// </summary>
	public override int GetHashCode() => HashCode.Combine(Items, Timestamp);
}
