using Excalibur.Data.Outbox.Exceptions;

using MediatR;

namespace Excalibur.Data.Outbox;

/// <summary>
///     Implements an outbox message dispatcher using MediatR for publishing messages.
/// </summary>
public class MediatorOutboxMessageDispatcher : IOutboxMessageDispatcher
{
	private readonly IPublisher _mediator;

	/// <summary>
	///     Initializes a new instance of the <see cref="MediatorOutboxMessageDispatcher" /> class.
	/// </summary>
	/// <param name="mediator"> The MediatR publisher used to dispatch messages. </param>
	public MediatorOutboxMessageDispatcher(IPublisher mediator)
	{
		_mediator = mediator;
	}

	/// <inheritdoc />
	public async Task DispatchAsync(OutboxMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (message.MessageBody is not INotification notification)
		{
			throw new InvalidOperationException("MessageBody must be of type INotification");
		}

		try
		{
			await _mediator.Publish(notification).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new OutboxMessageDispatchException(message.MessageId, ex);
		}
	}
}
