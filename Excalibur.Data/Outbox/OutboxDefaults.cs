namespace Excalibur.Data.Outbox;

public static class OutboxDefaults
{
	public const string OutboxDefaultTableName = "Outbox.Outbox";
	public const string OutboxDefaultDeadLetterTableName = "Outbox.DeadLetterOutbox";
	public const int OutboxDefaultDispatcherTimeout = 60000;
	public const int OutboxDefaultMaxAttempts = 5;
	public const int OutboxDefaultQueueSize = 500;
	public const int OutboxDefaultProducerBatchSize = 50;
	public const int OutboxDefaultConsumerBatchSize = 100;
}
