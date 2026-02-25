// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents the result of a batch key rotation check operation.
/// </summary>
public sealed record KeyRotationBatchResult
{
	/// <summary>
	/// Gets the total number of keys checked.
	/// </summary>
	public int KeysChecked { get; init; }

	/// <summary>
	/// Gets the number of keys that were due for rotation.
	/// </summary>
	public int KeysDueForRotation { get; init; }

	/// <summary>
	/// Gets the number of keys successfully rotated.
	/// </summary>
	public int KeysRotated { get; init; }

	/// <summary>
	/// Gets the number of keys that failed to rotate.
	/// </summary>
	public int KeysFailed { get; init; }

	/// <summary>
	/// Gets the individual rotation results for keys that were rotated or attempted.
	/// </summary>
	public IReadOnlyList<KeyRotationResult> Results { get; init; } = [];

	/// <summary>
	/// Gets the errors encountered during the batch operation.
	/// </summary>
	public IReadOnlyList<KeyRotationError> Errors { get; init; } = [];

	/// <summary>
	/// Gets the timestamp when the batch operation started.
	/// </summary>
	public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the timestamp when the batch operation completed.
	/// </summary>
	public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the duration of the batch operation.
	/// </summary>
	public TimeSpan Duration => CompletedAt - StartedAt;

	/// <summary>
	/// Gets a value indicating whether all due rotations succeeded.
	/// </summary>
	public bool AllSucceeded => KeysFailed == 0;

	/// <summary>
	/// Creates an empty result for when no keys were found.
	/// </summary>
	public static KeyRotationBatchResult Empty() => new()
	{
		KeysChecked = 0,
		KeysDueForRotation = 0,
		KeysRotated = 0,
		KeysFailed = 0
	};
}

/// <summary>
/// Represents an error that occurred during key rotation.
/// </summary>
public sealed record KeyRotationError
{
	/// <summary>
	/// Gets the key identifier that failed.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the error message.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// Gets the exception that caused the error, if any.
	/// </summary>
	public Exception? Exception { get; init; }

	/// <summary>
	/// Gets the timestamp when the error occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
