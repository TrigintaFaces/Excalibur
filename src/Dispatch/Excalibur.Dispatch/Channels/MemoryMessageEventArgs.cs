// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Event arguments for memory message events.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MemoryMessageEventArgs" /> class. </remarks>
/// <param name="envelope"> The message envelope. </param>
/// <param name="cancellationToken"> The cancellation token. </param>
public sealed class MemoryMessageEventArgs(MessageEnvelope envelope, CancellationToken cancellationToken) : EventArgs
{
	/// <summary>
	/// Gets the message envelope.
	/// </summary>
	/// <value>The current <see cref="Envelope"/> value.</value>
	public MessageEnvelope Envelope { get; } = envelope;

	/// <summary>
	/// Gets the cancellation token.
	/// </summary>
	/// <value>The current <see cref="CancellationToken"/> value.</value>
	public CancellationToken CancellationToken { get; } = cancellationToken;
}
