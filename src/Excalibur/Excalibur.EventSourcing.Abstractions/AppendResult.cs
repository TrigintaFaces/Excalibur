// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Globalization;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Represents the result of appending events to the store.
/// </summary>
public sealed class AppendResult
{
	private readonly bool _isConcurrencyConflict;

	private AppendResult(
		bool success,
		long nextExpectedVersion,
		long firstEventPosition,
		string? errorMessage = null,
		bool isConcurrencyConflict = false)
	{
		Success = success;
		NextExpectedVersion = nextExpectedVersion;
		FirstEventPosition = firstEventPosition;
		ErrorMessage = errorMessage;
		_isConcurrencyConflict = isConcurrencyConflict;
	}

	/// <summary>
	/// Gets a value indicating whether the append operation succeeded.
	/// </summary>
	public bool Success { get; }

	/// <summary>
	/// Gets the next expected version for the aggregate after this append.
	/// </summary>
	public long NextExpectedVersion { get; }

	/// <summary>
	/// Gets the global position of the first event that was appended.
	/// </summary>
	public long FirstEventPosition { get; }

	/// <summary>
	/// Gets the error message if the operation failed.
	/// </summary>
	public string? ErrorMessage { get; }

	/// <summary>
	/// Gets a value indicating whether the failure was due to a concurrency conflict.
	/// </summary>
	public bool IsConcurrencyConflict => _isConcurrencyConflict;

	/// <summary>
	/// Creates a successful append result.
	/// </summary>
	/// <param name="nextExpectedVersion">The next expected version.</param>
	/// <param name="firstEventPosition">The position of the first appended event.</param>
	/// <returns>A successful append result.</returns>
	public static AppendResult CreateSuccess(long nextExpectedVersion, long firstEventPosition) =>
		new(success: true, nextExpectedVersion, firstEventPosition);

	/// <summary>
	/// Creates a failed append result due to version mismatch.
	/// </summary>
	/// <param name="expectedVersion">The expected version.</param>
	/// <param name="actualVersion">The actual version.</param>
	/// <returns>A failed append result indicating concurrency conflict.</returns>
	public static AppendResult CreateConcurrencyConflict(long expectedVersion, long actualVersion) =>
		new(
			success: false,
			actualVersion,
			-1,
			string.Format(
				CultureInfo.InvariantCulture,
				"Concurrency conflict: expected version {0} but current version is {1}",
				expectedVersion,
				actualVersion),
			isConcurrencyConflict: true);

	/// <summary>
	/// Creates a failed append result with custom error.
	/// </summary>
	/// <param name="errorMessage">The error message.</param>
	/// <returns>A failed append result.</returns>
	public static AppendResult CreateFailure(string errorMessage) =>
		new(success: false, -1, -1, errorMessage);
}
