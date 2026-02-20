using Excalibur.Data.ElasticSearch;
namespace Excalibur.Data.Tests.ElasticSearch;

/// <summary>
/// Unit tests for ElasticsearchDeadLetterOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ElasticsearchDeadLetterOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new ElasticsearchDeadLetterOptions();

		// Assert
		options.DeadLetterIndexPrefix.ShouldBe("dead-letters");
		options.MaxRetryCount.ShouldBe(3);
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void DeadLetterIndexPrefix_CanBeCustomized()
	{
		// Arrange
		var options = new ElasticsearchDeadLetterOptions();

		// Act
		options.DeadLetterIndexPrefix = "dlq-messages";

		// Assert
		options.DeadLetterIndexPrefix.ShouldBe("dlq-messages");
	}

	[Fact]
	public void MaxRetryCount_CanBeCustomized()
	{
		// Arrange
		var options = new ElasticsearchDeadLetterOptions();

		// Act
		options.MaxRetryCount = 5;

		// Assert
		options.MaxRetryCount.ShouldBe(5);
	}

	[Fact]
	public void RetentionPeriod_CanBeCustomized()
	{
		// Arrange
		var options = new ElasticsearchDeadLetterOptions();

		// Act
		options.RetentionPeriod = TimeSpan.FromDays(90);

		// Assert
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(90));
	}

	[Fact]
	public void MaxRetryCount_CanBeSetToZero()
	{
		// Arrange
		var options = new ElasticsearchDeadLetterOptions();

		// Act
		options.MaxRetryCount = 0;

		// Assert
		options.MaxRetryCount.ShouldBe(0);
	}
}
