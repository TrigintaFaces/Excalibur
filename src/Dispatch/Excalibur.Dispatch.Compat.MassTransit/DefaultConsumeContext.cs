// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading;

namespace Excalibur.Dispatch.Compat.MassTransit;

/// <summary>
/// Default <see cref="ConsumeContext{TMessage}"/> implementation that wraps a message and cancellation
/// token when bridging an Excalibur.Dispatch event onto a migrated MassTransit-style consumer.
/// </summary>
/// <typeparam name="TMessage">The consumed message type.</typeparam>
internal sealed class DefaultConsumeContext<TMessage> : ConsumeContext<TMessage>
	where TMessage : class
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultConsumeContext{TMessage}"/> class.
	/// </summary>
	/// <param name="message">The consumed message.</param>
	/// <param name="cancellationToken">The cancellation token for the consume operation.</param>
	public DefaultConsumeContext(TMessage message, CancellationToken cancellationToken)
	{
		Message = message;
		CancellationToken = cancellationToken;
	}

	/// <inheritdoc />
	public TMessage Message { get; }

	/// <inheritdoc />
	public CancellationToken CancellationToken { get; }
}
