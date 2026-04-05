using Excalibur.Data.DataProcessing;
namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for DataProcessingOptions.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class DataProcessingOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var config = new DataProcessingOptions();

		// Assert
		config.SchemaName.ShouldBe("DataProcessor");
		config.TableName.ShouldBe("DataTaskRequests");
		config.QualifiedTableName.ShouldBe("[DataProcessor].[DataTaskRequests]");
		config.DispatcherTimeoutMilliseconds.ShouldBe(60000);
		config.MaxAttempts.ShouldBe(3);
		config.QueueSize.ShouldBe(5000);
		config.ProducerBatchSize.ShouldBe(100);
		config.ConsumerBatchSize.ShouldBe(10);
	}

	[Fact]
	public void SchemaName_CanBeCustomized()
	{
		// Arrange & Act
		var config = new DataProcessingOptions
		{
			SchemaName = "custom"
		};

		// Assert
		config.SchemaName.ShouldBe("custom");
		config.QualifiedTableName.ShouldBe("[custom].[DataTaskRequests]");
	}

	[Fact]
	public void TableName_CanBeCustomized()
	{
		// Arrange & Act
		var config = new DataProcessingOptions
		{
			TableName = "TaskTable"
		};

		// Assert
		config.TableName.ShouldBe("TaskTable");
		config.QualifiedTableName.ShouldBe("[DataProcessor].[TaskTable]");
	}

	[Fact]
	public void DispatcherTimeoutMilliseconds_CanBeCustomized()
	{
		// Arrange & Act
		var config = new DataProcessingOptions
		{
			DispatcherTimeoutMilliseconds = 10000
		};

		// Assert
		config.DispatcherTimeoutMilliseconds.ShouldBe(10000);
	}

	[Fact]
	public void MaxAttempts_CanBeCustomized()
	{
		// Arrange & Act
		var config = new DataProcessingOptions
		{
			MaxAttempts = 5
		};

		// Assert
		config.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void QueueSize_CanBeCustomized()
	{
		// Arrange & Act
		var config = new DataProcessingOptions
		{
			QueueSize = 1000
		};

		// Assert
		config.QueueSize.ShouldBe(1000);
	}

	[Fact]
	public void ProducerBatchSize_CanBeCustomized()
	{
		// Arrange & Act
		var config = new DataProcessingOptions
		{
			ProducerBatchSize = 100
		};

		// Assert
		config.ProducerBatchSize.ShouldBe(100);
	}

	[Fact]
	public void ConsumerBatchSize_CanBeCustomized()
	{
		// Arrange & Act
		var config = new DataProcessingOptions
		{
			ConsumerBatchSize = 200
		};

		// Assert
		config.ConsumerBatchSize.ShouldBe(200);
	}
}
