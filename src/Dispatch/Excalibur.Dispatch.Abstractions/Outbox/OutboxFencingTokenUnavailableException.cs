// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// Thrown when a leader gate is active but yields no fencing token, so a fenced outbox operation cannot be
/// proven to belong to the current tenure and is refused rather than performed unfenced.
/// </summary>
/// <remarks>
/// <para>
/// This is the ABSENT-token refusal, distinct from <see cref="StaleOutboxFencingTokenException"/>, which is
/// the SUPERSEDED-token refusal. They are separate types because they carry different evidence: a stale
/// refusal can name the token that overtook it, and this one has no token to compare at all. Reporting an
/// absent token as a stale one would state a high-water mark that was never read.
/// </para>
/// <para>
/// Reaching this state under an active gate means leadership was lost, since a fenced leader always holds a
/// token. Refusing is the fail-closed choice: draining without a fence is the "looks fenced but isn't" window
/// a superseded leader exploits. Callers treat it exactly as they treat any
/// <see cref="OutboxFenceRefusedException"/> — abort the message with no further store write.
/// </para>
/// </remarks>
public sealed class OutboxFencingTokenUnavailableException : OutboxFenceRefusedException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxFencingTokenUnavailableException"/> class.
	/// </summary>
	public OutboxFencingTokenUnavailableException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxFencingTokenUnavailableException"/> class with a
	/// message.
	/// </summary>
	/// <param name="message">The message that describes the refusal.</param>
	public OutboxFencingTokenUnavailableException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxFencingTokenUnavailableException"/> class with a
	/// message and an inner exception.
	/// </summary>
	/// <param name="message">The message that describes the refusal.</param>
	/// <param name="innerException">The exception that caused this refusal.</param>
	public OutboxFencingTokenUnavailableException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
