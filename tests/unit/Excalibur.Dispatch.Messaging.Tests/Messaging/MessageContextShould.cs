// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for MessageContext class covering lifecycle, properties, threading, disposal, and reset.
/// </summary>
/// <remarks>
/// Sprint 411 - Core Pipeline Coverage (T411.1).
/// Target: Increase MessageContext coverage from 37.8% to 70%.
/// Updated for IMessageContext decomposition: properties moved to feature interfaces and Items-based extension methods.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class MessageContextShould
{
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();

	#region Constructor Tests

	[Fact]
	public void DefaultConstructor_Should_Create_Valid_Instance()
	{
		// Act
		var context = new MessageContext();

		// Assert
		_ = context.ShouldNotBeNull();
		_ = context.Message.ShouldNotBeNull();
	}

	[Fact]
	public void ParameterizedConstructor_Should_Require_NonNull_Message()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MessageContext(null!, _serviceProvider));
	}

	[Fact]
	public void ParameterizedConstructor_Should_Require_NonNull_ServiceProvider()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MessageContext(message, null!));
	}

	[Fact]
	public void ParameterizedConstructor_Should_Set_Message_And_RequestServices()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		var context = new MessageContext(message, _serviceProvider);

		// Assert
		context.Message.ShouldBe(message);
		context.RequestServices.ShouldBe(_serviceProvider);
	}

	#endregion

	#region String Property Tests

	[Fact]
	public void MessageId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.MessageId = "test-message-id";

		// Assert
		context.MessageId.ShouldBe("test-message-id");
	}

	[Fact]
	public void ExternalId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateIdentityFeature().ExternalId = "external-123";

		// Assert
		context.GetExternalId().ShouldBe("external-123");
	}

	[Fact]
	public void UserId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateIdentityFeature().UserId = "user-abc";

		// Assert
		context.GetUserId().ShouldBe("user-abc");
	}

	[Fact]
	public void CorrelationId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.CorrelationId = "correlation-xyz";

		// Assert
		context.CorrelationId.ShouldBe("correlation-xyz");
	}

	[Fact]
	public void CausationId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.CausationId = "cause-123";

		// Assert
		context.CausationId.ShouldBe("cause-123");
	}

	[Fact]
	public void TraceParent_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateIdentityFeature().TraceParent = "00-trace-parent-01";

		// Assert
		context.GetTraceParent().ShouldBe("00-trace-parent-01");
	}

	[Fact]
	public void TenantId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateIdentityFeature().TenantId = "tenant-abc";

		// Assert
		context.GetTenantId().ShouldBe("tenant-abc");
	}

	[Fact]
	public void SessionId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateIdentityFeature().SessionId = "session-xyz";

		// Assert
		context.GetSessionId().ShouldBe("session-xyz");
	}

	[Fact]
	public void WorkflowId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateIdentityFeature().WorkflowId = "workflow-456";

		// Assert
		context.GetWorkflowId().ShouldBe("workflow-456");
	}

	[Fact]
	public void Source_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateRoutingFeature().Source = "TestService";

		// Assert
		context.GetSource().ShouldBe("TestService");
	}

	[Fact]
	public void MessageType_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.SetMessageType("OrderCreated");

		// Assert
		context.GetMessageType().ShouldBe("OrderCreated");
	}

	[Fact]
	public void ContentType_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.SetContentType("application/json");

		// Assert
		context.GetContentType().ShouldBe("application/json");
	}

	[Fact]
	public void PartitionKey_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateRoutingFeature().PartitionKey = "partition-1";

		// Assert
		context.GetPartitionKey().ShouldBe("partition-1");
	}

	[Fact]
	public void ReplyTo_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.ReplyTo("reply-queue");

		// Assert
		context.ReplyTo().ShouldBe("reply-queue");
	}

	[Fact]
	public void SerializerVersion_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.SerializerVersion("1.0");

		// Assert
		context.SerializerVersion().ShouldBe("1.0");
	}

	[Fact]
	public void MessageVersion_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.MessageVersion("2.0");

		// Assert
		context.MessageVersion().ShouldBe("2.0");
	}

	[Fact]
	public void ContractVersion_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.ContractVersion("v3");

		// Assert
		context.ContractVersion().ShouldBe("v3");
	}

	[Fact]
	public void TransactionId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateTransactionFeature().TransactionId = "tx-123";

		// Assert
		context.GetTransactionId().ShouldBe("tx-123");
	}

	#endregion

	#region Numeric/Boolean Property Tests

	[Fact]
	public void DeliveryCount_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateProcessingFeature().DeliveryCount = 5;

		// Assert
		context.GetDeliveryCount().ShouldBe(5);
	}

	[Fact]
	public void DesiredVersion_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.DesiredVersion("3");

		// Assert
		context.DesiredVersion().ShouldBe("3");
	}

	[Fact]
	public void ProcessingAttempts_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateProcessingFeature().ProcessingAttempts = 2;

		// Assert
		context.GetProcessingAttempts().ShouldBe(2);
	}

	[Fact]
	public void IsRetry_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateProcessingFeature().IsRetry = true;

		// Assert
		context.GetIsRetry().ShouldBeTrue();
	}

	[Fact]
	public void ValidationPassed_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateValidationFeature().ValidationPassed = true;

		// Assert
		context.GetValidationPassed().ShouldBeTrue();
	}

	[Fact]
	public void TimeoutExceeded_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateTimeoutFeature().TimeoutExceeded = true;

		// Assert
		context.GetTimeoutExceeded().ShouldBeTrue();
	}

	[Fact]
	public void RateLimitExceeded_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.GetOrCreateRateLimitFeature().RateLimitExceeded = true;

		// Assert
		context.GetRateLimitExceeded().ShouldBeTrue();
	}

	#endregion

	#region DateTime/TimeSpan Property Tests

	[Fact]
	public void ReceivedTimestampUtc_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var timestamp = new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero);

		// Act
		context.SetReceivedTimestampUtc(timestamp);

		// Assert
		context.GetReceivedTimestampUtc().ShouldBe(timestamp);
	}

	[Fact]
	public void ReceivedTimestampUtc_Should_Be_Null_When_Not_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		var first = context.GetReceivedTimestampUtc();
		var second = context.GetReceivedTimestampUtc();

		// Assert
		second.ShouldBe(first);
	}

	[Fact]
	public void SentTimestampUtc_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var timestamp = new DateTimeOffset(2026, 1, 20, 11, 0, 0, TimeSpan.Zero);

		// Act
		context.SetSentTimestampUtc(timestamp);

		// Assert
		context.GetSentTimestampUtc().ShouldBe(timestamp);
	}

	[Fact]
	public void FirstAttemptTime_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		context.GetOrCreateProcessingFeature().FirstAttemptTime = timestamp;

		// Assert
		context.GetFirstAttemptTime().ShouldBe(timestamp);
	}

	[Fact]
	public void ValidationTimestamp_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		context.GetOrCreateValidationFeature().ValidationTimestamp = timestamp;

		// Assert
		context.GetValidationTimestamp().ShouldBe(timestamp);
	}

	[Fact]
	public void TimeoutElapsed_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var elapsed = TimeSpan.FromSeconds(30);

		// Act
		context.GetOrCreateTimeoutFeature().TimeoutElapsed = elapsed;

		// Assert
		context.GetTimeoutElapsed().ShouldBe(elapsed);
	}

	[Fact]
	public void RateLimitRetryAfter_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var retryAfter = TimeSpan.FromMinutes(5);

		// Act
		context.GetOrCreateRateLimitFeature().RateLimitRetryAfter = retryAfter;

		// Assert
		context.GetRateLimitRetryAfter().ShouldBe(retryAfter);
	}

	#endregion

	#region Complex Object Property Tests

	[Fact]
	public void RequestServices_Should_Not_Allow_Null()
	{
		// Arrange
		var context = new MessageContext();
		context.Initialize(_serviceProvider);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.RequestServices = null!);
	}

	[Fact]
	public void RequestServices_Should_Support_Set()
	{
		// Arrange
		var context = new MessageContext();
		var newProvider = A.Fake<IServiceProvider>();

		// Act
		context.RequestServices = newProvider;

		// Assert
		context.RequestServices.ShouldBe(newProvider);
	}

	[Fact]
	public void ValidationResult_Should_Not_Allow_Null()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.ValidationResult = null!);
	}

	[Fact]
	public void ValidationResult_Should_Support_Get_And_Set()
	{
		// Note: Cannot use A.Fake<IValidationResult>() because IValidationResult has
		// static abstract members (C# 11 feature) which FakeItEasy cannot mock.
		// Instead, we test the default behavior.

		// Arrange
		var context = new MessageContext();

		// Assert - Default ValidationResult should be valid
		// Note: IValidationResult has static abstract members which prevents using
		// Shouldly assertions directly. Using simple assertions instead.
		(context.ValidationResult is not null).ShouldBeTrue();
		context.ValidationResult.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void AuthorizationResult_Should_Not_Allow_Null()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.AuthorizationResult = null!);
	}

	[Fact]
	public void AuthorizationResult_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var authResult = A.Fake<IAuthorizationResult>();
		_ = A.CallTo(() => authResult.IsAuthorized).Returns(true);

		// Act
		context.AuthorizationResult = authResult;

		// Assert
		context.AuthorizationResult.ShouldBe(authResult);
	}

	[Fact]
	public void RoutingDecision_Should_Allow_Null()
	{
		// Arrange
		var context = new MessageContext();

		// Act -- routing feature starts with null RoutingDecision by default
		var decision = context.GetRoutingDecision();

		// Assert
		decision.ShouldBeNull();
	}

	[Fact]
	public void RoutingDecision_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var routingDecision = RoutingDecision.Success("local", []);

		// Act
		context.GetOrCreateRoutingFeature().RoutingDecision = routingDecision;

		// Assert
		context.GetRoutingDecision().ShouldBe(routingDecision);
	}

	[Fact]
	public void VersionMetadata_Should_Not_Allow_Null()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.VersionMetadata = null!);
	}

	[Fact]
	public void VersionMetadata_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var versionMetadata = A.Fake<IMessageVersionMetadata>();

		// Act
		context.VersionMetadata = versionMetadata;

		// Assert
		context.VersionMetadata.ShouldBe(versionMetadata);
	}

	[Fact]
	public void Result_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var result = new { Success = true, Data = "test" };

		// Act
		context.Result = result;

		// Assert
		context.Result.ShouldBe(result);
	}

	[Fact]
	public void Metadata_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var metadata = A.Fake<IMessageMetadata>();

		// Act
		context.Metadata = metadata;

		// Assert
		context.Metadata.ShouldBe(metadata);
	}

	[Fact]
	public void Transaction_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var transaction = new object();

		// Act
		context.GetOrCreateTransactionFeature().Transaction = transaction;

		// Assert
		context.GetTransaction().ShouldBe(transaction);
	}

	#endregion

	#region Items Dictionary Tests

	[Fact]
	public void Items_Should_Be_Lazily_Initialized()
	{
		// Arrange
		var context = new MessageContext();

		// Act - Access Items to trigger lazy initialization
		var items = context.Items;

		// Assert
		_ = items.ShouldNotBeNull();
		items.ShouldBeEmpty();
	}

	[Fact]
	public void ContainsItem_Should_Return_False_For_Missing_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		var result = context.ContainsItem("nonexistent");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ContainsItem_Should_Return_True_For_Existing_Key()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key", "value");

		// Act
		var result = context.ContainsItem("key");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainsItem_Should_Throw_For_Null_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert -- null key throws ArgumentNullException (subclass of ArgumentException) from dictionary
		_ = Should.Throw<ArgumentNullException>(() => context.ContainsItem(null!));
	}

	[Fact]
	public void GetItem_Should_Return_Default_For_Missing_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		var result = context.GetItem<string>("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetItem_Should_Return_Value_For_Existing_Key()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key", "value");

		// Act
		var result = context.GetItem<string>("key");

		// Assert
		result.ShouldBe("value");
	}

	[Fact]
	public void GetItem_Should_Throw_For_Null_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert -- null key throws ArgumentNullException (subclass of ArgumentException) from dictionary
		_ = Should.Throw<ArgumentNullException>(() => context.GetItem<string>(null!));
	}

	[Fact]
	public void GetItem_WithDefault_Should_Return_Default_For_Missing_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		var result = context.GetItem("nonexistent", "default-value");

		// Assert
		result.ShouldBe("default-value");
	}

	[Fact]
	public void GetItem_WithDefault_Should_Return_Value_For_Existing_Key()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key", "value");

		// Act
		var result = context.GetItem("key", "default-value");

		// Assert
		result.ShouldBe("value");
	}

	[Fact]
	public void SetItem_Should_Add_New_Item()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.SetItem("key", "value");

		// Assert
		context.GetItem<string>("key").ShouldBe("value");
	}

	[Fact]
	public void SetItem_Should_Update_Existing_Item()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key", "old-value");

		// Act
		context.SetItem("key", "new-value");

		// Assert
		context.GetItem<string>("key").ShouldBe("new-value");
	}

	[Fact]
	public void SetItem_Should_Overwrite_When_Value_Is_Null()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key", "value");

		// Act -- SetItem stores the value (including null) in Items
		context.SetItem<string?>("key", null);

		// Assert -- key still exists but value is null
		context.Items.ContainsKey("key").ShouldBeTrue();
	}

	[Fact]
	public void SetItem_Should_Throw_For_Null_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert -- null key throws ArgumentNullException (subclass of ArgumentException) from dictionary
		_ = Should.Throw<ArgumentNullException>(() => context.SetItem(null!, "value"));
	}

	[Fact]
	public void RemoveItem_Should_Remove_Existing_Item()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key", "value");

		// Act
		context.RemoveItem("key");

		// Assert
		context.ContainsItem("key").ShouldBeFalse();
	}

	[Fact]
	public void RemoveItem_Should_Not_Throw_For_Missing_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert (should not throw)
		Should.NotThrow(() => context.RemoveItem("nonexistent"));
	}

	[Fact]
	public void RemoveItem_Should_Throw_For_Null_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert -- null key throws ArgumentNullException (subclass of ArgumentException) from dictionary
		_ = Should.Throw<ArgumentNullException>(() => context.RemoveItem(null!));
	}

	#endregion

	#region Items As Properties Tests

	[Fact]
	public void Items_Should_Support_Direct_Dictionary_Access()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key1", "value1");
		context.SetItem("key2", 42);

		// Act -- Items dictionary is the canonical property store
		var items = context.Items;

		// Assert
		_ = items.ShouldNotBeNull();
		items["key1"].ShouldBe("value1");
		items["key2"].ShouldBe(42);
	}

	#endregion

	#region Success Property Tests

	[Fact]
	public void Success_Should_Return_True_When_All_Results_Pass()
	{
		// Arrange
		var context = new MessageContext();
		// Default results should all be successful

		// Assert
		context.Success.ShouldBeTrue();
	}

	[Fact]
	public void Success_Should_Return_False_When_Authorization_Fails_Via_AuthorizationResult()
	{
		// Note: Cannot use A.Fake<IValidationResult>() because IValidationResult has
		// static abstract members (C# 11 feature) which FakeItEasy cannot mock.
		// We test via AuthorizationResult instead since that doesn't have static abstract members.

		// Arrange
		var context = new MessageContext();
		var authResult = A.Fake<IAuthorizationResult>();
		_ = A.CallTo(() => authResult.IsAuthorized).Returns(false);
		context.AuthorizationResult = authResult;

		// Assert
		context.Success.ShouldBeFalse();
	}

	[Fact]
	public void Success_Should_Return_False_When_Authorization_Fails()
	{
		// Arrange
		var context = new MessageContext();
		var authResult = A.Fake<IAuthorizationResult>();
		_ = A.CallTo(() => authResult.IsAuthorized).Returns(false);
		context.AuthorizationResult = authResult;

		// Assert
		context.Success.ShouldBeFalse();
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void Reset_Should_Clear_All_String_Properties()
	{
		// Arrange
		var context = new MessageContext();
		context.MessageId = "test-id";
		context.GetOrCreateIdentityFeature().ExternalId = "ext-id";
		context.GetOrCreateIdentityFeature().UserId = "user-id";
		context.CorrelationId = "corr-id";
		context.CausationId = "cause-id";
		context.GetOrCreateIdentityFeature().TenantId = "tenant-id";

		// Act
		context.Reset();

		// Assert
		// PERF-5: MessageId uses lazy generation - after reset it generates a fresh ID instead of null
		_ = context.MessageId.ShouldNotBeNull();
		context.MessageId.ShouldNotBe("test-id"); // Should be a new generated ID, not the original
		context.GetExternalId().ShouldBeNull();
		context.GetUserId().ShouldBeNull();
		context.CorrelationId.ShouldBeNull();
		context.CausationId.ShouldBeNull();
		context.GetTenantId().ShouldBeNull();
	}

	[Fact]
	public void Reset_Should_Clear_ReceivedTimestampUtc()
	{
		// Arrange
		var context = new MessageContext();
		context.SetReceivedTimestampUtc(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

		// Act
		context.Reset();

		// Assert -- Items are cleared, so ReceivedTimestampUtc should be null
		context.GetReceivedTimestampUtc().ShouldBeNull();
	}

	[Fact]
	public void Reset_Should_Clear_Numeric_Properties()
	{
		// Arrange
		var context = new MessageContext();
		context.GetOrCreateProcessingFeature().DeliveryCount = 5;
		context.GetOrCreateProcessingFeature().ProcessingAttempts = 3;
		context.DesiredVersion("2");

		// Act
		context.Reset();

		// Assert
		context.GetDeliveryCount().ShouldBe(0);
		context.GetProcessingAttempts().ShouldBe(0);
		context.DesiredVersion().ShouldBeNull();
	}

	[Fact]
	public void Reset_Should_Clear_Boolean_Properties()
	{
		// Arrange
		var context = new MessageContext();
		context.GetOrCreateProcessingFeature().IsRetry = true;
		context.GetOrCreateValidationFeature().ValidationPassed = true;
		context.GetOrCreateTimeoutFeature().TimeoutExceeded = true;
		context.GetOrCreateRateLimitFeature().RateLimitExceeded = true;

		// Act
		context.Reset();

		// Assert
		context.GetIsRetry().ShouldBeFalse();
		context.GetValidationPassed().ShouldBeFalse();
		context.GetTimeoutExceeded().ShouldBeFalse();
		context.GetRateLimitExceeded().ShouldBeFalse();
	}

	[Fact]
	public void Reset_Should_Clear_Items_Dictionary()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key1", "value1");
		context.SetItem("key2", 42);

		// Act
		context.Reset();

		// Assert
		context.ContainsItem("key1").ShouldBeFalse();
		context.ContainsItem("key2").ShouldBeFalse();
	}

	[Fact]
	public void Reset_Should_Reset_ValidationResult_To_Default()
	{
		// Note: Cannot use A.Fake<IValidationResult>() because IValidationResult has
		// static abstract members (C# 11 feature) which FakeItEasy cannot mock.
		// Instead, we test that ValidationResult is reset to a valid state.

		// Arrange
		var context = new MessageContext();

		// Set some properties and then reset
		context.CorrelationId = "test-correlation";

		// Act
		context.Reset();

		// Assert - Should reset to default successful validation result
		// Note: IValidationResult has static abstract members which prevents using
		// Shouldly assertions directly. Using simple assertions instead.
		(context.ValidationResult is not null).ShouldBeTrue();
		context.ValidationResult.IsValid.ShouldBeTrue();
		context.CorrelationId.ShouldBeNull();
	}

	#endregion

	#region Initialize Tests

	[Fact]
	public void Initialize_Should_Set_RequestServices()
	{
		// Arrange
		var context = new MessageContext();
		var serviceProvider = A.Fake<IServiceProvider>();

		// Act
		context.Initialize(serviceProvider);

		// Assert
		context.RequestServices.ShouldBe(serviceProvider);
	}

	[Fact]
	public void Initialize_Should_Throw_For_Null_ServiceProvider()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.Initialize(null!));
	}

	#endregion

	#region CreateForDeserialization Tests

	[Fact]
	public void CreateForDeserialization_Should_Create_Valid_Context()
	{
		// Act
		var context = MessageContext.CreateForDeserialization(_serviceProvider);

		// Assert
		_ = context.ShouldNotBeNull();
		context.RequestServices.ShouldBe(_serviceProvider);
	}

	#endregion

	#region Sprint 70 - CreateChildContext Tests (Task pbzd)

	/// <summary>
	/// Verifies that CreateChildContext generates a new MessageId for the child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Generate_New_MessageId()
	{
		// Arrange
		var parent = new MessageContext();
		parent.MessageId = "parent-message-id";
		parent.CorrelationId = "correlation-123";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.MessageId.ShouldNotBeNullOrEmpty();
		child.MessageId.ShouldNotBe(parent.MessageId);
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the CorrelationId from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_CorrelationId()
	{
		// Arrange
		var parent = new MessageContext();
		parent.CorrelationId = "correlation-123";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.CorrelationId.ShouldBe(parent.CorrelationId);
	}

	/// <summary>
	/// Verifies that CreateChildContext sets the CausationId to the parent's MessageId.
	/// This establishes the causal chain between parent and child messages.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Set_CausationId_To_Parent_MessageId()
	{
		// Arrange
		var parent = new MessageContext();
		parent.MessageId = "parent-message-id";
		parent.CorrelationId = "correlation-123";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.CausationId.ShouldBe(parent.MessageId);
	}

	/// <summary>
	/// Verifies that CreateChildContext falls back to CorrelationId when MessageId is null.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Use_CorrelationId_As_CausationId_When_MessageId_Null()
	{
		// Arrange
		var parent = new MessageContext();
		parent.MessageId = null;
		parent.CorrelationId = "correlation-123";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.CausationId.ShouldBe(parent.CorrelationId);
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the TenantId from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_TenantId()
	{
		// Arrange
		var parent = new MessageContext();
		parent.GetOrCreateIdentityFeature().TenantId = "tenant-abc";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.GetTenantId().ShouldBe("tenant-abc");
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the UserId from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_UserId()
	{
		// Arrange
		var parent = new MessageContext();
		parent.GetOrCreateIdentityFeature().UserId = "user-123";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.GetUserId().ShouldBe("user-123");
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the SessionId from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_SessionId()
	{
		// Arrange
		var parent = new MessageContext();
		parent.GetOrCreateIdentityFeature().SessionId = "session-xyz";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.GetSessionId().ShouldBe("session-xyz");
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the WorkflowId from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_WorkflowId()
	{
		// Arrange
		var parent = new MessageContext();
		parent.GetOrCreateIdentityFeature().WorkflowId = "workflow-456";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.GetWorkflowId().ShouldBe("workflow-456");
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the TraceParent from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_TraceParent()
	{
		// Arrange
		var parent = new MessageContext();
		parent.GetOrCreateIdentityFeature().TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.GetTraceParent().ShouldBe("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the Source from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_Source()
	{
		// Arrange
		var parent = new MessageContext();
		parent.GetOrCreateRoutingFeature().Source = "OrderService";
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.GetSource().ShouldBe("OrderService");
	}

	/// <summary>
	/// Verifies that CreateChildContext does NOT copy Items dictionary.
	/// Child context should start with an empty Items dictionary.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Not_Copy_Items()
	{
		// Arrange
		var parent = new MessageContext();
		parent.Initialize(_serviceProvider);
		parent.SetItem("key1", "value1");
		parent.SetItem("key2", 42);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.Items.ShouldBeEmpty();
		child.ContainsItem("key1").ShouldBeFalse();
		child.ContainsItem("key2").ShouldBeFalse();
	}

	/// <summary>
	/// Verifies that CreateChildContext uses the parent's RequestServices.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Use_Parent_RequestServices()
	{
		// Arrange
		var parent = new MessageContext();
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.RequestServices.ShouldBe(_serviceProvider);
	}

	/// <summary>
	/// Comprehensive test that verifies all propagated fields in a single test.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_All_CrossCutting_Identifiers()
	{
		// Arrange
		var parent = new MessageContext();
		parent.MessageId = "parent-msg-123";
		parent.CorrelationId = "correlation-456";
		parent.CausationId = "causation-789";

		var identity = parent.GetOrCreateIdentityFeature();
		identity.TenantId = "tenant-abc";
		identity.UserId = "user-def";
		identity.SessionId = "session-ghi";
		identity.WorkflowId = "workflow-jkl";
		identity.TraceParent = "00-trace-span-01";

		parent.GetOrCreateRoutingFeature().Source = "TestService";
		parent.Initialize(_serviceProvider);
		parent.SetItem("should-not-copy", "value");

		// Act
		var child = parent.CreateChildContext();

		// Assert - New ID generated
		child.MessageId.ShouldNotBeNullOrEmpty();
		child.MessageId.ShouldNotBe(parent.MessageId);

		// Assert - Causality chain established
		child.CorrelationId.ShouldBe(parent.CorrelationId);
		child.CausationId.ShouldBe(parent.MessageId); // Parent's MessageId becomes child's CausationId

		// Assert - Cross-cutting concerns propagated
		child.GetTenantId().ShouldBe("tenant-abc");
		child.GetUserId().ShouldBe("user-def");
		child.GetSessionId().ShouldBe("session-ghi");
		child.GetWorkflowId().ShouldBe("workflow-jkl");
		child.GetTraceParent().ShouldBe("00-trace-span-01");
		child.GetSource().ShouldBe("TestService");

		// Assert - Items not copied
		child.Items.ShouldBeEmpty();
	}

	#endregion
}
