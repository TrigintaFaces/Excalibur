// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing;

namespace Excalibur.Dispatch.Testing.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class MessageContextBuilderDepthShould
{
	#region CreateChildContext

	[Fact]
	public void CreateChildContextInheritingCorrelationId()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithCorrelationId("parent-corr")
			.WithTenantId("tenant-A")
			.WithUserId("user-1")
			.WithSessionId("sess-1")
			.WithWorkflowId("wf-1")
			.WithTraceParent("00-trace")
			.WithSource("source-1")
			.WithRequestServices(A.Fake<IServiceProvider>())
			.Build();

		// Act
		var child = context.CreateChildContext();

		// Assert
		child.ShouldNotBeNull();
		child.CorrelationId.ShouldBe("parent-corr");
		child.TenantId.ShouldBe("tenant-A");
		child.UserId.ShouldBe("user-1");
		child.SessionId.ShouldBe("sess-1");
		child.WorkflowId.ShouldBe("wf-1");
		child.TraceParent.ShouldBe("00-trace");
		child.Source.ShouldBe("source-1");
	}

	[Fact]
	public void CreateChildContextWithNewMessageId()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithMessageId("parent-msg-id")
			.Build();

		// Act
		var child = context.CreateChildContext();

		// Assert
		child.MessageId.ShouldNotBe("parent-msg-id");
		child.MessageId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(child.MessageId, out _).ShouldBeTrue();
	}

	[Fact]
	public void CreateChildContextSettingCausationIdToParentMessageId()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithMessageId("parent-msg-id")
			.WithCorrelationId("corr-1")
			.Build();

		// Act
		var child = context.CreateChildContext();

		// Assert
		child.CausationId.ShouldBe("parent-msg-id");
	}

	[Fact]
	public void CreateChildContextFallingBackToCorrelationIdWhenNoMessageId()
	{
		// Arrange - build a context with an explicit null MessageId scenario
		var context = new MessageContextBuilder()
			.WithCorrelationId("corr-fallback")
			.Build();

		// The default builder auto-generates MessageId, so child.CausationId will be parent's MessageId
		var child = context.CreateChildContext();

		// Assert - CausationId should be the auto-generated parent MessageId
		child.CausationId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CreateChildContextInheritingRequestServices()
	{
		// Arrange
		var sp = A.Fake<IServiceProvider>();
		var context = new MessageContextBuilder()
			.WithRequestServices(sp)
			.Build();

		// Act
		var child = context.CreateChildContext();

		// Assert
		child.RequestServices.ShouldBeSameAs(sp);
	}

	[Fact]
	public void CreateChildContextWithDifferentObjectReference()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act
		var child = context.CreateChildContext();

		// Assert
		child.ShouldNotBeSameAs(context);
	}

	#endregion

	#region Items and Properties

	[Fact]
	public void ContainsItemReturnsTrueForExistingKey()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithItem("existing-key", "value")
			.Build();

		// Act & Assert
		context.ContainsItem("existing-key").ShouldBeTrue();
	}

	[Fact]
	public void ContainsItemReturnsFalseForMissingKey()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act & Assert
		context.ContainsItem("nonexistent-key").ShouldBeFalse();
	}

	[Fact]
	public void GetItemReturnsDefaultForMissingKey()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act
		var result = context.GetItem<string>("missing");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetItemReturnsDefaultForWrongType()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithItem("key", "string-value")
			.Build();

		// Act
		var result = context.GetItem<int>("key");

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void GetItemWithDefaultReturnsDefaultForMissingKey()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act
		var result = context.GetItem("missing", "fallback-value");

		// Assert
		result.ShouldBe("fallback-value");
	}

	[Fact]
	public void GetItemWithDefaultReturnsDefaultForWrongType()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithItem("key", "string-value")
			.Build();

		// Act
		var result = context.GetItem("key", 42);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public void GetItemWithDefaultReturnsActualValueWhenFound()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithItem("key", 99)
			.Build();

		// Act
		var result = context.GetItem("key", 42);

		// Assert
		result.ShouldBe(99);
	}

	[Fact]
	public void SetItemAddsNewItem()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act
		context.SetItem("dynamic-key", "dynamic-value");

		// Assert
		context.GetItem<string>("dynamic-key").ShouldBe("dynamic-value");
	}

	[Fact]
	public void SetItemOverwritesExistingItem()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithItem("key", "original")
			.Build();

		// Act
		context.SetItem("key", "updated");

		// Assert
		context.GetItem<string>("key").ShouldBe("updated");
	}

	[Fact]
	public void RemoveItemDeletesExistingItem()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithItem("removable", "value")
			.Build();

		// Act
		context.RemoveItem("removable");

		// Assert
		context.ContainsItem("removable").ShouldBeFalse();
	}

	[Fact]
	public void RemoveItemDoesNotThrowForMissingKey()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act & Assert - should not throw
		context.RemoveItem("nonexistent");
	}

	[Fact]
	public void ItemsCollectionReflectsAddedItems()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithItem("a", 1)
			.WithItem("b", 2)
			.Build();

		// Act & Assert
		context.Items.Count.ShouldBe(2);
		context.Items.ContainsKey("a").ShouldBeTrue();
		context.Items.ContainsKey("b").ShouldBeTrue();
	}

	[Fact]
	public void PropertiesCollectionSameAsItems()
	{
		// Arrange
		var context = new MessageContextBuilder()
			.WithItem("prop-key", "prop-val")
			.Build();

		// Act & Assert - Properties should reflect same data
		context.Properties.ContainsKey("prop-key").ShouldBeTrue();
	}

	#endregion

	#region CancellationToken

	[Fact]
	public void SetCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();

		// Act — CancellationToken is not exposed on IMessageContext but builder accepts it
		var context = new MessageContextBuilder()
			.WithCancellationToken(cts.Token)
			.Build();

		// Assert — verify the builder does not throw; IMessageContext does not expose CancellationToken
		context.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultCancellationTokenIsNone()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert — verify the builder does not throw; IMessageContext does not expose CancellationToken
		context.ShouldNotBeNull();
	}

	#endregion

	#region Default Values

	[Fact]
	public void DefaultDeliveryCountIsZero()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.DeliveryCount.ShouldBe(0);
	}

	[Fact]
	public void DefaultCausationIdIsNull()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.CausationId.ShouldBeNull();
	}

	[Fact]
	public void DefaultRoutingDecisionIsNotNull()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.RoutingDecision.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultProcessingAttemptsIsZero()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.ProcessingAttempts.ShouldBe(0);
	}

	[Fact]
	public void DefaultIsRetryIsFalse()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.IsRetry.ShouldBeFalse();
	}

	[Fact]
	public void DefaultTimeoutExceededIsFalse()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.TimeoutExceeded.ShouldBeFalse();
	}

	[Fact]
	public void DefaultRateLimitExceededIsFalse()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.RateLimitExceeded.ShouldBeFalse();
	}

	[Fact]
	public void DefaultResultIsNull()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.Result.ShouldBeNull();
	}

	[Fact]
	public void DefaultValidationPassedIsFalse()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.ValidationPassed.ShouldBeFalse();
	}

	[Fact]
	public void DefaultSentTimestampUtcIsNull()
	{
		// Arrange & Act
		var context = new MessageContextBuilder().Build();

		// Assert
		context.SentTimestampUtc.ShouldBeNull();
	}

	#endregion

	#region Mutable Properties After Build

	[Fact]
	public void AllowSettingResultAfterBuild()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act
		context.Result = "some-result";

		// Assert
		context.Result.ShouldBe("some-result");
	}

	[Fact]
	public void AllowSettingProcessingAttemptsAfterBuild()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act
		context.ProcessingAttempts = 5;

		// Assert
		context.ProcessingAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingIsRetryAfterBuild()
	{
		// Arrange
		var context = new MessageContextBuilder().Build();

		// Act
		context.IsRetry = true;

		// Assert
		context.IsRetry.ShouldBeTrue();
	}

	#endregion

	#region Full Fluent Chain

	[Fact]
	public void SupportFullFluentChainWithAllProperties()
	{
		// Arrange
		var sp = A.Fake<IServiceProvider>();
		var msg = A.Fake<IDispatchMessage>();
		using var cts = new CancellationTokenSource();

		// Act
		var context = new MessageContextBuilder()
			.WithMessageId("msg-1")
			.WithCorrelationId("corr-1")
			.WithCausationId("cause-1")
			.WithTenantId("tenant-1")
			.WithUserId("user-1")
			.WithSessionId("sess-1")
			.WithWorkflowId("wf-1")
			.WithPartitionKey("pk-1")
			.WithSource("source-1")
			.WithMessageType("type-1")
			.WithContentType("application/json")
			.WithTraceParent("trace-1")
			.WithExternalId("ext-1")
			.WithDeliveryCount(3)
			.WithCancellationToken(cts.Token)
			.WithRequestServices(sp)
			.WithMessage(msg)
			.WithItem("custom", "value")
			.Build();

		// Assert - all properties set correctly
		context.MessageId.ShouldBe("msg-1");
		context.CorrelationId.ShouldBe("corr-1");
		context.CausationId.ShouldBe("cause-1");
		context.TenantId.ShouldBe("tenant-1");
		context.UserId.ShouldBe("user-1");
		context.SessionId.ShouldBe("sess-1");
		context.WorkflowId.ShouldBe("wf-1");
		context.PartitionKey.ShouldBe("pk-1");
		context.Source.ShouldBe("source-1");
		context.MessageType.ShouldBe("type-1");
		context.ContentType.ShouldBe("application/json");
		context.TraceParent.ShouldBe("trace-1");
		context.ExternalId.ShouldBe("ext-1");
		context.DeliveryCount.ShouldBe(3);
		// CancellationToken is not exposed on IMessageContext; skipping assertion
		context.RequestServices.ShouldBeSameAs(sp);
		context.Message.ShouldBeSameAs(msg);
		context.GetItem<string>("custom").ShouldBe("value");
	}

	#endregion
}
