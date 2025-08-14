namespace Excalibur.Data.Outbox;

/// <summary>
///     Distributes messages stored in the outbox. Can be implemented for various message brokers such as MediatR, NServiceBus, or MassTransit.
/// </summary>
public interface IOutboxMessageDispatcher
{
	/// <summary>
	///     Dispatches a single outbox message.
	/// </summary>
	/// <param name="message"> The outbox message to dispatch. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	public Task DispatchAsync(OutboxMessage message);
}
