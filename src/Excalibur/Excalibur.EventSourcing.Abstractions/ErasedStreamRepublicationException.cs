// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// The exception thrown when a save is recognised as an already-committed retry whose events are no longer
/// retrievable, so the repository refuses to republish the caller's live in-memory payloads.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape it closes.</b> An append commits, its acknowledgement is lost, the events are erased, and
/// the caller — still holding the aggregate it built the payloads from — retries the save. The store
/// recognises its own rows by identity and answers
/// <see cref="AppendOutcome.AlreadyCommitted"/>, because the append genuinely did commit. What has stopped
/// being true is that present implies retrievable. Continuing from there would stage those payloads to the
/// outbox and hand them to inline projections, publishing data that was destroyed on purpose — after the
/// destruction had already been certified, and with nothing downstream ever learning.
/// </para>
/// <para>
/// <b>It carries no identifiers by design.</b> The type is the whole signal: a caller that needs to know
/// catches it. The message names no aggregate, no subject and no payload, so that neither a surfaced
/// exception message nor a log line built from one can disclose which subject was involved.
/// </para>
/// <para>
/// <b>What the caller should do.</b> Discard the in-memory aggregate and reload it. Nothing was staged and
/// nothing was notified, so there is nothing to compensate; the append itself remains durable and erased.
/// Retrying the same in-memory instance will throw again, which is the intended behaviour.
/// </para>
/// </remarks>
public sealed class ErasedStreamRepublicationException : InvalidOperationException
{
	private const string DefaultMessage =
		"A previously committed append was recognised on retry, but its events are no longer retrievable. "
		+ "Nothing was staged to the outbox and nothing was notified. Reload the aggregate rather than "
		+ "retrying the in-memory instance.";

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasedStreamRepublicationException"/> class with the
	/// defined, identifier-free message.
	/// </summary>
	public ErasedStreamRepublicationException()
		: base(DefaultMessage)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasedStreamRepublicationException"/> class.
	/// </summary>
	/// <param name="message">
	/// The message. Callers within the framework use the parameterless constructor; this overload exists
	/// for the exception contract and MUST NOT be passed an aggregate identifier, a subject identifier or
	/// any payload.
	/// </param>
	public ErasedStreamRepublicationException(string? message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasedStreamRepublicationException"/> class.
	/// </summary>
	/// <param name="message">
	/// The message. MUST NOT be passed an aggregate identifier, a subject identifier or any payload.
	/// </param>
	/// <param name="innerException">The inner exception.</param>
	public ErasedStreamRepublicationException(string? message, Exception? innerException)
		: base(message, innerException)
	{
	}
}
