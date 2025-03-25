using Excalibur.Core.Exceptions;

namespace Excalibur.Data.Outbox.Exceptions;

/// <summary>
///     Represents an exception that occurs during the dispatch of an outbox message.
/// </summary>
[Serializable]
public class OutboxMessageDispatchException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="OutboxMessageDispatchException" /> class.
	/// </summary>
	/// <param name="messageId"> The identifier of the outbox message that failed to dispatch. </param>
	/// <param name="innerException"> The underlying exception that caused this error. </param>
	public OutboxMessageDispatchException(string messageId, Exception? innerException = null)
		: base(statusCode: 500, message: $"Failed to dispatch outbox message with ID: {messageId}.", innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId, nameof(messageId));

		MessageId = messageId;
	}

	/// <summary>
	///     Gets the identifier of the outbox message that caused the exception.
	/// </summary>
	public string MessageId { get; }
}
