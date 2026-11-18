// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0



namespace Excalibur.Dispatch;

/// <summary>
/// Signals that a presented fencing token was lower than the store's recorded high-water mark for the
/// affected scope -- meaning the caller's leadership tenure has been superseded by a newer leader and the
/// mutation must not be applied.
/// </summary>
/// <remarks>
/// <para>
/// This is a fail-closed signal: a caller that receives it MUST treat it as an abort of the protected
/// operation, not as a transient error to retry with the same token.
/// </para>
/// <para>
/// The fenced completion contract reports a refusal in its return
/// value -- <see cref="OutboxCompletionOutcome.FenceRefused"/> -- because a void-returning mutation made
/// "refused" and "applied" the same observation and every implementation reached for a different wrong
/// encoding of the difference. A consumer-authored store MAY still refuse by throwing this, and the drain
/// still handles it; a store that does so is not following the contract, but it is not silently losing
/// messages either, which is what the return value was introduced to prevent. A learned refusal stops
/// later admissions at the captured token or any older token in the same authority domain.
/// </para>
/// <para>
/// The refused and recorded tokens on this type have no counterpart on the returned outcome. Read the
/// store's recorded mark through <see cref="IFencedOutboxStoreDiagnostics.GetFencingHighWaterAsync"/>
/// instead; that member exists for exactly this question and answers it without a refusal having to occur.
/// </para>
/// </remarks>
public sealed class StaleOutboxFencingTokenException : OutboxFenceRefusedException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="StaleOutboxFencingTokenException"/> class.
	/// </summary>
	public StaleOutboxFencingTokenException()
		: base("The presented outbox fencing token is stale and was rejected.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StaleOutboxFencingTokenException"/> class with a message.
	/// </summary>
	/// <param name="message"> The error message. </param>
	public StaleOutboxFencingTokenException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StaleOutboxFencingTokenException"/> class with a message
	/// and an inner exception.
	/// </summary>
	/// <param name="message"> The error message. </param>
	/// <param name="innerException"> The inner exception. </param>
	public StaleOutboxFencingTokenException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the fencing token that was presented and rejected, when known.
	/// </summary>
	public long? PresentedToken { get; init; }

	/// <summary>
	/// Gets the highest fencing token previously observed for the affected scope, when known.
	/// </summary>
	public long? HighWaterToken { get; init; }
}
