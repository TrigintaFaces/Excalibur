// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Thrown by <see cref="ILegalHoldStore.SaveHoldAsync"/> when — and only when — a hold with the same
/// <see cref="LegalHold.HoldId"/> is already stored.
/// </summary>
/// <remarks>
/// <para>
/// This type exists so that "the hold is already on file" is <b>distinguishable by the caller</b> from
/// every other reason a save can fail. Without it the duplicate signal and the fail-closed tenancy signal
/// are the same type: <see cref="Excalibur.Dispatch.TenantRequiredException"/> also derives from
/// <see cref="InvalidOperationException"/> and is raised, before any row is examined, when multi-tenancy
/// is active and no ambient tenant was resolved. A caller writing
/// <c>catch (InvalidOperationException) { /* already filed */ }</c> therefore treats a hold that was
/// <em>never written</em> as one that was — and a legal hold is a control that blocks erasure, so one
/// silently dropped does not fail safe: the next erasure runs and reports success.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException"/> because a duplicate insert genuinely is an
/// invalid operation on the store's current state, and because that keeps an existing broad
/// <c>catch (InvalidOperationException)</c> working. A caller that needs the duplicate signal specifically
/// must catch <b>this</b> type.
/// </para>
/// <para>
/// <b>The identifier namespace this reports on is estate-wide, and that is deliberate.</b> Every shipped
/// relational provider declares the hold identifier as the table's sole primary key, so a hold identifier
/// is unique across the whole estate rather than within one tenant. Under multi-tenancy that produces a
/// pair of answers a caller can read as contradictory unless it is stated: this exception can be raised
/// for a hold that <see cref="ILegalHoldStore.GetHoldAsync"/> reports as absent, because the colliding
/// hold belongs to a tenant whose rows are — correctly — not visible. "The identifier is taken" and "you
/// cannot see it" are both true. Callers that must not disclose whether an identifier is in use should
/// allocate hold identifiers randomly rather than deriving them from a case reference or any other value
/// another tenant could guess.
/// </para>
/// <para>
/// Implementations raise this rather than the underlying database provider's own duplicate-key exception,
/// so a caller can handle the condition without referencing that provider or knowing its error codes. The
/// provider's exception is preserved as <see cref="Exception.InnerException"/> so the underlying cause
/// stays diagnosable.
/// </para>
/// </remarks>
public sealed class DuplicateLegalHoldException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateLegalHoldException"/> class.
	/// </summary>
	public DuplicateLegalHoldException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateLegalHoldException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public DuplicateLegalHoldException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateLegalHoldException"/> class with a message
	/// and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The underlying store failure that reported the duplicate, when there was one.</param>
	public DuplicateLegalHoldException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the identifier of the hold that is already stored.
	/// </summary>
	/// <value>
	/// The <see cref="LegalHold.HoldId"/> that was re-filed, or <see langword="null"/> when the exception
	/// was constructed without one.
	/// </value>
	public Guid? HoldId { get; init; }

	/// <summary>
	/// Creates an exception for a hold identifier that is already stored.
	/// </summary>
	/// <param name="holdId">The hold identifier that was re-filed.</param>
	/// <param name="innerException">The underlying store failure that reported the duplicate, if any.</param>
	/// <returns>The exception to throw.</returns>
	public static DuplicateLegalHoldException ForHoldId(Guid holdId, Exception? innerException = null) =>
		new($"Legal hold {holdId} already exists", innerException)
		{
			HoldId = holdId,
		};
}
