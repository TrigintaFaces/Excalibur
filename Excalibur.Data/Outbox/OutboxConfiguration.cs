namespace Excalibur.Data.Outbox;

/// <summary>
///     Represents the configuration settings for the outbox system.
/// </summary>
public class OutboxConfiguration
{
	/// <summary>
	///     Gets or sets the name of the table used for storing outbox messages.
	/// </summary>
	/// <value> A <see cref="string" /> representing the name of the outbox table. </value>
	public string TableName { get; set; }

	/// <summary>
	///     Gets or sets the name of the dead-letter table for storing messages that failed to dispatch.
	/// </summary>
	/// <value> A <see cref="string" /> representing the name of the dead-letter table. </value>
	public string DeadLetterTableName { get; set; }

	/// <summary>
	///     Gets or sets the timeout, in milliseconds, for the dispatcher to reserve messages.
	/// </summary>
	/// <value> An <see cref="int" /> representing the timeout in milliseconds. </value>
	public int DispatcherTimeoutMilliseconds { get; set; }

	/// <summary>
	///     Gets or sets the maximum number of retries allowed for processing a message before it is moved to the dead-letter table.
	/// </summary>
	/// <value> An <see cref="int" /> representing the maximum number of retries. </value>
	public int MaxRetries { get; set; }
}
