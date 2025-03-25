namespace Excalibur.Data.Outbox;

/// <summary>
///     Represents a record in the outbox, used to store messages for reliable delivery to external systems.
/// </summary>
public class OutboxRecord
{
	/// <summary>
	///     Gets or sets the unique identifier for the outbox record.
	/// </summary>
	/// <value> A <see cref="Guid" /> representing the unique identifier for this record. </value>
	public Guid OutboxId { get; set; }

	/// <summary>
	///     Gets or sets the timestamp when the outbox record was created.
	/// </summary>
	/// <value> A <see cref="DateTime" /> representing the creation time of the record, in UTC. </value>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	///     Gets or sets the serialized event data associated with the record.
	/// </summary>
	/// <value> A <see cref="string" /> containing the serialized data of the events or messages to be dispatched. </value>
	public string EventData { get; set; }

	/// <summary>
	///     Gets or sets the identifier of the dispatcher currently processing this record.
	/// </summary>
	/// <value>
	///     A <see cref="string" /> representing the unique identifier of the dispatcher handling this record. This value can be <c> null
	///     </c> if the record is not being processed.
	/// </value>
	public string DispatcherId { get; set; }

	/// <summary>
	///     Gets or sets the timeout period for the dispatcher currently processing this record.
	/// </summary>
	/// <value>
	///     A <see cref="DateTime?" /> indicating the expiration time for the dispatcher lock on this record. If the timeout is reached, the
	///     record may be processed by another dispatcher.
	/// </value>
	public DateTime? DispatcherTimeout { get; set; }

	/// <summary>
	///     Gets or sets the number of attempts made to dispatch this record.
	/// </summary>
	/// <value> An <see cref="int" /> representing the total number of dispatch attempts for this record. </value>
	public int Attempts { get; set; }
}
