// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IAwsSqsSnsBuilder"/> and related SNS integration builders.
/// Part of S471.2 - ConfigureSns builder (Sprint 471).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsSnsBuilderShould : UnitTestBase
{
	private const string ValidRegion = "us-east-1";
	private const string ValidTopicArn = "arn:aws:sns:us-east-1:123456789012:orders";
	private const string ValidQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/orders";

	#region ConfigureSns Entry Point Tests

	[Fact]
	public void ConfigureSns_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsSqsTransport("sqs", sqs =>
			{
				_ = sqs.UseRegion(ValidRegion)
				   .ConfigureSns(null!);
			}));
	}

	[Fact]
	public void ConfigureSns_InvokeConfigureCallback()
	{
		// Arrange
		var services = new ServiceCollection();
		var configureInvoked = false;

		// Act
		_ = services.AddAwsSqsTransport("sqs", sqs =>
		{
			_ = sqs.UseRegion(ValidRegion)
			   .ConfigureSns(sns =>
			   {
				   configureInvoked = true;
			   });
		});

		// Assert
		configureInvoked.ShouldBeTrue();
	}

	[Fact]
	public void ConfigureSns_ReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		IAwsSqsTransportBuilder? capturedBuilder = null;

		// Act
		_ = services.AddAwsSqsTransport("sqs", sqs =>
		{
			capturedBuilder = sqs.UseRegion(ValidRegion)
			   .ConfigureSns(sns => { });
		});

		// Assert
		_ = capturedBuilder.ShouldNotBeNull();
	}

	#endregion

	#region TopicPrefix Tests

	[Fact]
	public void TopicPrefix_ThrowWhenPrefixIsNull()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.TopicPrefix(null!));
	}

	[Fact]
	public void TopicPrefix_ThrowWhenPrefixIsEmpty()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.TopicPrefix(""));
	}

	[Fact]
	public void TopicPrefix_ThrowWhenPrefixIsWhitespace()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.TopicPrefix("   "));
	}

	[Fact]
	public void TopicPrefix_SetPrefixInOptions()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.TopicPrefix("myapp-prod-");

		// Assert
		options.TopicPrefix.ShouldBe("myapp-prod-");
	}

	[Fact]
	public void TopicPrefix_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		var result = builder.TopicPrefix("myapp-");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region AutoCreateTopics Tests

	[Fact]
	public void AutoCreateTopics_EnableByDefault()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.AutoCreateTopics();

		// Assert
		options.AutoCreateTopics.ShouldBeTrue();
	}

	[Fact]
	public void AutoCreateTopics_DisableWhenExplicit()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		options.AutoCreateTopics = true; // Pre-enable
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.AutoCreateTopics(false);

		// Assert
		options.AutoCreateTopics.ShouldBeFalse();
	}

	[Fact]
	public void AutoCreateTopics_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		var result = builder.AutoCreateTopics();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region RawMessageDelivery Tests

	[Fact]
	public void RawMessageDelivery_EnableByDefault()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.RawMessageDelivery();

		// Assert
		options.RawMessageDelivery.ShouldBeTrue();
	}

	[Fact]
	public void RawMessageDelivery_DisableWhenExplicit()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		options.RawMessageDelivery = true; // Pre-enable
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.RawMessageDelivery(false);

		// Assert
		options.RawMessageDelivery.ShouldBeFalse();
	}

	[Fact]
	public void RawMessageDelivery_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		var result = builder.RawMessageDelivery();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MapTopic Tests

	[Fact]
	public void MapTopic_ThrowWhenTopicArnIsNull()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapTopic<TestMessage>(null!));
	}

	[Fact]
	public void MapTopic_ThrowWhenTopicArnIsEmpty()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapTopic<TestMessage>(""));
	}

	[Fact]
	public void MapTopic_AddMappingToOptions()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.MapTopic<TestMessage>(ValidTopicArn);

		// Assert
		options.TopicMappings.ShouldContainKey(typeof(TestMessage));
		options.TopicMappings[typeof(TestMessage)].ShouldBe(ValidTopicArn);
	}

	[Fact]
	public void MapTopic_SupportMultipleMappings()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.MapTopic<TestMessage>("arn:aws:sns:us-east-1:123:orders")
			   .MapTopic<AnotherMessage>("arn:aws:sns:us-east-1:123:payments");

		// Assert
		options.TopicMappings.Count.ShouldBe(2);
		options.HasTopicMappings.ShouldBeTrue();
	}

	[Fact]
	public void MapTopic_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		var result = builder.MapTopic<TestMessage>(ValidTopicArn);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region SubscribeQueue Tests

	[Fact]
	public void SubscribeQueue_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.SubscribeQueue<TestMessage>(null!));
	}

	[Fact]
	public void SubscribeQueue_AddSubscriptionToOptions()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.SubscribeQueue<TestMessage>(sub =>
		{
			_ = sub.TopicArn(ValidTopicArn)
			   .QueueUrl(ValidQueueUrl);
		});

		// Assert
		options.Subscriptions.Count.ShouldBe(1);
		options.HasSubscriptions.ShouldBeTrue();
	}

	[Fact]
	public void SubscribeQueue_ConfigureSubscriptionOptions()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.SubscribeQueue<TestMessage>(sub =>
		{
			_ = sub.TopicArn(ValidTopicArn)
			   .QueueUrl(ValidQueueUrl)
			   .RawMessageDelivery(true);
		});

		// Assert
		var subscription = options.Subscriptions[0];
		subscription.TopicArn.ShouldBe(ValidTopicArn);
		subscription.QueueUrl.ShouldBe(ValidQueueUrl);
		subscription.RawMessageDelivery.ShouldBe(true);
	}

	[Fact]
	public void SubscribeQueue_SupportMultipleSubscriptions()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		_ = builder.SubscribeQueue<TestMessage>(sub =>
			sub.TopicArn("arn:aws:sns:us-east-1:123:orders")
			   .QueueUrl("https://sqs.us-east-1.amazonaws.com/123/orders"))
		   .SubscribeQueue<AnotherMessage>(sub =>
			   sub.TopicArn("arn:aws:sns:us-east-1:123:payments")
				  .QueueUrl("https://sqs.us-east-1.amazonaws.com/123/payments"));

		// Assert
		options.Subscriptions.Count.ShouldBe(2);
	}

	[Fact]
	public void SubscribeQueue_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var builder = new AwsSqsSnsBuilder(options);

		// Act
		var result = builder.SubscribeQueue<TestMessage>(sub =>
			sub.TopicArn(ValidTopicArn).QueueUrl(ValidQueueUrl));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Subscription Builder Tests

	[Fact]
	public void SubscriptionBuilder_TopicArn_ThrowWhenNull()
	{
		// Arrange
		var options = new AwsSqsSubscriptionOptions();
		var builder = new AwsSqsSubscriptionBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.TopicArn(null!));
	}

	[Fact]
	public void SubscriptionBuilder_QueueUrl_ThrowWhenNull()
	{
		// Arrange
		var options = new AwsSqsSubscriptionOptions();
		var builder = new AwsSqsSubscriptionBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.QueueUrl(null!));
	}

	[Fact]
	public void SubscriptionBuilder_FilterPolicy_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new AwsSqsSubscriptionOptions();
		var builder = new AwsSqsSubscriptionBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.FilterPolicy(null!));
	}

	[Fact]
	public void SubscriptionBuilder_FilterPolicy_CreateFilterPolicyOptions()
	{
		// Arrange
		var options = new AwsSqsSubscriptionOptions();
		var builder = new AwsSqsSubscriptionBuilder(options);

		// Act
		_ = builder.FilterPolicy(filter =>
		{
			_ = filter.OnMessageAttributes()
				  .Attribute("priority").Equals("high");
		});

		// Assert
		options.HasFilterPolicy.ShouldBeTrue();
		_ = options.FilterPolicy.ShouldNotBeNull();
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void ConfigureSns_SupportFullFluentChain()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddAwsSqsTransport("sqs", sqs =>
			{
				_ = sqs.UseRegion(ValidRegion)
				   .ConfigureSns(sns =>
				   {
					   _ = sns.TopicPrefix("myapp-")
						  .AutoCreateTopics(true)
						  .RawMessageDelivery(true)
						  .MapTopic<TestMessage>(ValidTopicArn)
						  .SubscribeQueue<TestMessage>(sub =>
						  {
							  _ = sub.TopicArn(ValidTopicArn)
								 .QueueUrl(ValidQueueUrl)
								 .RawMessageDelivery(false)
								 .FilterPolicy(filter =>
								 {
									 _ = filter.OnMessageAttributes()
										   .Attribute("priority").Equals("high").Or().Equals("urgent")
										   .And()
										   .Attribute("region").Prefix("us-");
								 });
						  });
				   });
			});
		});
	}

	[Fact]
	public void ConfigureSns_IntegrateWithOtherBuilders()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddAwsSqsTransport("orders", sqs =>
			{
				_ = sqs.UseRegion(ValidRegion)
				   .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)))
				   .ConfigureBatch(batch => batch.SendBatchSize(10))
				   .ConfigureSns(sns =>
				   {
					   _ = sns.TopicPrefix("myapp-")
						  .MapTopic<TestMessage>(ValidTopicArn);
				   })
				   .MapQueue<TestMessage>(ValidQueueUrl);
			});
		});
	}

	#endregion

	#region Helper Classes

	private sealed class TestMessage { }
	private sealed class AnotherMessage { }

	#endregion
}
