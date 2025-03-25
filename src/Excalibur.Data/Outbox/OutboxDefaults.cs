namespace Excalibur.Data.Outbox;

public static class OutboxDefaults
{
	public const string OutboxDefaultTableName = "Outbox.Outbox";
	public const string OutboxDefaultDeadLetterTableName = "Outbox.DeadLetterOutbox";
	public const int OutboxDefaultDispatcherTimeout = 60000;
	public const int OutboxDefaultMaxAttempts = 5;
	public const int OutboxDefaultQueueSize = 5000;
	public const int OutboxDefaultProducerBatchSize = 100;
	public const int OutboxDefaultConsumerBatchSize = 10;
}
