using Excalibur.Domain;

namespace Excalibur.A3.Audit;

/// <summary>
///     Provides functionality to publish audit messages to an external system.
/// </summary>
public interface IAuditMessagePublisher
{
	/// <summary>
	///     Publishes an audit message to an external system asynchronously.
	/// </summary>
	/// <typeparam name="TMessage"> The type of the message to publish. </typeparam>
	/// <param name="message"> The audit message to publish. </param>
	/// <param name="context"> The activity context associated with the message. </param>
	public Task PublishAsync<TMessage>(TMessage message, IActivityContext context);
}
