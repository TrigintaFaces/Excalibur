using Excalibur.Domain;

namespace Excalibur.Application;

/// <summary>
///     Defines a contract for publishing messages within a system.
/// </summary>
public interface IMessagePublisher
{
	/// <summary>
	///     Publishes a message asynchronously to the appropriate message-handling system.
	/// </summary>
	/// <typeparam name="TMessage"> The type of the message to be published. </typeparam>
	/// <param name="message"> The message to be published. </param>
	/// <param name="context"> The activity context containing metadata such as correlation ID, tenant ID, or other relevant information. </param>
	/// <returns> A task that represents the asynchronous operation of publishing the message. </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if <paramref name="message" /> or <paramref name="context" /> is <c> null </c>.
	/// </exception>
	public Task PublishAsync<TMessage>(TMessage message, IActivityContext context);
}
