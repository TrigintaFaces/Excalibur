using Excalibur.Data.DataProcessing;
namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for DataProcessingConfiguration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DataProcessingConfigurationShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var config = new DataProcessingConfiguration();

		// Assert
		config.TableName.ShouldBe("DataProcessor.DataTaskRequests");
		config.DispatcherTimeoutMilliseconds.ShouldBe(60000);
		config.MaxAttempts.ShouldBe(3);
		config.QueueSize.ShouldBe(5000);
		config.ProducerBatchSize.ShouldBe(100);
		config.ConsumerBatchSize.ShouldBe(10);
	}

	[Fact]
	public void TableName_CanBeCustomized()
	{
		// Arrange & Act
		var config = new DataProcessingConfiguration
		{
			TableName = "Custom.TaskTable"
		};

		// Assert
		config.TableName.ShouldBe("Custom.TaskTable");
	}

	[Fact]
	public void DispatcherTimeoutMilliseconds_CanBeCustomized()
	{
		// Arrange & Act
		var config = new DataProcessingConfiguration
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
		var config = new DataProcessingConfiguration
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
		var config = new DataProcessingConfiguration
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
		var config = new DataProcessingConfiguration
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
		var config = new DataProcessingConfiguration
		{
			ConsumerBatchSize = 200
		};

		// Assert
		config.ConsumerBatchSize.ShouldBe(200);
	}
}
