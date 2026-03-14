// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
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
	public void SetAndGetStringPropertiesViaFeatures()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act - identity features
		var identity = context.GetOrCreateIdentityFeature();
		identity.ExternalId = "ext-1";
		identity.UserId = "user-1";
		identity.TraceParent = "trace-1";
		identity.TenantId = "tenant-1";
		identity.SessionId = "session-1";
		identity.WorkflowId = "workflow-1";

		// Act - core IMessageContext properties
		context.CorrelationId = "corr-1";
		context.CausationId = "cause-1";

		// Act - Items-based properties
		context.SerializerVersion("v1");
		context.MessageVersion("1.0");
		context.ContractVersion("2.0");
		context.SetMessageType("TestType");
		context.SetContentType("application/json");

		// Act - routing features
		var routing = context.GetOrCreateRoutingFeature();
		routing.Source = "source-1";
		routing.PartitionKey = "partition-1";

		// Items-based well-known
		context.ReplyTo("reply-to-1");

		// Assert - identity features
		context.GetExternalId().ShouldBe("ext-1");
		context.GetUserId().ShouldBe("user-1");
		context.GetTraceParent().ShouldBe("trace-1");
		context.GetTenantId().ShouldBe("tenant-1");
		context.GetSessionId().ShouldBe("session-1");
		context.GetWorkflowId().ShouldBe("workflow-1");

		// Assert - core IMessageContext
		context.CorrelationId.ShouldBe("corr-1");
		context.CausationId.ShouldBe("cause-1");

		// Assert - Items-based
		context.SerializerVersion().ShouldBe("v1");
		context.MessageVersion().ShouldBe("1.0");
		context.ContractVersion().ShouldBe("2.0");
		context.GetMessageType().ShouldBe("TestType");
		context.GetContentType().ShouldBe("application/json");

		// Assert - routing
		context.GetSource().ShouldBe("source-1");
		context.GetPartitionKey().ShouldBe("partition-1");
		context.ReplyTo().ShouldBe("reply-to-1");
	}

	[Fact]
	public void SetAndGetDesiredVersion()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		context.DesiredVersion("5");

		// Assert
		context.DesiredVersion().ShouldBe("5");
	}

	[Fact]
	public void SetAndGetDeliveryCount()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act
		var processing = context.GetOrCreateProcessingFeature();
		processing.DeliveryCount = 3;

		// Assert
		context.GetDeliveryCount().ShouldBe(3);
	}

	[Fact]
	public void SetAndGetReceivedTimestampUtc()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

		// Act
		context.SetReceivedTimestampUtc(timestamp);

		// Assert
		context.GetReceivedTimestampUtc().ShouldBe(timestamp);
	}

	[Fact]
	public void SetAndGetSentTimestampUtc()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var timestamp = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

		// Act
		context.SetSentTimestampUtc(timestamp);

		// Assert
		context.GetSentTimestampUtc().ShouldBe(timestamp);
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

		// Assert -- cast to object to avoid CS8920 with IValidationResult static abstract members
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
	public void SetAndGetRoutingDecisionViaFeature()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var decision = RoutingDecision.Success("test", []);

		// Act
		context.GetOrCreateRoutingFeature().RoutingDecision = decision;

		// Assert
		context.GetRoutingDecision().ShouldBe(decision);
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
	public void SetAndGetHotPathPropertiesViaFeatures()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var time = DateTimeOffset.UtcNow;

		// Act - processing feature
		var processing = context.GetOrCreateProcessingFeature();
		processing.ProcessingAttempts = 2;
		processing.FirstAttemptTime = time;
		processing.IsRetry = true;

		// Act - validation feature
		var validation = context.GetOrCreateValidationFeature();
		validation.ValidationPassed = true;
		validation.ValidationTimestamp = time;

		// Act - transaction feature
		var transaction = context.GetOrCreateTransactionFeature();
		transaction.Transaction = new object();
		transaction.TransactionId = "tx-1";

		// Act - timeout feature
		var timeout = context.GetOrCreateTimeoutFeature();
		timeout.TimeoutExceeded = true;
		timeout.TimeoutElapsed = TimeSpan.FromSeconds(5);

		// Act - rate limit feature
		var rateLimit = context.GetOrCreateRateLimitFeature();
		rateLimit.RateLimitExceeded = true;
		rateLimit.RateLimitRetryAfter = TimeSpan.FromMinutes(1);

		// Assert
		context.GetProcessingAttempts().ShouldBe(2);
		context.GetFirstAttemptTime().ShouldBe(time);
		context.GetIsRetry().ShouldBeTrue();
		context.GetValidationPassed().ShouldBeTrue();
		context.GetValidationTimestamp().ShouldBe(time);
		context.GetTransaction().ShouldNotBeNull();
		context.GetTransactionId().ShouldBe("tx-1");
		context.GetTimeoutExceeded().ShouldBeTrue();
		context.GetTimeoutElapsed().ShouldBe(TimeSpan.FromSeconds(5));
		context.GetRateLimitExceeded().ShouldBeTrue();
		context.GetRateLimitRetryAfter().ShouldBe(TimeSpan.FromMinutes(1));
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
	public void StoreAndRetrieveItemsInDictionary()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.Items["key1"] = "value1";

		// Act & Assert
		context.Items.ShouldNotBeNull();
		context.Items.ContainsKey("key1").ShouldBeTrue();
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
	public void ContainsItemThrowsForNullKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert -- null key throws ArgumentNullException from the underlying Dictionary
		Should.Throw<ArgumentNullException>(() => context.ContainsItem(null!));
	}

	[Fact]
	public void ContainsItemReturnsFalseForEmptyOrWhiteSpaceKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert -- empty/whitespace keys are valid dictionary keys that simply don't exist
		context.ContainsItem("").ShouldBeFalse();
		context.ContainsItem("   ").ShouldBeFalse();
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
	public void GetItemThrowsForNullKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert -- null key throws ArgumentNullException from the underlying Dictionary
		Should.Throw<ArgumentNullException>(() => context.GetItem<string>(null!));
	}

	[Fact]
	public void GetItemReturnsDefaultForEmptyKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert -- empty key is a valid dictionary key that simply doesn't exist
		context.GetItem<string>("").ShouldBeNull();
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
	public void SetItemStoresNullValueInDictionary()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		context.SetItem("key", "value");

		// Act -- SetItem stores null via the null-forgiving operator; it does not remove the key
		context.SetItem<string?>("key", null);

		// Assert -- the key still exists in Items (with a null value stored),
		// but GetItem returns default(string) = null because the stored null
		// does not match 'is T typed' for reference types
		context.Items.ContainsKey("key").ShouldBeTrue();
		context.GetItem<string>("key").ShouldBeNull();
	}

	[Fact]
	public void SetItemThrowsForNullKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert -- null key throws ArgumentNullException from the underlying Dictionary
		Should.Throw<ArgumentNullException>(() => context.SetItem(null!, "value"));
	}

	[Fact]
	public void SetItemAcceptsEmptyKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert -- empty key is a valid dictionary key
		context.SetItem("", "value");
		context.GetItem<string>("").ShouldBe("value");
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

		// Act & Assert -- should not throw
		context.RemoveItem("nonexistent");
	}

	[Fact]
	public void RemoveItemThrowsForNullKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert -- null key throws ArgumentNullException from the underlying Dictionary
		Should.Throw<ArgumentNullException>(() => context.RemoveItem(null!));
	}

	[Fact]
	public void RemoveItemDoesNothingForEmptyKey()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Act & Assert -- empty key is a valid dictionary key; removing a non-existent key is a no-op
		context.RemoveItem("");
	}

	[Fact]
	public void ResetClearsAllState()
	{
		// Arrange
		var context = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Set identity features
		var identity = context.GetOrCreateIdentityFeature();
		identity.ExternalId = "ext";
		identity.UserId = "user";
		identity.TenantId = "tenant";

		// Set core properties
		context.CorrelationId = "corr";
		context.CausationId = "cause";

		// Set Items
		context.SetItem("key", "value");

		// Act
		context.Reset();

		// Assert - core properties are cleared
		context.CorrelationId.ShouldBeNull();
		context.CausationId.ShouldBeNull();

		// Items should be cleared but dictionary reused
		context.ContainsItem("key").ShouldBeFalse();

		// Features should be cleared
		context.GetExternalId().ShouldBeNull();
		context.GetUserId().ShouldBeNull();
		context.GetTenantId().ShouldBeNull();
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
	public void InitializeSetsServiceProvider()
	{
		// Arrange
		var context = new MessageContext();
		var provider = A.Fake<IServiceProvider>();

		// Act
		context.Initialize(provider);

		// Assert
		context.RequestServices.ShouldBe(provider);
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

		var identity = context.GetOrCreateIdentityFeature();
		identity.TenantId = "tenant-1";
		identity.UserId = "user-1";
		identity.SessionId = "session-1";
		identity.WorkflowId = "workflow-1";
		identity.TraceParent = "trace-1";

		var routing = context.GetOrCreateRoutingFeature();
		routing.Source = "source-1";

		// Act
		var child = context.CreateChildContext();

		// Assert
		child.CorrelationId.ShouldBe("corr-parent");
		child.CausationId.ShouldBe(context.MessageId);
		child.GetTenantId().ShouldBe("tenant-1");
		child.GetUserId().ShouldBe("user-1");
		child.GetSessionId().ShouldBe("session-1");
		child.GetWorkflowId().ShouldBe("workflow-1");
		child.GetTraceParent().ShouldBe("trace-1");
		child.GetSource().ShouldBe("source-1");
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

		// Act & Assert -- no lazy marking, no explicit set -> null
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

		// Act -- marking after explicit set should be a no-op
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

		// Act -- explicit set after lazy marking
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

		// Act -- explicitly set to null
		context.CorrelationId = null;

		// Assert -- should return null, not generate
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

		// Assert -- should equal the lazy-generated CorrelationId
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

		// Assert -- after reset, lazy flags are cleared and values are null
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

		// Act -- reset and re-mark
		context.Reset();
		context.MarkForLazyCorrelation();
		var secondCorrelationId = context.CorrelationId;

		// Assert -- should generate a new value
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

		// Assert -- both should reference the same cached default instance
		ReferenceEquals(context1.VersionMetadata, context2.VersionMetadata).ShouldBeTrue();
	}

	[Fact]
	public void NewContextUsesStaticDefaultValidationResult()
	{
		// Arrange & Act
		var context1 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var context2 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Assert -- both should reference the same cached default instance
		// Cast to object to avoid CS8920 with static abstract members
		ReferenceEquals((object)context1.ValidationResult, (object)context2.ValidationResult).ShouldBeTrue();
	}

	[Fact]
	public void NewContextUsesStaticDefaultAuthorizationResult()
	{
		// Arrange & Act
		var context1 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);
		var context2 = new MessageContext(new FakeDispatchMessage(), _serviceProvider);

		// Assert -- both should reference the same cached default instance
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
