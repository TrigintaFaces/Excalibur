using Excalibur.Data.Outbox;

using Shouldly;

namespace Excalibur.Tests.Unit.Data.Outbox;

public class OutboxConfigurationShould
{
	[Fact]
	public void InitializeWithDefaultValues()
	{
		// Act
		var configuration = new OutboxConfiguration();

		// Assert
		configuration.TableName.ShouldBe(OutboxDefaults.OutboxDefaultTableName);
		configuration.DeadLetterTableName.ShouldBe(OutboxDefaults.OutboxDefaultDeadLetterTableName);
		configuration.MaxAttempts.ShouldBe(OutboxDefaults.OutboxDefaultMaxAttempts);
		configuration.DispatcherTimeoutMilliseconds.ShouldBe(OutboxDefaults.OutboxDefaultDispatcherTimeout);
		configuration.QueueSize.ShouldBe(OutboxDefaults.OutboxDefaultQueueSize);
		configuration.ProducerBatchSize.ShouldBe(OutboxDefaults.OutboxDefaultProducerBatchSize);
		configuration.ConsumerBatchSize.ShouldBe(OutboxDefaults.OutboxDefaultConsumerBatchSize);
	}

	[Fact]
	public void AllowCustomization()
	{
		// Act - Create with custom properties using object initializer
		var configuration = new OutboxConfiguration
		{
			TableName = "CustomOutbox",
			DeadLetterTableName = "CustomDeadLetter",
			MaxAttempts = 5,
			DispatcherTimeoutMilliseconds = 30000,
			QueueSize = 500,
			ProducerBatchSize = 25,
			ConsumerBatchSize = 5
		};

		// Assert
		configuration.TableName.ShouldBe("CustomOutbox");
		configuration.DeadLetterTableName.ShouldBe("CustomDeadLetter");
		configuration.MaxAttempts.ShouldBe(5);
		configuration.DispatcherTimeoutMilliseconds.ShouldBe(30000);
		configuration.QueueSize.ShouldBe(500);
		configuration.ProducerBatchSize.ShouldBe(25);
		configuration.ConsumerBatchSize.ShouldBe(5);
	}
}
