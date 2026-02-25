// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents an item to be migrated in a batch encryption migration operation.
/// </summary>
public sealed record EncryptionMigrationItem
{
	/// <summary>
	/// Gets the unique identifier for this migration item.
	/// </summary>
	public required string ItemId { get; init; }

	/// <summary>
	/// Gets the encrypted data to migrate.
	/// </summary>
	public required EncryptedData EncryptedData { get; init; }

	/// <summary>
	/// Gets the encryption context used for the original encryption.
	/// </summary>
	public required EncryptionContext SourceContext { get; init; }

	/// <summary>
	/// Gets optional metadata associated with this item for tracking purposes.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Metadata { get; init; }

	/// <summary>
	/// Gets the priority of this item within the batch (lower values = higher priority).
	/// </summary>
	public int Priority { get; init; }
}
