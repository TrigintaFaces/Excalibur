// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for MessageContext class covering lifecycle, properties, threading, disposal, and reset.
/// </summary>
/// <remarks>
/// Sprint 411 - Core Pipeline Coverage (T411.1).
/// Target: Increase MessageContext coverage from 37.8% to 70%.
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
		context.ExternalId = "external-123";

		// Assert
		context.ExternalId.ShouldBe("external-123");
	}

	[Fact]
	public void UserId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.UserId = "user-abc";

		// Assert
		context.UserId.ShouldBe("user-abc");
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
		context.TraceParent = "00-trace-parent-01";

		// Assert
		context.TraceParent.ShouldBe("00-trace-parent-01");
	}

	[Fact]
	public void TenantId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.TenantId = "tenant-abc";

		// Assert
		context.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void SessionId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.SessionId = "session-xyz";

		// Assert
		context.SessionId.ShouldBe("session-xyz");
	}

	[Fact]
	public void WorkflowId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.WorkflowId = "workflow-456";

		// Assert
		context.WorkflowId.ShouldBe("workflow-456");
	}

	[Fact]
	public void Source_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.Source = "TestService";

		// Assert
		context.Source.ShouldBe("TestService");
	}

	[Fact]
	public void MessageType_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.MessageType = "OrderCreated";

		// Assert
		context.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void ContentType_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.ContentType = "application/json";

		// Assert
		context.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void PartitionKey_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.PartitionKey = "partition-1";

		// Assert
		context.PartitionKey.ShouldBe("partition-1");
	}

	[Fact]
	public void ReplyTo_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.ReplyTo = "reply-queue";

		// Assert
		context.ReplyTo.ShouldBe("reply-queue");
	}

	[Fact]
	public void SerializerVersion_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.SerializerVersion = "1.0";

		// Assert
		context.SerializerVersion.ShouldBe("1.0");
	}

	[Fact]
	public void MessageVersion_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.MessageVersion = "2.0";

		// Assert
		context.MessageVersion.ShouldBe("2.0");
	}

	[Fact]
	public void ContractVersion_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.ContractVersion = "v3";

		// Assert
		context.ContractVersion.ShouldBe("v3");
	}

	[Fact]
	public void TransactionId_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.TransactionId = "tx-123";

		// Assert
		context.TransactionId.ShouldBe("tx-123");
	}

	#endregion

	#region Numeric/Boolean Property Tests

	[Fact]
	public void DeliveryCount_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.DeliveryCount = 5;

		// Assert
		context.DeliveryCount.ShouldBe(5);
	}

	[Fact]
	public void DesiredVersion_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.DesiredVersion = 3;

		// Assert
		context.DesiredVersion.ShouldBe(3);
	}

	[Fact]
	public void ProcessingAttempts_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.ProcessingAttempts = 2;

		// Assert
		context.ProcessingAttempts.ShouldBe(2);
	}

	[Fact]
	public void IsRetry_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.IsRetry = true;

		// Assert
		context.IsRetry.ShouldBeTrue();
	}

	[Fact]
	public void ValidationPassed_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.ValidationPassed = true;

		// Assert
		context.ValidationPassed.ShouldBeTrue();
	}

	[Fact]
	public void TimeoutExceeded_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.TimeoutExceeded = true;

		// Assert
		context.TimeoutExceeded.ShouldBeTrue();
	}

	[Fact]
	public void RateLimitExceeded_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();

		// Act
		context.RateLimitExceeded = true;

		// Assert
		context.RateLimitExceeded.ShouldBeTrue();
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
		context.ReceivedTimestampUtc = timestamp;

		// Assert
		context.ReceivedTimestampUtc.ShouldBe(timestamp);
	}

	[Fact]
	public void SentTimestampUtc_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var timestamp = new DateTimeOffset(2026, 1, 20, 11, 0, 0, TimeSpan.Zero);

		// Act
		context.SentTimestampUtc = timestamp;

		// Assert
		context.SentTimestampUtc.ShouldBe(timestamp);
	}

	[Fact]
	public void FirstAttemptTime_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		context.FirstAttemptTime = timestamp;

		// Assert
		context.FirstAttemptTime.ShouldBe(timestamp);
	}

	[Fact]
	public void ValidationTimestamp_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		context.ValidationTimestamp = timestamp;

		// Assert
		context.ValidationTimestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void TimeoutElapsed_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var elapsed = TimeSpan.FromSeconds(30);

		// Act
		context.TimeoutElapsed = elapsed;

		// Assert
		context.TimeoutElapsed.ShouldBe(elapsed);
	}

	[Fact]
	public void RateLimitRetryAfter_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var retryAfter = TimeSpan.FromMinutes(5);

		// Act
		context.RateLimitRetryAfter = retryAfter;

		// Assert
		context.RateLimitRetryAfter.ShouldBe(retryAfter);
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

		// Act
		context.RoutingDecision = null;

		// Assert
		context.RoutingDecision.ShouldBeNull();
	}

	[Fact]
	public void RoutingDecision_Should_Support_Get_And_Set()
	{
		// Arrange
		var context = new MessageContext();
		var routingDecision = RoutingDecision.Success("local", []);

		// Act
		context.RoutingDecision = routingDecision;

		// Assert
		context.RoutingDecision.ShouldBe(routingDecision);
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
		context.Transaction = transaction;

		// Assert
		context.Transaction.ShouldBe(transaction);
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

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.ContainsItem(null!));
	}

	[Fact]
	public void ContainsItem_Should_Throw_For_Empty_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.ContainsItem(""));
	}

	[Fact]
	public void ContainsItem_Should_Throw_For_Whitespace_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.ContainsItem("   "));
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

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.GetItem<string>(null!));
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
	public void SetItem_Should_Remove_Item_When_Value_Is_Null()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key", "value");

		// Act
		context.SetItem<string?>("key", null);

		// Assert
		context.ContainsItem("key").ShouldBeFalse();
	}

	[Fact]
	public void SetItem_Should_Throw_For_Null_Key()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetItem(null!, "value"));
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

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.RemoveItem(null!));
	}

	#endregion

	#region Properties Dictionary Tests

	[Fact]
	public void Properties_Should_Return_Compatible_Dictionary()
	{
		// Arrange
		var context = new MessageContext();
		context.SetItem("key1", "value1");
		context.SetItem("key2", 42);

		// Act
		var properties = context.Properties;

		// Assert
		_ = properties.ShouldNotBeNull();
		properties["key1"].ShouldBe("value1");
		properties["key2"].ShouldBe(42);
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

	[Fact]
	public void Success_Should_Return_False_When_Routing_Fails()
	{
		// Arrange
		var context = new MessageContext();
		var routingDecision = RoutingDecision.Failure("No matching rules");
		context.RoutingDecision = routingDecision;

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
		context.ExternalId = "ext-id";
		context.UserId = "user-id";
		context.CorrelationId = "corr-id";
		context.CausationId = "cause-id";
		context.TenantId = "tenant-id";

		// Act
		context.Reset();

		// Assert
		// PERF-5: MessageId uses lazy generation - after reset it generates a fresh ID instead of null
		_ = context.MessageId.ShouldNotBeNull();
		context.MessageId.ShouldNotBe("test-id"); // Should be a new generated ID, not the original
		context.ExternalId.ShouldBeNull();
		context.UserId.ShouldBeNull();
		context.CorrelationId.ShouldBeNull();
		context.CausationId.ShouldBeNull();
		context.TenantId.ShouldBeNull();
	}

	[Fact]
	public void Reset_Should_Clear_Numeric_Properties()
	{
		// Arrange
		var context = new MessageContext();
		context.DeliveryCount = 5;
		context.ProcessingAttempts = 3;
		context.DesiredVersion = 2;

		// Act
		context.Reset();

		// Assert
		context.DeliveryCount.ShouldBe(0);
		context.ProcessingAttempts.ShouldBe(0);
		context.DesiredVersion.ShouldBeNull();
	}

	[Fact]
	public void Reset_Should_Clear_Boolean_Properties()
	{
		// Arrange
		var context = new MessageContext();
		context.IsRetry = true;
		context.ValidationPassed = true;
		context.TimeoutExceeded = true;
		context.RateLimitExceeded = true;

		// Act
		context.Reset();

		// Assert
		context.IsRetry.ShouldBeFalse();
		context.ValidationPassed.ShouldBeFalse();
		context.TimeoutExceeded.ShouldBeFalse();
		context.RateLimitExceeded.ShouldBeFalse();
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
	public void Initialize_Should_Set_ReceivedTimestamp()
	{
		// Arrange
		var context = new MessageContext();
		var before = DateTimeOffset.UtcNow;

		// Act
		context.Initialize(_serviceProvider);
		var after = DateTimeOffset.UtcNow;

		// Assert
		context.ReceivedTimestampUtc.ShouldBeGreaterThanOrEqualTo(before);
		context.ReceivedTimestampUtc.ShouldBeLessThanOrEqualTo(after);
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
		var parent = new MessageContext
		{
			MessageId = "parent-message-id",
			CorrelationId = "correlation-123",
		};
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
		var parent = new MessageContext
		{
			CorrelationId = "correlation-123",
		};
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
		var parent = new MessageContext
		{
			MessageId = "parent-message-id",
			CorrelationId = "correlation-123",
		};
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
		var parent = new MessageContext
		{
			MessageId = null,
			CorrelationId = "correlation-123",
		};
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
		var parent = new MessageContext
		{
			TenantId = "tenant-abc",
		};
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.TenantId.ShouldBe(parent.TenantId);
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the UserId from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_UserId()
	{
		// Arrange
		var parent = new MessageContext
		{
			UserId = "user-123",
		};
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.UserId.ShouldBe(parent.UserId);
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the SessionId from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_SessionId()
	{
		// Arrange
		var parent = new MessageContext
		{
			SessionId = "session-xyz",
		};
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.SessionId.ShouldBe(parent.SessionId);
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the WorkflowId from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_WorkflowId()
	{
		// Arrange
		var parent = new MessageContext
		{
			WorkflowId = "workflow-456",
		};
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.WorkflowId.ShouldBe(parent.WorkflowId);
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the TraceParent from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_TraceParent()
	{
		// Arrange
		var parent = new MessageContext
		{
			TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
		};
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.TraceParent.ShouldBe(parent.TraceParent);
	}

	/// <summary>
	/// Verifies that CreateChildContext propagates the Source from parent to child.
	/// </summary>
	[Fact]
	public void CreateChildContext_Should_Propagate_Source()
	{
		// Arrange
		var parent = new MessageContext
		{
			Source = "OrderService",
		};
		parent.Initialize(_serviceProvider);

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.Source.ShouldBe(parent.Source);
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
		var parent = new MessageContext
		{
			MessageId = "parent-msg-123",
			CorrelationId = "correlation-456",
			CausationId = "causation-789",
			TenantId = "tenant-abc",
			UserId = "user-def",
			SessionId = "session-ghi",
			WorkflowId = "workflow-jkl",
			TraceParent = "00-trace-span-01",
			Source = "TestService",
		};
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
		child.TenantId.ShouldBe(parent.TenantId);
		child.UserId.ShouldBe(parent.UserId);
		child.SessionId.ShouldBe(parent.SessionId);
		child.WorkflowId.ShouldBe(parent.WorkflowId);
		child.TraceParent.ShouldBe(parent.TraceParent);
		child.Source.ShouldBe(parent.Source);

		// Assert - Items not copied
		child.Items.ShouldBeEmpty();
	}

	#endregion
}
