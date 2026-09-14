// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Base type for every refusal of an outbox completion: the signal that a drain operation was declined
/// because the caller can no longer prove it is entitled to decide this message's fate.
/// </summary>
/// <remarks>
/// <para>
/// A refusal is not a delivery failure, and the distinction is the reason this type exists. A caller that
/// cannot tell the two apart routes the refusal into its failure handling and marks, retries, or dead-letters
/// a message it no longer owns — an unfenced write performed by a superseded tenure, which is precisely what
/// the fence exists to prevent. Catching this base type gives a caller that property once, for every refusal,
/// including ones added later; catching each derived type separately re-opens the hole the next time the
/// family grows.
/// </para>
/// <para>
/// The correct response is always the same regardless of which derived type arrives: abort the operation for
/// that message with no further store write, and leave it as claimed. The live leader resolves it once the
/// claim ages out. Never retry with the same token — the refusal is fail-closed, not transient.
/// </para>
/// <para>
/// <b>The family is defined by that obligation, not by the mechanism that produced it.</b> Entitlement
/// can be lost in more than one way - a tenure superseded by a newer leader, or a claim that lapsed and
/// was taken by a later cycle of the SAME processor - and the caller's correct response does not vary
/// between them. The name says "fence" for historical reasons; the obligation above, not the name, is
/// the membership test for whether a new refusal belongs here.
/// </para>
/// </remarks>
public abstract class OutboxFenceRefusedException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxFenceRefusedException"/> class.
	/// </summary>
	protected OutboxFenceRefusedException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxFenceRefusedException"/> class with a message.
	/// </summary>
	/// <param name="message">The message that describes the refusal.</param>
	protected OutboxFenceRefusedException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxFenceRefusedException"/> class with a message and an
	/// inner exception.
	/// </summary>
	/// <param name="message">The message that describes the refusal.</param>
	/// <param name="innerException">The exception that caused this refusal.</param>
	protected OutboxFenceRefusedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
