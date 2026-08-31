using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// Unit tests for AwsSqsOptions configuration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AwsSqsOptionsShould : UnitTestBase
{
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

}
