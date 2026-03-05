// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Tests.TestFakes;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageContextShould : IDisposable
{
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();

	public void Dispose()
	{
		// No resources to dispose
	}

	[Fact]
	public void InitializeWithMessageAndServiceProvider()
	{
		// Arrange
		var message = new FakeDispatchMessage();

		// Act
		var context = new MessageContext(message, _serviceProvider);

		// Assert
		context.Message.ShouldBe(message);
		context.RequestServices.ShouldBe(_serviceProvider);
	}

	[Fact]
	public void ThrowWhenMessageIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new MessageContext(null!, _serviceProvider));
	}

	[Fact]
	public void ThrowWhenServiceProviderIsNull()
	{
		// Act & Assert
		var message = new FakeDispatchMessage();
		Should.Throw<ArgumentNullException>(() => new MessageContext(message, null!));
	}

	[Fact]
	public void InitializeWithParameterlessConstructor()
	{
		// Act
		var context = new MessageContext();

		// Assert
		context.Message.ShouldNotBeNull();
		context.RequestServices.ShouldNotBeNull();
	}

	[Fact]
	public void GenerateMessageIdLazily()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		var id = context.MessageId;

		// Assert
		id.ShouldNotBeNullOrWhiteSpace();
		Guid.TryParse(id, out _).ShouldBeTrue();
	}

	[Fact]
	public void ReturnSameMessageIdOnMultipleAccesses()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		var id1 = context.MessageId;
		var id2 = context.MessageId;

		// Assert
		id1.ShouldBe(id2);
	}

	[Fact]
	public void AllowExplicitMessageIdSet()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var customId = "custom-id-123";

		// Act
		context.MessageId = customId;

		// Assert
		context.MessageId.ShouldBe(customId);
	}

	[Fact]
	public void AllowExplicitNullMessageId()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.MessageId = null;

		// Act & Assert
		context.MessageId.ShouldBeNull();
	}

	[Fact]
	public void SetAndGetStringProperties()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		context.ExternalId = "ext-1";
		context.UserId = "user-1";
		context.CorrelationId = "corr-1";
		context.CausationId = "cause-1";
		context.TraceParent = "trace-1";
		context.SerializerVersion = "v1";
		context.MessageVersion = "1.0";
		context.ContractVersion = "2.0";
		context.TenantId = "tenant-1";
		context.SessionId = "session-1";
		context.WorkflowId = "workflow-1";
		context.Source = "source-1";
		context.MessageType = "TestType";
		context.ContentType = "application/json";
		context.PartitionKey = "partition-1";
		context.ReplyTo = "reply-to-1";

		// Assert
		context.ExternalId.ShouldBe("ext-1");
		context.UserId.ShouldBe("user-1");
		context.CorrelationId.ShouldBe("corr-1");
		context.CausationId.ShouldBe("cause-1");
		context.TraceParent.ShouldBe("trace-1");
		context.SerializerVersion.ShouldBe("v1");
		context.MessageVersion.ShouldBe("1.0");
		context.ContractVersion.ShouldBe("2.0");
		context.TenantId.ShouldBe("tenant-1");
		context.SessionId.ShouldBe("session-1");
		context.WorkflowId.ShouldBe("workflow-1");
		context.Source.ShouldBe("source-1");
		context.MessageType.ShouldBe("TestType");
		context.ContentType.ShouldBe("application/json");
		context.PartitionKey.ShouldBe("partition-1");
		context.ReplyTo.ShouldBe("reply-to-1");
	}

	[Fact]
	public void SetAndGetDesiredVersion()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		context.DesiredVersion = 5;

		// Assert
		context.DesiredVersion.ShouldBe(5);
	}

	[Fact]
	public void SetAndGetDeliveryCount()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		context.DeliveryCount = 3;

		// Assert
		context.DeliveryCount.ShouldBe(3);
	}

	[Fact]
	public void SetAndGetReceivedTimestampUtc()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

		// Act
		context.ReceivedTimestampUtc = timestamp;

		// Assert
		context.ReceivedTimestampUtc.ShouldBe(timestamp);
	}

	[Fact]
	public void SetAndGetSentTimestampUtc()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var timestamp = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

		// Act
		context.SentTimestampUtc = timestamp;

		// Assert
		context.SentTimestampUtc.ShouldBe(timestamp);
	}

	[Fact]
	public void SetAndGetVersionMetadata()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var metadata = A.Fake<IMessageVersionMetadata>();

		// Act
		context.VersionMetadata = metadata;

		// Assert
		context.VersionMetadata.ShouldBe(metadata);
	}

	[Fact]
	public void ThrowWhenVersionMetadataSetToNull()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.VersionMetadata = null!);
	}

	[Fact]
	public void SetAndGetValidationResult()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var result = new TestValidationResult(true);

		// Act
		context.ValidationResult = result;

		// Assert — cast to object to avoid CS8920 with IValidationResult static abstract members
		((object)context.ValidationResult).ShouldBe(result);
	}

	[Fact]
	public void ThrowWhenValidationResultSetToNull()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.ValidationResult = null!);
	}

	[Fact]
	public void SetAndGetAuthorizationResult()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var result = A.Fake<IAuthorizationResult>();
		A.CallTo(() => result.IsAuthorized).Returns(true);

		// Act
		context.AuthorizationResult = result;

		// Assert
		context.AuthorizationResult.ShouldBe(result);
	}

	[Fact]
	public void ThrowWhenAuthorizationResultSetToNull()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.AuthorizationResult = null!);
	}

	[Fact]
	public void SetAndGetRoutingDecision()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var decision = RoutingDecision.Success("test", []);

		// Act
		context.RoutingDecision = decision;

		// Assert
		context.RoutingDecision.ShouldBe(decision);
	}

	[Fact]
	public void SetAndGetRequestServices()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var newProvider = A.Fake<IServiceProvider>();

		// Act
		context.RequestServices = newProvider;

		// Assert
		context.RequestServices.ShouldBe(newProvider);
	}

	[Fact]
	public void ThrowWhenRequestServicesSetToNull()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.RequestServices = null!);
	}

	[Fact]
	public void SetAndGetHotPathProperties()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var time = DateTimeOffset.UtcNow;

		// Act
		context.ProcessingAttempts = 2;
		context.FirstAttemptTime = time;
		context.IsRetry = true;
		context.ValidationPassed = true;
		context.ValidationTimestamp = time;
		context.Transaction = new object();
		context.TransactionId = "tx-1";
		context.TimeoutExceeded = true;
		context.TimeoutElapsed = TimeSpan.FromSeconds(5);
		context.RateLimitExceeded = true;
		context.RateLimitRetryAfter = TimeSpan.FromMinutes(1);

		// Assert
		context.ProcessingAttempts.ShouldBe(2);
		context.FirstAttemptTime.ShouldBe(time);
		context.IsRetry.ShouldBeTrue();
		context.ValidationPassed.ShouldBeTrue();
		context.ValidationTimestamp.ShouldBe(time);
		context.Transaction.ShouldNotBeNull();
		context.TransactionId.ShouldBe("tx-1");
		context.TimeoutExceeded.ShouldBeTrue();
		context.TimeoutElapsed.ShouldBe(TimeSpan.FromSeconds(5));
		context.RateLimitExceeded.ShouldBeTrue();
		context.RateLimitRetryAfter.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void ReturnSuccessWhenAllResultsAreValid()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		// Default validation and authorization results are success

		// Act & Assert
		context.Success.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFailureWhenValidationFails()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var failResult = new TestValidationResult(false);
		context.ValidationResult = failResult;

		// Act & Assert
		context.Success.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFailureWhenAuthorizationFails()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var failResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => failResult.IsAuthorized).Returns(false);
		context.AuthorizationResult = failResult;

		// Act & Assert
		context.Success.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFailureWhenRoutingFails()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.RoutingDecision = RoutingDecision.Failure("route-error");

		// Act & Assert
		context.Success.ShouldBeFalse();
	}

	[Fact]
	public void InitializeLazyItemsDictionaryOnAccess()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		var items = context.Items;

		// Assert
		items.ShouldNotBeNull();
		items.Count.ShouldBe(0);
	}

	[Fact]
	public void ReturnPropertiesDictionary()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.Items["key1"] = "value1";

		// Act
		var properties = context.Properties;

		// Assert
		properties.ShouldNotBeNull();
		properties.ContainsKey("key1").ShouldBeTrue();
	}

	[Fact]
	public void ContainsItemReturnsTrueForExistingKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.SetItem("testKey", "testValue");

		// Act & Assert
		context.ContainsItem("testKey").ShouldBeTrue();
	}

	[Fact]
	public void ContainsItemReturnsFalseForMissingKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		context.ContainsItem("missing").ShouldBeFalse();
	}

	[Fact]
	public void ContainsItemThrowsForNullOrWhiteSpaceKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		Should.Throw<ArgumentException>(() => context.ContainsItem(null!));
		Should.Throw<ArgumentException>(() => context.ContainsItem(""));
		Should.Throw<ArgumentException>(() => context.ContainsItem("   "));
	}

	[Fact]
	public void GetItemReturnsValueForExistingKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.SetItem("testKey", 42);

		// Act
		var value = context.GetItem<int>("testKey");

		// Assert
		value.ShouldBe(42);
	}

	[Fact]
	public void GetItemReturnsDefaultForMissingKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		var value = context.GetItem<string>("missing");

		// Assert
		value.ShouldBeNull();
	}

	[Fact]
	public void GetItemWithDefaultReturnsDefaultForMissingKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		var value = context.GetItem("missing", "fallback");

		// Assert
		value.ShouldBe("fallback");
	}

	[Fact]
	public void GetItemThrowsForNullOrWhiteSpaceKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		Should.Throw<ArgumentException>(() => context.GetItem<string>(null!));
		Should.Throw<ArgumentException>(() => context.GetItem<string>(""));
	}

	[Fact]
	public void SetItemAddsOrUpdatesValue()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		context.SetItem("key", "value1");
		context.SetItem("key", "value2");

		// Assert
		context.GetItem<string>("key").ShouldBe("value2");
	}

	[Fact]
	public void SetItemRemovesWhenValueIsNull()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.SetItem("key", "value");

		// Act
		context.SetItem<string?>("key", null);

		// Assert
		context.ContainsItem("key").ShouldBeFalse();
	}

	[Fact]
	public void SetItemThrowsForNullOrWhiteSpaceKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		Should.Throw<ArgumentException>(() => context.SetItem(null!, "value"));
		Should.Throw<ArgumentException>(() => context.SetItem("", "value"));
	}

	[Fact]
	public void RemoveItemRemovesExistingKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.SetItem("key", "value");

		// Act
		context.RemoveItem("key");

		// Assert
		context.ContainsItem("key").ShouldBeFalse();
	}

	[Fact]
	public void RemoveItemDoesNothingForMissingKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert — should not throw
		context.RemoveItem("nonexistent");
	}

	[Fact]
	public void RemoveItemThrowsForNullOrWhiteSpaceKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		Should.Throw<ArgumentException>(() => context.RemoveItem(null!));
		Should.Throw<ArgumentException>(() => context.RemoveItem(""));
	}

	[Fact]
	public void ResetClearsAllProperties()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.ExternalId = "ext";
		context.UserId = "user";
		context.CorrelationId = "corr";
		context.CausationId = "cause";
		context.TenantId = "tenant";
		context.DeliveryCount = 5;
		context.ProcessingAttempts = 3;
		context.IsRetry = true;
		context.ValidationPassed = true;
		context.TimeoutExceeded = true;
		context.RateLimitExceeded = true;
		context.SetItem("key", "value");

		// Act
		context.Reset();

		// Assert
		context.ExternalId.ShouldBeNull();
		context.UserId.ShouldBeNull();
		context.CorrelationId.ShouldBeNull();
		context.CausationId.ShouldBeNull();
		context.TenantId.ShouldBeNull();
		context.DeliveryCount.ShouldBe(0);
		context.ProcessingAttempts.ShouldBe(0);
		context.IsRetry.ShouldBeFalse();
		context.ValidationPassed.ShouldBeFalse();
		context.TimeoutExceeded.ShouldBeFalse();
		context.RateLimitExceeded.ShouldBeFalse();
		// Items should be cleared but dictionary reused
		context.ContainsItem("key").ShouldBeFalse();
	}

	[Fact]
	public void ResetGeneratesNewMessageId()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var oldId = context.MessageId;

		// Act
		context.Reset();
		var newId = context.MessageId;

		// Assert
		newId.ShouldNotBe(oldId);
	}

	[Fact]
	public void InitializeSetsServiceProviderAndTimestamp()
	{
		// Arrange
		var context = new MessageContext();
		var provider = A.Fake<IServiceProvider>();
		var before = DateTimeOffset.UtcNow;

		// Act
		context.Initialize(provider);
		var after = DateTimeOffset.UtcNow;

		// Assert
		context.RequestServices.ShouldBe(provider);
		context.ReceivedTimestampUtc.ShouldBeInRange(before, after);
	}

	[Fact]
	public void InitializeThrowsWhenServiceProviderIsNull()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.Initialize(null!));
	}

	[Fact]
	public void CreateChildContextPropagatesCrossCuttingIds()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.CorrelationId = "corr-parent";
		context.TenantId = "tenant-1";
		context.UserId = "user-1";
		context.SessionId = "session-1";
		context.WorkflowId = "workflow-1";
		context.TraceParent = "trace-1";
		context.Source = "source-1";

		// Act
		var child = context.CreateChildContext();

		// Assert
		child.CorrelationId.ShouldBe("corr-parent");
		child.CausationId.ShouldBe(context.MessageId);
		child.TenantId.ShouldBe("tenant-1");
		child.UserId.ShouldBe("user-1");
		child.SessionId.ShouldBe("session-1");
		child.WorkflowId.ShouldBe("workflow-1");
		child.TraceParent.ShouldBe("trace-1");
		child.Source.ShouldBe("source-1");
		child.MessageId.ShouldNotBe(context.MessageId);
	}

	[Fact]
	public void CreateForDeserializationCreatesContextWithServiceProvider()
	{
		// Arrange & Act
		var context = MessageContext.CreateForDeserialization(_serviceProvider);

		// Assert
		context.ShouldNotBeNull();
		context.RequestServices.ShouldBe(_serviceProvider);
	}

	[Fact]
	public void SetAndGetResult()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var result = new { Success = true };

		// Act
		context.Result = result;

		// Assert
		context.Result.ShouldBe(result);
	}

	[Fact]
	public void SetAndGetMetadata()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var metadata = A.Fake<IMessageMetadata>();

		// Act
		context.Metadata = metadata;

		// Assert
		context.Metadata.ShouldBe(metadata);
	}

	[Fact]
	public void ResetRestoresDefaultServiceProviderIfSet()
	{
		// Arrange
		var context = new MessageContext();
		var defaultProvider = A.Fake<IServiceProvider>();
		context.Initialize(defaultProvider);

		var tempProvider = A.Fake<IServiceProvider>();
		context.RequestServices = tempProvider;

		// Act
		context.Reset();

		// Assert
		context.RequestServices.ShouldBe(defaultProvider);
	}

	#region PERF-6: Lazy CorrelationId/CausationId Tests

	[Fact]
	public void LazyCorrelationId_ReturnsNullWhenNotMarkedAndNotSet()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert — no lazy marking, no explicit set → null
		context.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void LazyCorrelationId_GeneratesUuid7OnFirstAccessWhenMarked()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.MarkForLazyCorrelation();

		// Act
		var correlationId = context.CorrelationId;

		// Assert
		correlationId.ShouldNotBeNullOrWhiteSpace();
		Guid.TryParse(correlationId, out _).ShouldBeTrue();
	}

	[Fact]
	public void LazyCorrelationId_ReturnsSameValueOnSubsequentAccesses()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.MarkForLazyCorrelation();

		// Act
		var first = context.CorrelationId;
		var second = context.CorrelationId;

		// Assert
		first.ShouldBe(second);
	}

	[Fact]
	public void LazyCorrelationId_PreservesExplicitlySetValue()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.CorrelationId = "explicit-corr-id";

		// Act — marking after explicit set should be a no-op
		context.MarkForLazyCorrelation();

		// Assert
		context.CorrelationId.ShouldBe("explicit-corr-id");
	}

	[Fact]
	public void LazyCorrelationId_ExplicitSetOverridesLazyMarking()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.MarkForLazyCorrelation();

		// Act — explicit set after lazy marking
		context.CorrelationId = "override-corr-id";

		// Assert
		context.CorrelationId.ShouldBe("override-corr-id");
	}

	[Fact]
	public void LazyCorrelationId_ExplicitNullIsPreserved()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.MarkForLazyCorrelation();

		// Act — explicitly set to null
		context.CorrelationId = null;

		// Assert — should return null, not generate
		context.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void LazyCausationId_ReturnsNullWhenNotMarkedAndNotSet()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert
		context.CausationId.ShouldBeNull();
	}

	[Fact]
	public void LazyCausationId_DefaultsToCorrelationIdWhenMarked()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.MarkForLazyCorrelation();
		context.MarkForLazyCausation();

		// Act
		var causationId = context.CausationId;

		// Assert — should equal the lazy-generated CorrelationId
		causationId.ShouldNotBeNullOrWhiteSpace();
		causationId.ShouldBe(context.CorrelationId);
	}

	[Fact]
	public void LazyCausationId_PreservesExplicitlySetValue()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.CausationId = "explicit-cause-id";

		// Act
		context.MarkForLazyCausation();

		// Assert
		context.CausationId.ShouldBe("explicit-cause-id");
	}

	[Fact]
	public void LazyCausationId_ExplicitSetOverridesLazyMarking()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.MarkForLazyCausation();

		// Act
		context.CausationId = "override-cause-id";

		// Assert
		context.CausationId.ShouldBe("override-cause-id");
	}

	[Fact]
	public void ResetClearsLazyCorrelationState()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.MarkForLazyCorrelation();
		context.MarkForLazyCausation();
		var _ = context.CorrelationId; // trigger generation

		// Act
		context.Reset();

		// Assert — after reset, lazy flags are cleared and values are null
		context.CorrelationId.ShouldBeNull();
		context.CausationId.ShouldBeNull();
	}

	[Fact]
	public void LazyCorrelationId_CanBeReMarkedAfterReset()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.MarkForLazyCorrelation();
		var firstCorrelationId = context.CorrelationId;

		// Act — reset and re-mark
		context.Reset();
		context.MarkForLazyCorrelation();
		var secondCorrelationId = context.CorrelationId;

		// Assert — should generate a new value
		secondCorrelationId.ShouldNotBeNullOrWhiteSpace();
		secondCorrelationId.ShouldNotBe(firstCorrelationId);
	}

	#endregion

	#region Static Default Instances Tests

	[Fact]
	public void NewContextUsesStaticDefaultVersionMetadata()
	{
		// Arrange & Act
		var context1 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var context2 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Assert — both should reference the same cached default instance
		ReferenceEquals(context1.VersionMetadata, context2.VersionMetadata).ShouldBeTrue();
	}

	[Fact]
	public void NewContextUsesStaticDefaultValidationResult()
	{
		// Arrange & Act
		var context1 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var context2 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Assert — both should reference the same cached default instance
		// Cast to object to avoid CS8920 with static abstract members
		ReferenceEquals((object)context1.ValidationResult, (object)context2.ValidationResult).ShouldBeTrue();
	}

	[Fact]
	public void NewContextUsesStaticDefaultAuthorizationResult()
	{
		// Arrange & Act
		var context1 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var context2 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Assert — both should reference the same cached default instance
		ReferenceEquals(context1.AuthorizationResult, context2.AuthorizationResult).ShouldBeTrue();
	}

	[Fact]
	public void ResetRestoresStaticDefaultVersionMetadata()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var defaultMetadata = context.VersionMetadata;
		context.VersionMetadata = A.Fake<IMessageVersionMetadata>();

		// Act
		context.Reset();

		// Assert
		ReferenceEquals(context.VersionMetadata, defaultMetadata).ShouldBeTrue();
	}

	#endregion

	/// <summary>
	/// Concrete implementation of IValidationResult for testing.
	/// Cannot use A.Fake because the interface has static abstract members (CS8920).
	/// </summary>
	private sealed class TestValidationResult(bool isValid) : IValidationResult
	{
		public IReadOnlyCollection<object> Errors { get; } = isValid ? [] : ["Error"];
		public bool IsValid { get; } = isValid;

		public static IValidationResult Failed(params object[] errors) =>
			new TestValidationResult(false);

		public static IValidationResult Success() =>
			new TestValidationResult(true);
	}
}
