using Excalibur.Dispatch.Options.Transport;

namespace Excalibur.Dispatch.Tests.Options.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportOptionsShould
{
	[Fact]
	public void AzureStorageQueueOptions_HaveDefaults()
	{
		var opts = new AzureStorageQueueOptions();

		opts.ConnectionString.ShouldBe(string.Empty);
		opts.MaxMessages.ShouldBe(32);
		opts.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AzureStorageQueueOptions_AllowSettingProperties()
	{
		var opts = new AzureStorageQueueOptions
		{
			ConnectionString = "UseDevelopmentStorage=true",
			MaxMessages = 16,
			VisibilityTimeout = TimeSpan.FromMinutes(5),
		};

		opts.ConnectionString.ShouldBe("UseDevelopmentStorage=true");
		opts.MaxMessages.ShouldBe(16);
		opts.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void CronTimerOptions_HaveDefaults()
	{
		var opts = new CronTimerOptions();

		opts.TimeZone.ShouldBe(TimeZoneInfo.Utc);
		opts.RunOnStartup.ShouldBeFalse();
		opts.PreventOverlap.ShouldBeTrue();
	}

	[Fact]
	public void CronTimerOptions_AllowSettingProperties()
	{
		var opts = new CronTimerOptions
		{
			TimeZone = TimeZoneInfo.Local,
			RunOnStartup = true,
			PreventOverlap = false,
		};

		opts.TimeZone.ShouldBe(TimeZoneInfo.Local);
		opts.RunOnStartup.ShouldBeTrue();
		opts.PreventOverlap.ShouldBeFalse();
	}

	[Fact]
	public void RabbitMQOptions_HaveDefaults()
	{
		var opts = new RabbitMQOptions();

		opts.VirtualHost.ShouldBe("/");
		opts.PrefetchCount.ShouldBe((ushort)100);
		opts.AutoAck.ShouldBeFalse();
	}

	[Fact]
	public void RabbitMQOptions_AllowSettingProperties()
	{
		var opts = new RabbitMQOptions
		{
			VirtualHost = "/production",
			PrefetchCount = 50,
			AutoAck = true,
		};

		opts.VirtualHost.ShouldBe("/production");
		opts.PrefetchCount.ShouldBe((ushort)50);
		opts.AutoAck.ShouldBeTrue();
	}

	[Fact]
	public void SnsOptions_HaveDefaults()
	{
		var opts = new SnsOptions();

		opts.TopicArn.ShouldBe(string.Empty);
		opts.Region.ShouldBe("us-east-1");
		opts.EnableDeduplication.ShouldBeFalse();
	}

	[Fact]
	public void SnsOptions_AllowSettingProperties()
	{
		var opts = new SnsOptions
		{
			TopicArn = "arn:aws:sns:us-west-2:123456789:my-topic",
			Region = "eu-west-1",
			EnableDeduplication = true,
		};

		opts.TopicArn.ShouldBe("arn:aws:sns:us-west-2:123456789:my-topic");
		opts.Region.ShouldBe("eu-west-1");
		opts.EnableDeduplication.ShouldBeTrue();
	}

	[Fact]
	public void SqsOptions_HaveDefaults()
	{
		var opts = new SqsOptions();

		opts.QueueUrl.ShouldBeNull();
		opts.MaxNumberOfMessages.ShouldBe(10);
		opts.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void SqsOptions_AllowSettingProperties()
	{
		var opts = new SqsOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
			MaxNumberOfMessages = 5,
			VisibilityTimeout = TimeSpan.FromMinutes(1),
		};

		opts.QueueUrl.ShouldNotBeNull();
		opts.MaxNumberOfMessages.ShouldBe(5);
		opts.VisibilityTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}
}
