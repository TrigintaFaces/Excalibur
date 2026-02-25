// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="AwsSqsSnsOptions"/> and related options classes.
/// Part of S471.4 - Unit Tests for SNS Builders (Sprint 471).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsSnsOptionsShould : UnitTestBase
{
	private const string ValidTopicArn = "arn:aws:sns:us-east-1:123456789012:orders";
	private const string ValidQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/orders";

	#region Constants Tests

	[Fact]
	public void MaxTopicPrefixLength_Be256()
	{
		// Assert
		AwsSqsSnsOptions.MaxTopicPrefixLength.ShouldBe(256);
	}

	[Fact]
	public void MaxFilterPolicyAttributes_Be5()
	{
		// Assert
		AwsSqsSnsOptions.MaxFilterPolicyAttributes.ShouldBe(5);
	}

	[Fact]
	public void MaxFilterPolicyValues_Be150()
	{
		// Assert
		AwsSqsSnsOptions.MaxFilterPolicyValues.ShouldBe(150);
	}

	#endregion

	#region Default Values Tests

	[Fact]
	public void Constructor_HaveNullTopicPrefix()
	{
		// Arrange & Act
		var options = new AwsSqsSnsOptions();

		// Assert
		options.TopicPrefix.ShouldBeNull();
	}

	[Fact]
	public void Constructor_HaveAutoCreateTopicsDisabled()
	{
		// Arrange & Act
		var options = new AwsSqsSnsOptions();

		// Assert
		options.AutoCreateTopics.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_HaveRawMessageDeliveryDisabled()
	{
		// Arrange & Act
		var options = new AwsSqsSnsOptions();

		// Assert
		options.RawMessageDelivery.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_HaveEmptyTopicMappings()
	{
		// Arrange & Act
		var options = new AwsSqsSnsOptions();

		// Assert
		options.TopicMappings.ShouldBeEmpty();
		options.HasTopicMappings.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_HaveEmptySubscriptions()
	{
		// Arrange & Act
		var options = new AwsSqsSnsOptions();

		// Assert
		options.Subscriptions.ShouldBeEmpty();
		options.HasSubscriptions.ShouldBeFalse();
	}

	#endregion

	#region TopicPrefix Tests

	[Fact]
	public void TopicPrefix_CanBeSet()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();

		// Act
		options.TopicPrefix = "myapp-prod-";

		// Assert
		options.TopicPrefix.ShouldBe("myapp-prod-");
	}

	[Fact]
	public void TopicPrefix_CanBeSetToNull()
	{
		// Arrange
		var options = new AwsSqsSnsOptions { TopicPrefix = "test-" };

		// Act
		options.TopicPrefix = null;

		// Assert
		options.TopicPrefix.ShouldBeNull();
	}

	[Fact]
	public void TopicPrefix_AcceptMaxLength()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var maxPrefix = new string('a', AwsSqsSnsOptions.MaxTopicPrefixLength);

		// Act
		options.TopicPrefix = maxPrefix;

		// Assert
		options.TopicPrefix.ShouldBe(maxPrefix);
	}

	[Fact]
	public void TopicPrefix_ThrowWhenExceedsMaxLength()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var tooLongPrefix = new string('a', AwsSqsSnsOptions.MaxTopicPrefixLength + 1);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			options.TopicPrefix = tooLongPrefix);
	}

	#endregion

	#region AutoCreateTopics Tests

	[Fact]
	public void AutoCreateTopics_CanBeEnabled()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();

		// Act
		options.AutoCreateTopics = true;

		// Assert
		options.AutoCreateTopics.ShouldBeTrue();
	}

	[Fact]
	public void AutoCreateTopics_CanBeDisabled()
	{
		// Arrange
		var options = new AwsSqsSnsOptions { AutoCreateTopics = true };

		// Act
		options.AutoCreateTopics = false;

		// Assert
		options.AutoCreateTopics.ShouldBeFalse();
	}

	#endregion

	#region RawMessageDelivery Tests

	[Fact]
	public void RawMessageDelivery_CanBeEnabled()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();

		// Act
		options.RawMessageDelivery = true;

		// Assert
		options.RawMessageDelivery.ShouldBeTrue();
	}

	[Fact]
	public void RawMessageDelivery_CanBeDisabled()
	{
		// Arrange
		var options = new AwsSqsSnsOptions { RawMessageDelivery = true };

		// Act
		options.RawMessageDelivery = false;

		// Assert
		options.RawMessageDelivery.ShouldBeFalse();
	}

	#endregion

	#region TopicMappings Tests

	[Fact]
	public void TopicMappings_CanAddMapping()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();

		// Act
		options.TopicMappings[typeof(TestMessage)] = ValidTopicArn;

		// Assert
		options.HasTopicMappings.ShouldBeTrue();
		options.TopicMappings[typeof(TestMessage)].ShouldBe(ValidTopicArn);
	}

	[Fact]
	public void TopicMappings_CanHaveMultipleMappings()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();

		// Act
		options.TopicMappings[typeof(TestMessage)] = ValidTopicArn;
		options.TopicMappings[typeof(string)] = "arn:aws:sns:us-east-1:123:strings";

		// Assert
		options.TopicMappings.Count.ShouldBe(2);
	}

	#endregion

	#region Subscriptions Tests

	[Fact]
	public void Subscriptions_CanAddSubscription()
	{
		// Arrange
		var options = new AwsSqsSnsOptions();
		var subscription = new AwsSqsSubscriptionOptions
		{
			TopicArn = ValidTopicArn,
			QueueUrl = ValidQueueUrl,
		};

		// Act
		options.Subscriptions.Add(subscription);

		// Assert
		options.HasSubscriptions.ShouldBeTrue();
		options.Subscriptions.Count.ShouldBe(1);
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void Options_SupportFullConfiguration()
	{
		// Arrange & Act
		var options = new AwsSqsSnsOptions
		{
			TopicPrefix = "prod-",
			AutoCreateTopics = true,
			RawMessageDelivery = true,
		};
		options.TopicMappings[typeof(TestMessage)] = ValidTopicArn;
		options.Subscriptions.Add(new AwsSqsSubscriptionOptions
		{
			TopicArn = ValidTopicArn,
			QueueUrl = ValidQueueUrl,
			RawMessageDelivery = false,
		});

		// Assert
		options.TopicPrefix.ShouldBe("prod-");
		options.AutoCreateTopics.ShouldBeTrue();
		options.RawMessageDelivery.ShouldBeTrue();
		options.HasTopicMappings.ShouldBeTrue();
		options.HasSubscriptions.ShouldBeTrue();
	}

	#endregion

	private sealed class TestMessage { }
}

/// <summary>
/// Unit tests for <see cref="AwsSqsSubscriptionOptions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsSubscriptionOptionsShould : UnitTestBase
{
	private const string ValidTopicArn = "arn:aws:sns:us-east-1:123456789012:orders";
	private const string ValidQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/orders";

	#region Default Values Tests

	[Fact]
	public void Constructor_HaveEmptyTopicArn()
	{
		// Arrange & Act
		var options = new AwsSqsSubscriptionOptions();

		// Assert
		options.TopicArn.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_HaveEmptyQueueUrl()
	{
		// Arrange & Act
		var options = new AwsSqsSubscriptionOptions();

		// Assert
		options.QueueUrl.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_HaveNullRawMessageDelivery()
	{
		// Arrange & Act
		var options = new AwsSqsSubscriptionOptions();

		// Assert
		options.RawMessageDelivery.ShouldBeNull();
	}

	[Fact]
	public void Constructor_HaveNullFilterPolicy()
	{
		// Arrange & Act
		var options = new AwsSqsSubscriptionOptions();

		// Assert
		options.FilterPolicy.ShouldBeNull();
		options.HasFilterPolicy.ShouldBeFalse();
	}

	#endregion

	#region Property Tests

	[Fact]
	public void TopicArn_CanBeSet()
	{
		// Arrange
		var options = new AwsSqsSubscriptionOptions();

		// Act
		options.TopicArn = ValidTopicArn;

		// Assert
		options.TopicArn.ShouldBe(ValidTopicArn);
	}

	[Fact]
	public void QueueUrl_CanBeSet()
	{
		// Arrange
		var options = new AwsSqsSubscriptionOptions();

		// Act
		options.QueueUrl = ValidQueueUrl;

		// Assert
		options.QueueUrl.ShouldBe(ValidQueueUrl);
	}

	[Fact]
	public void RawMessageDelivery_CanBeSet()
	{
		// Arrange
		var options = new AwsSqsSubscriptionOptions();

		// Act
		options.RawMessageDelivery = true;

		// Assert
		options.RawMessageDelivery.ShouldBe(true);
	}

	[Fact]
	public void FilterPolicy_CanBeSet()
	{
		// Arrange
		var options = new AwsSqsSubscriptionOptions();
		var filterPolicy = new AwsSqsFilterPolicyOptions();

		// Act
		options.FilterPolicy = filterPolicy;

		// Assert
		options.FilterPolicy.ShouldBeSameAs(filterPolicy);
		options.HasFilterPolicy.ShouldBeTrue();
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void Options_SupportFullConfiguration()
	{
		// Arrange & Act
		var options = new AwsSqsSubscriptionOptions
		{
			TopicArn = ValidTopicArn,
			QueueUrl = ValidQueueUrl,
			RawMessageDelivery = true,
			FilterPolicy = new AwsSqsFilterPolicyOptions(),
		};

		// Assert
		options.TopicArn.ShouldBe(ValidTopicArn);
		options.QueueUrl.ShouldBe(ValidQueueUrl);
		options.RawMessageDelivery.ShouldBe(true);
		options.HasFilterPolicy.ShouldBeTrue();
	}

	#endregion
}
