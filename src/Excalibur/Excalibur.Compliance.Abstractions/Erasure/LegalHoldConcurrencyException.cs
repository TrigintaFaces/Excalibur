// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

namespace Excalibur.Compliance;

/// <summary>
/// Thrown by <see cref="ILegalHoldStore.UpdateHoldAsync"/> when — and only when — the stored hold is
/// present and visible, but carries a <see cref="LegalHold.Version"/> other than the one the caller read.
/// The write is <b>not</b> applied.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is an exception rather than a return value.</b> A legal hold is the authority that stops an
/// erasure destroying records a data controller is legally obliged to keep. The update path is a
/// read-modify-write: a caller reads a hold, decides from what it read, and writes the whole record back.
/// Without a version check that decision is applied over whatever the record became in the meantime, so a
/// hold extended between the read and the write is released anyway and the next erasure for that subject
/// proceeds. The loss is not of the hold — it is of the records the hold protected, and it is irreversible.
/// </para>
/// <para>
/// That failure is <b>silent</b>, which is what sets the reporting bar here. The released record is
/// well-formed and carries a plausible reason, so an auditor inspecting it afterwards sees a legitimate
/// release; the extension simply is not there, and nothing downstream learns that the erasure which
/// followed was unlawful. A conflict signalled as a <see langword="bool"/> or an enum can be discarded at
/// the call site — both shipped call sites did exactly that, writing <c>_ = await …</c> — and a discarded
/// conflict reproduces the silence one layer up. An exception cannot be dropped by accident.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException"/> for the same reason
/// <see cref="DuplicateLegalHoldException"/> does: writing over a record that moved genuinely is an invalid
/// operation on the store's current state. A caller that needs the conflict signal specifically must catch
/// <b>this</b> type — and it is distinct from <see cref="DuplicateLegalHoldException"/> because the remedies
/// differ. A duplicate means the identifier is taken; a conflict means the caller's premise expired, so the
/// correct response is to <b>re-read and re-decide</b>, never to re-apply the same write with a fresh
/// version. Re-applying it reintroduces the lost update with extra steps.
/// </para>
/// <para>
/// A hold that is absent, or that belongs to another tenant, is <b>not</b> reported here:
/// <see cref="ILegalHoldStore.UpdateHoldAsync"/> returns <see langword="false"/> for those, so "nothing to
/// write to" stays distinguishable from "the record moved under you".
/// </para>
/// </remarks>
public sealed class LegalHoldConcurrencyException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LegalHoldConcurrencyException"/> class.
	/// </summary>
	public LegalHoldConcurrencyException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LegalHoldConcurrencyException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public LegalHoldConcurrencyException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LegalHoldConcurrencyException"/> class with a message
	/// and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The underlying store failure, when there was one.</param>
	public LegalHoldConcurrencyException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the identifier of the hold whose update was refused.
	/// </summary>
	/// <value>
	/// The <see cref="LegalHold.HoldId"/> that was written to, or <see langword="null"/> when the exception
	/// was constructed without one.
	/// </value>
	public Guid? HoldId { get; init; }

	/// <summary>
	/// Gets the <see cref="LegalHold.Version"/> the caller read and expected to still be stored.
	/// </summary>
	public int? ExpectedVersion { get; init; }

	/// <summary>
	/// Gets the <see cref="LegalHold.Version"/> actually stored when the write was attempted.
	/// </summary>
	/// <remarks>
	/// Re-read the hold before deciding anything from this number. It says the record moved; it does not
	/// say what it moved to, and the difference is the whole point — a hold whose expiry was extended must
	/// stay active.
	/// </remarks>
	public int? ActualVersion { get; init; }

	/// <summary>
	/// Creates an exception for a hold whose stored version no longer matches the one the caller read.
	/// </summary>
	/// <param name="holdId">The hold identifier that was written to.</param>
	/// <param name="expectedVersion">The version the caller read.</param>
	/// <param name="actualVersion">The version actually stored.</param>
	/// <param name="innerException">The underlying store failure, if any.</param>
	/// <returns>The exception to throw.</returns>
	public static LegalHoldConcurrencyException ForHold(
		Guid holdId,
		int expectedVersion,
		int actualVersion,
		Exception? innerException = null) =>
		new(
			string.Format(
				CultureInfo.InvariantCulture,
				"Legal hold {0} was modified concurrently: expected version {1}, stored version is {2}. "
				+ "The update was not applied. Re-read the hold and re-evaluate before writing again.",
				holdId,
				expectedVersion,
				actualVersion),
			innerException)
		{
			HoldId = holdId,
			ExpectedVersion = expectedVersion,
			ActualVersion = actualVersion,
		};
}
