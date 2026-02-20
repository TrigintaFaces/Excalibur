// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Exception thrown when an encryption migration operation fails.
/// </summary>
public sealed class EncryptionMigrationException : EncryptionException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionMigrationException" /> class.
	/// </summary>
	public EncryptionMigrationException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionMigrationException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The message that describes the error. </param>
	public EncryptionMigrationException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionMigrationException" /> class with a specified error message and a
	/// reference to the inner exception.
	/// </summary>
	/// <param name="message"> The message that describes the error. </param>
	/// <param name="innerException"> The exception that is the cause of the current exception. </param>
	public EncryptionMigrationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the migration ID associated with this exception.
	/// </summary>
	public string? MigrationId { get; init; }

	/// <summary>
	/// Gets or sets the item ID that caused the failure.
	/// </summary>
	public string? ItemId { get; init; }

	/// <summary>
	/// Gets or sets the number of items that were successfully migrated before the failure.
	/// </summary>
	public int? SucceededCount { get; init; }

	/// <summary>
	/// Gets or sets the number of items that failed.
	/// </summary>
	public int? FailedCount { get; init; }
}
