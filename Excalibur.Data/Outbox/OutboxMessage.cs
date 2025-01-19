using Excalibur.Extensions;

namespace Excalibur.Data.Outbox;

/// <summary>
///     Represents a message to be stored and dispatched via the outbox mechanism.
/// </summary>
public class OutboxMessage
{
	/// <summary>
	///     Gets or sets the unique identifier for the message.
	/// </summary>
	/// <value>
	///     A <see cref="string" /> representing the unique identifier of the message. Defaults to a new UUID string generated using <see cref="Uuid7Extensions.GenerateString" />.
	/// </value>
	public string MessageId { get; set; } = Uuid7Extensions.GenerateString();

	/// <summary>
	///     Gets or sets the body of the message.
	/// </summary>
	/// <value> An <see cref="object" /> representing the message content. </value>
	public object MessageBody { get; set; }

	/// <summary>
	///     Gets or sets the headers associated with the message.
	/// </summary>
	/// <value> A collection of key-value pairs representing metadata or additional information about the message. </value>
	public IReadOnlyDictionary<string, string>? MessageHeaders { get; set; }

	/// <summary>
	///     Gets or sets the destination of the message.
	/// </summary>
	/// <value>
	///     A <see cref="string" /> representing the intended destination of the message. This could be a queue, topic, or other messaging endpoint.
	/// </value>
	public string? Destination { get; set; }
}
