// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Globalization;

namespace Excalibur.EventSourcing;

/// <summary>
/// Represents the result of appending events to the store.
/// </summary>
public sealed class AppendResult
{
	private readonly bool _isConcurrencyConflict;

	private AppendResult(
		bool success,
		long? nextExpectedVersion,
		long? firstEventPosition,
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
	/// Gets the version the aggregate's stream is at after this append — the value a subsequent append
	/// must pass as its expected version — or <see langword="null"/> when this result cannot state one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Versions are zero-based, so appending <c>N</c> events to a new stream reports <c>N-1</c> here: this
	/// is the version of the last event written, not the version the next event will receive. An append
	/// that carried no events reports back the version it was given.
	/// </para>
	/// <para>
	/// A failed append reports <see langword="null"/> rather than a number, because it has no version to
	/// report and <c>-1</c> is not free to borrow as a sentinel: under this interface's version base
	/// <c>-1</c> is the ordinary value meaning <em>this stream does not exist</em>. Reporting it after a
	/// failure would hand a caller a number asserting the opposite of the truth, which they could pass
	/// straight back as an expected version and create a stream that already holds events.
	/// </para>
	/// <para>
	/// A concurrency conflict is the one failure that <em>can</em> state a version: the store read the
	/// stream's actual version in order to detect the conflict, so it reports that measured value here —
	/// including a genuine <c>-1</c> when the conflict is that the stream does not exist at all.
	/// </para>
	/// </remarks>
	/// <value>The stream's current version after the append, or <see langword="null"/> when unavailable.</value>
	public long? NextExpectedVersion { get; }

	/// <summary>
	/// Gets the global stream position of the first event that was appended, or <see langword="null"/>
	/// when the store does not support a global ordering.
	/// </summary>
	/// <remarks>
	/// A store that maintains a monotonic, store-wide sequence across all streams returns a real position
	/// here. Stores that only track per-stream versions (or track no global ordering at all) return
	/// <see langword="null"/> rather than fabricating a value. The property is also <see langword="null"/>
	/// for failed appends and for successful appends that contained no events.
	/// </remarks>
	/// <value>
	/// The monotonic global position of the first appended event, or <see langword="null"/> when global
	/// ordering is unsupported for the originating provider.
	/// </value>
	public long? FirstEventPosition { get; }

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
	/// <param name="firstEventPosition">
	/// The global stream position of the first appended event, or <see langword="null"/> when the store
	/// does not support a global ordering.
	/// </param>
	/// <returns>A successful append result.</returns>
	public static AppendResult CreateSuccess(long nextExpectedVersion, long? firstEventPosition) =>
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
			firstEventPosition: null,
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
	/// <returns>A failed append result, reporting no version.</returns>
	/// <remarks>
	/// Nothing was appended, so the result states no version: <see cref="NextExpectedVersion"/> is
	/// <see langword="null"/>. Use <see cref="CreateConcurrencyConflict"/> for the one failure that has a
	/// version to report.
	/// </remarks>
	public static AppendResult CreateFailure(string errorMessage) =>
		new(success: false, nextExpectedVersion: null, firstEventPosition: null, errorMessage);
}
