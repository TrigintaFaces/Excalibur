// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a single data change captured from a Change Data Capture (CDC) source.
/// </summary>
/// <typeparam name="T">The type of the data entity that changed.</typeparam>
/// <remarks>
/// <para>
/// This record provides a provider-agnostic representation of a data change event.
/// The generic type parameter allows strongly-typed access to the before/after data.
/// </para>
/// <para>
/// <b>State Availability by Change Type:</b>
/// <list type="table">
/// <listheader>
/// <term>ChangeType</term>
/// <description>Before/After Availability</description>
/// </listheader>
/// <item>
/// <term>Insert</term>
/// <description><see cref="Before"/> is <see langword="null"/>; <see cref="After"/> contains inserted data</description>
/// </item>
/// <item>
/// <term>Update</term>
/// <description><see cref="Before"/> contains previous state (if supported); <see cref="After"/> contains new state</description>
/// </item>
/// <item>
/// <term>Delete</term>
/// <description><see cref="Before"/> contains deleted data (if supported); <see cref="After"/> is <see langword="null"/></description>
/// </item>
/// </list>
/// </para>
/// <para>
/// <b>Before-Image Support:</b>
/// Not all providers support before-images for updates and deletes:
/// <list type="bullet">
/// <item><description>SQL Server: Requires CDC "all columns" tracking mode</description></item>
/// <item><description>Postgres: Requires REPLICA IDENTITY FULL or appropriate index</description></item>
/// <item><description>MongoDB: Requires explicit pre-image recording (changeStreamPreAndPostImages)</description></item>
/// <item><description>CosmosDB: Does not support before-images</description></item>
/// <item><description>DynamoDB: Requires NEW_AND_OLD_IMAGES stream view type</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="Type">The type of change that occurred.</param>
/// <param name="Before">
/// The state of the entity before the change, or <see langword="null"/> for inserts
/// or when before-images are not available.
/// </param>
/// <param name="After">
/// The state of the entity after the change, or <see langword="null"/> for deletes.
/// </param>
/// <param name="Position">
/// The position in the CDC stream where this change occurred. Used for checkpointing
/// and resumption.
/// </param>
/// <param name="Timestamp">
/// The timestamp when this change was committed to the source database.
/// </param>
/// <example>
/// <code>
/// // Processing changes from an IChangeDataCapture source
/// await foreach (var change in cdc.GetChangesAsync(lastPosition, cancellationToken))
/// {
///     switch (change.Type)
///     {
///         case ChangeType.Insert:
///             await HandleInsertAsync(change.After!);
///             break;
///         case ChangeType.Update:
///             await HandleUpdateAsync(change.Before, change.After!);
///             break;
///         case ChangeType.Delete:
///             await HandleDeleteAsync(change.Before!);
///             break;
///     }
///
///     // Checkpoint after processing
///     await stateStore.SavePositionAsync(change.Position);
/// }
/// </code>
/// </example>
public sealed record Change<T>(
	ChangeType Type,
	T? Before,
	T? After,
	ChangePosition Position,
	DateTimeOffset Timestamp)
{
	/// <summary>
	/// Gets a value indicating whether this change includes before-image data.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if <see cref="Before"/> is not <see langword="null"/>;
	/// otherwise, <see langword="false"/>.
	/// </value>
	public bool HasBeforeImage => Before is not null;

	/// <summary>
	/// Gets a value indicating whether this change includes after-image data.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if <see cref="After"/> is not <see langword="null"/>;
	/// otherwise, <see langword="false"/>.
	/// </value>
	public bool HasAfterImage => After is not null;

	/// <summary>
	/// Gets the entity key or identifier from the available before/after data.
	/// </summary>
	/// <typeparam name="TKey">The type of the entity key.</typeparam>
	/// <param name="keySelector">A function that extracts the key from an entity.</param>
	/// <returns>
	/// The key extracted from <see cref="After"/> if available;
	/// otherwise, the key from <see cref="Before"/>.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when both <see cref="Before"/> and <see cref="After"/> are <see langword="null"/>.
	/// </exception>
	public TKey GetKey<TKey>(Func<T, TKey> keySelector)
	{
		ArgumentNullException.ThrowIfNull(keySelector);

		if (After is not null)
		{
			return keySelector(After);
		}

		if (Before is not null)
		{
			return keySelector(Before);
		}

		throw new InvalidOperationException(
				ErrorMessages.CannotExtractKeyBothBeforeAndAfterNull);
	}
}
