// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Transport;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportOptionsShould
{
	// --- AzureStorageQueueOptions ---

	[Fact]
	public void AzureStorageQueueOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new AzureStorageQueueOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
		options.MaxMessages.ShouldBe(32);
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void AzureStorageQueueOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new AzureStorageQueueOptions
		{
			ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test",
			MaxMessages = 10,
			VisibilityTimeout = TimeSpan.FromMinutes(5),
			PollingInterval = TimeSpan.FromSeconds(5),
		};

		// Assert
		options.ConnectionString.ShouldBe("DefaultEndpointsProtocol=https;AccountName=test");
		options.MaxMessages.ShouldBe(10);
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	// --- CronTimerOptions ---

	[Fact]
	public void CronTimerOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CronTimerOptions();

		// Assert
		options.TimeZone.ShouldBe(TimeZoneInfo.Utc);
		options.RunOnStartup.ShouldBeFalse();
		options.PreventOverlap.ShouldBeTrue();
	}

	[Fact]
	public void CronTimerOptions_AllProperties_AreSettable()
	{
		// Arrange
		var pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

		// Act
		var options = new CronTimerOptions
		{
			TimeZone = pacific,
			RunOnStartup = true,
			PreventOverlap = false,
		};

		// Assert
		options.TimeZone.ShouldBe(pacific);
		options.RunOnStartup.ShouldBeTrue();
		options.PreventOverlap.ShouldBeFalse();
	}

	// --- RabbitMQOptions ---

	[Fact]
	public void RabbitMQOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new RabbitMQOptions();

		// Assert
		options.VirtualHost.ShouldBe("/");
		options.PrefetchCount.ShouldBe((ushort)100);
		options.AutoAck.ShouldBeFalse();
		options.Durable.ShouldBeTrue();
		options.Exchange.ShouldBe(string.Empty);
		options.RoutingKey.ShouldBe(string.Empty);
	}

	[Fact]
	public void RabbitMQOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new RabbitMQOptions
		{
			VirtualHost = "/test",
			PrefetchCount = 50,
			AutoAck = true,
			Durable = false,
			Exchange = "test-exchange",
			RoutingKey = "test-key",
		};

		// Assert
		options.VirtualHost.ShouldBe("/test");
		options.PrefetchCount.ShouldBe((ushort)50);
		options.AutoAck.ShouldBeTrue();
		options.Durable.ShouldBeFalse();
		options.Exchange.ShouldBe("test-exchange");
		options.RoutingKey.ShouldBe("test-key");
	}

	// --- SnsOptions ---

	[Fact]
	public void SnsOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new SnsOptions();

		// Assert
		options.TopicArn.ShouldBe(string.Empty);
		options.Region.ShouldBe("us-east-1");
		options.EnableDeduplication.ShouldBeFalse();
		options.UseFifo.ShouldBeFalse();
		options.MessageGroupId.ShouldBe("default");
	}

	[Fact]
	public void SnsOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new SnsOptions
		{
			TopicArn = "arn:aws:sns:us-west-2:123456789:my-topic",
			Region = "eu-west-1",
			EnableDeduplication = true,
			UseFifo = true,
			MessageGroupId = "group-1",
		};

		// Assert
		options.TopicArn.ShouldBe("arn:aws:sns:us-west-2:123456789:my-topic");
		options.Region.ShouldBe("eu-west-1");
		options.EnableDeduplication.ShouldBeTrue();
		options.UseFifo.ShouldBeTrue();
		options.MessageGroupId.ShouldBe("group-1");
	}

	// --- SqsOptions ---

	[Fact]
	public void SqsOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new SqsOptions();

		// Assert
		options.QueueUrl.ShouldBeNull();
		options.MaxNumberOfMessages.ShouldBe(10);
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.WaitTimeSeconds.ShouldBe(20);
		options.MaxConcurrency.ShouldBe(10);
		options.Region.ShouldBe("us-east-1");
	}

	[Fact]
	public void SqsOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new SqsOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
			MaxNumberOfMessages = 5,
			VisibilityTimeout = TimeSpan.FromMinutes(2),
			WaitTimeSeconds = 10,
			MaxConcurrency = 20,
			Region = "ap-southeast-1",
		};

		// Assert
		options.QueueUrl.ShouldNotBeNull();
		options.QueueUrl.ToString().ShouldContain("my-queue");
		options.MaxNumberOfMessages.ShouldBe(5);
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.WaitTimeSeconds.ShouldBe(10);
		options.MaxConcurrency.ShouldBe(20);
		options.Region.ShouldBe("ap-southeast-1");
	}
}
