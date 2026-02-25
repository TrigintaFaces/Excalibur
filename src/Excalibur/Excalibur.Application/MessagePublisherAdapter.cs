// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain;

namespace Excalibur.Application;

/// <summary>
/// Adapter that bridges between the legacy Excalibur.Application.IMessagePublisher and the new Excalibur.Dispatch.Abstractions.IMessagePublisher.
/// </summary>
/// <remarks>
/// This adapter provides backward compatibility by converting IActivityContext to IMessageContext and delegating to the new Dispatch
/// publisher interface.
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="MessagePublisherAdapter" /> class. </remarks>
/// <param name="publisher"> The new dispatch publisher to delegate to. </param>
/// <exception cref="ArgumentNullException"> Thrown when <paramref name="publisher" /> is null. </exception>
// R0.8: Type or member is obsolete
#pragma warning disable CS0618

internal sealed class MessagePublisherAdapter(IMessagePublisher publisher) : IMessagePublisher
#pragma warning restore CS0618 // Type or member is obsolete
{
	private readonly IMessagePublisher _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));

	/// <inheritdoc />
	public async Task PublishAsync<TMessage>(TMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		await _publisher.PublishAsync(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		await _publisher.PublishAsync(message, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Publishes a message asynchronously using an activity context (backward compatibility method).
	/// </summary>
	/// <typeparam name="TMessage"> The type of the message to be published. </typeparam>
	/// <param name="message"> The message to be published. </param>
	/// <param name="context"> The activity context containing metadata. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	public async Task PublishAsync<TMessage>(TMessage message, IActivityContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		// Create a compatible message context from the activity context
		var messageContext = new ActivityContextAdapter(context);

		await _publisher.PublishAsync(message, messageContext, cancellationToken).ConfigureAwait(false);
	}
}
