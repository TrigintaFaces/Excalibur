// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Globalization;

namespace Excalibur.EventSourcing;

/// <summary>
/// Represents the result of appending events to the store.
/// </summary>
public sealed class AppendResult
{
	private AppendResult(
		AppendOutcome outcome,
		long? nextExpectedVersion,
		long? firstEventPosition,
		string? errorMessage = null)
	{
		Outcome = outcome;
		NextExpectedVersion = nextExpectedVersion;
		FirstEventPosition = firstEventPosition;
		ErrorMessage = errorMessage;
	}

	/// <summary>
	/// Gets what the append actually did.
	/// </summary>
	/// <remarks>
	/// <see cref="Success"/> and <see cref="IsConcurrencyConflict"/> are derived from this, so a caller
	/// that only asks "did it work" needs no change. Read this instead when the difference between an
	/// append written by <em>this</em> call and one recognised as already durable matters — see
	/// <see cref="AppendOutcome.AlreadyCommitted"/>.
	/// </remarks>
	/// <value>The single outcome that holds for this result.</value>
	public AppendOutcome Outcome { get; }

	/// <summary>
	/// Gets a value indicating whether the append operation succeeded.
	/// </summary>
	/// <remarks>
	/// True for <see cref="AppendOutcome.Committed"/> and for
	/// <see cref="AppendOutcome.AlreadyCommitted"/> alike: in both the events the caller asked to append
	/// are durable, which is what this property has always meant.
	/// </remarks>
	public bool Success => Outcome is AppendOutcome.Committed or AppendOutcome.AlreadyCommitted;

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
	/// A concurrency conflict is the one failure that <em>can</em> state a version, and it states one only
	/// when it measured one: where the store read the stream's actual version in order to detect the
	/// conflict it reports that measured value here — including a genuine <c>-1</c> when the conflict is
	/// that the stream does not exist at all. Where the store detected the conflict by another route and
	/// the version read did not succeed, it reports <see langword="null"/>.
	/// </para>
	/// <para>
	/// Every value this property carries is one the store <em>measured</em>. It is never the caller's own
	/// expected version echoed back, never a bound, and never derived from anything but a read. That is
	/// what makes the two meanings of <c>-1</c> separable: a <c>-1</c> here is always "the stream
	/// measurably does not exist", and "not measured" is <see langword="null"/> instead. A caller may pass
	/// any non-null value straight back as the next expected version; on <see langword="null"/> it must
	/// reload.
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
	public bool IsConcurrencyConflict => Outcome is AppendOutcome.ConcurrencyConflict;


	/// <summary>
	/// Creates a successful append result for events this call wrote.
	/// </summary>
	/// <param name="nextExpectedVersion">The next expected version.</param>
	/// <param name="firstEventPosition">
	/// The global stream position of the first appended event, or <see langword="null"/> when the store
	/// does not support a global ordering.
	/// </param>
	/// <returns>A successful append result.</returns>
	public static AppendResult CreateSuccess(long nextExpectedVersion, long? firstEventPosition) =>
		new(AppendOutcome.Committed, nextExpectedVersion, firstEventPosition);

	/// <summary>
	/// Creates a successful append result for events that were <em>already</em> durably present, which the
	/// store recognised as its own by identity rather than writing again.
	/// </summary>
	/// <param name="nextExpectedVersion">The version the recognised events reached.</param>
	/// <param name="firstEventPosition">
	/// The global stream position of the first recognised event, or <see langword="null"/> when the store
	/// does not support a global ordering or cannot state one.
	/// </param>
	/// <returns>A successful append result carrying <see cref="AppendOutcome.AlreadyCommitted"/>.</returns>
	/// <remarks>
	/// Every store that answers a committed-append identity probe reports through here rather than through
	/// <see cref="CreateSuccess"/>. The two are indistinguishable to a caller reading only
	/// <see cref="Success"/>, and that is deliberate — the distinction is for the caller that needs it.
	/// </remarks>
	public static AppendResult CreateAlreadyCommitted(long nextExpectedVersion, long? firstEventPosition) =>
		new(AppendOutcome.AlreadyCommitted, nextExpectedVersion, firstEventPosition);

	/// <summary>
	/// Creates a failed append result due to version mismatch.
	/// </summary>
	/// <param name="expectedVersion">The version the append required the stream to be at.</param>
	/// <param name="actualVersion">
	/// The stream's current version <em>as the store read it</em>, or <see langword="null"/> when the store
	/// detected the conflict without being able to read one.
	/// </param>
	/// <returns>A failed append result indicating concurrency conflict.</returns>
	/// <remarks>
	/// <para>
	/// <paramref name="actualVersion"/> takes only what a read returned. Pass the read's own result
	/// straight through — never <paramref name="expectedVersion"/>, never a bound, never a value derived
	/// from anything but a read. The parameter is <see cref="long"/>? precisely so that a store which did
	/// not measure has no way to <em>type</em> a number here: the violation is inexpressible rather than
	/// merely discouraged.
	/// </para>
	/// <para>
	/// Echoing the caller's own <paramref name="expectedVersion"/> back is the specific error this
	/// signature exists to prevent, and it is not merely unmeasured but known-false: the state that reaches
	/// an unmeasured conflict is one where a stream-key unique violation was raised, which proves another
	/// writer holds the row at <c>expectedVersion + 1</c>. That makes <paramref name="expectedVersion"/>
	/// the one value the stream provably is <em>not</em>, and it would additionally render
	/// <see cref="ErrorMessage"/> as "expected version N but current version is N" — a conflict asserting
	/// that nothing moved.
	/// </para>
	/// <para>
	/// When <paramref name="actualVersion"/> is <see langword="null"/> the message states that the version
	/// could not be determined rather than naming one, so the string a consumer reads never claims a
	/// measurement that was not taken.
	/// </para>
	/// </remarks>
	public static AppendResult CreateConcurrencyConflict(long expectedVersion, long? actualVersion) =>
		new(
			AppendOutcome.ConcurrencyConflict,
			actualVersion,
			firstEventPosition: null,
			actualVersion is { } measured
				? string.Format(
					CultureInfo.InvariantCulture,
					"Concurrency conflict: expected version {0} but current version is {1}",
					expectedVersion,
					measured)
				: string.Format(
					CultureInfo.InvariantCulture,
					"Concurrency conflict: expected version {0}; the store could not determine the stream's "
						+ "current version",
					expectedVersion));

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
		new(AppendOutcome.Failed, nextExpectedVersion: null, firstEventPosition: null, errorMessage);
}
