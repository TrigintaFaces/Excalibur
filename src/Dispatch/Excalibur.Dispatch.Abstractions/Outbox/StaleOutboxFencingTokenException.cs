// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch;

/// <summary>
/// Thrown by <see cref="IFencedOutboxStore.MarkSentAsync(string, long, CancellationToken)"/> when the presented
/// fencing token is lower than the store's recorded high-water mark for the affected scope -- meaning the
/// caller's leadership tenure has been superseded by a newer leader and the mutation must not be applied.
/// </summary>
/// <remarks>
/// This is a fail-closed signal: the caller MUST treat this as an abort of the protected operation, not as
/// a transient error to retry with the same token.
/// </remarks>
public sealed class StaleOutboxFencingTokenException : Exception
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
