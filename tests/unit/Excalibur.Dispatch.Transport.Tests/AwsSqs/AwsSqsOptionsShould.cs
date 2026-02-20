using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// Unit tests for AwsSqsOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AwsSqsOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new AwsSqsOptions();

		// Assert
		options.MaxNumberOfMessages.ShouldBe(10);
		options.WaitTimeSeconds.ShouldBe(TimeSpan.FromSeconds(20));
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.MessageRetentionPeriod.ShouldBe(345600);
		options.UseFifoQueue.ShouldBeFalse();
		options.ContentBasedDeduplication.ShouldBeFalse();
		options.KmsDataKeyReusePeriodSeconds.ShouldBe(300);
	}

	[Fact]
	public void QueueUrl_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new AwsSqsOptions();
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");

		// Act
		options.QueueUrl = queueUrl;

		// Assert
		options.QueueUrl.ShouldBe(queueUrl);
	}

	[Fact]
	public void MaxNumberOfMessages_CanBeCustomized()
	{
		// Arrange
		var options = new AwsSqsOptions();

		// Act
		options.MaxNumberOfMessages = 5;

		// Assert
		options.MaxNumberOfMessages.ShouldBe(5);
	}

	[Fact]
	public void WaitTimeSeconds_CanBeCustomized()
	{
		// Arrange
		var options = new AwsSqsOptions();

		// Act
		options.WaitTimeSeconds = TimeSpan.FromSeconds(10);

		// Assert
		options.WaitTimeSeconds.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void VisibilityTimeout_CanBeCustomized()
	{
		// Arrange
		var options = new AwsSqsOptions();

		// Act
		options.VisibilityTimeout = TimeSpan.FromSeconds(60);

		// Assert
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void UseFifoQueue_CanBeEnabled()
	{
		// Arrange
		var options = new AwsSqsOptions();

		// Act
		options.UseFifoQueue = true;

		// Assert
		options.UseFifoQueue.ShouldBeTrue();
	}

	[Fact]
	public void ContentBasedDeduplication_CanBeEnabled()
	{
		// Arrange
		var options = new AwsSqsOptions();

		// Act
		options.ContentBasedDeduplication = true;

		// Assert
		options.ContentBasedDeduplication.ShouldBeTrue();
	}

	[Fact]
	public void EnableEncryption_CanBeEnabled()
	{
		// Arrange
		var options = new AwsSqsOptions();

		// Act
		options.EnableEncryption = true;

		// Assert
		options.EnableEncryption.ShouldBeTrue();
	}

	[Fact]
	public void KmsMasterKeyId_CanBeSet()
	{
		// Arrange
		var options = new AwsSqsOptions();

		// Act
		options.KmsMasterKeyId = "alias/my-key";

		// Assert
		options.KmsMasterKeyId.ShouldBe("alias/my-key");
	}

	[Fact]
	public void MessageRetentionPeriod_CanBeCustomized()
	{
		// Arrange
		var options = new AwsSqsOptions();

		// Act
		options.MessageRetentionPeriod = 86400; // 1 day

		// Assert
		options.MessageRetentionPeriod.ShouldBe(86400);
	}
}
