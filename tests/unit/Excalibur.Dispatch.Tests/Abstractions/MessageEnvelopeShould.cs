// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="MessageEnvelope"/> covering construction, properties, collections,
/// pooling, cloning, and disposal.
/// </summary>
/// <remarks>
/// Sprint 410 - Foundation Coverage Tests (T410.5).
/// Target: Increase MessageEnvelope coverage from 34.1% to 80%.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class MessageEnvelopeShould : IDisposable
{
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();
	private readonly List<MessageEnvelope> _envelopes = [];

	public void Dispose()
	{
		foreach (var envelope in _envelopes)
		{
			envelope.Dispose();
		}
	}

	private MessageEnvelope CreateEnvelope()
	{
		var envelope = new MessageEnvelope();
		_envelopes.Add(envelope);
		return envelope;
	}

	#region Construction Tests

	[Fact]
	public void DefaultConstructor_Should_Initialize_With_NewMessageId()
	{
		// Act
		var envelope = CreateEnvelope();

		// Assert
		envelope.MessageId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(envelope.MessageId, out _).ShouldBeTrue();
	}

	[Fact]
	public void DefaultConstructor_Should_Set_ReceivedTimestampUtc()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var envelope = CreateEnvelope();

		// Assert
		var after = DateTimeOffset.UtcNow;
		envelope.ReceivedTimestampUtc.ShouldBeGreaterThanOrEqualTo(before);
		envelope.ReceivedTimestampUtc.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Constructor_With_Message_Should_Set_Message_Property()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		var envelope = new MessageEnvelope(message);
		_envelopes.Add(envelope);

		// Assert
		envelope.Message.ShouldBe(message);
		envelope.MessageId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Constructor_With_Null_Message_Should_Throw_ArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MessageEnvelope(null!));
	}

	#endregion

	#region Core Property Tests

	[Fact]
	public void Should_Get_And_Set_All_Core_Properties()
	{
		// Arrange
		var envelope = CreateEnvelope();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		envelope.MessageId = "msg-123";
		envelope.ExternalId = "ext-456";
		envelope.UserId = "user-789";
		envelope.CorrelationId = "corr-abc";
		envelope.CausationId = "cause-def";
		envelope.TraceParent = "trace-ghi";
		envelope.SerializerVersion = "1.0";
		envelope.MessageVersion = "2.0";
		envelope.ContractVersion = "3.0";
		envelope.DesiredVersion = 5;
		envelope.TenantId = "tenant-xyz";
		envelope.Source = "source-app";
		envelope.MessageType = "TestMessage";
		envelope.ContentType = "application/json";
		envelope.DeliveryCount = 3;
		envelope.Subject = "Test Subject";
		envelope.Body = "Test Body";
		envelope.PartitionKey = "partition-1";
		envelope.ReplyTo = "reply-queue";
		envelope.ReceivedTimestampUtc = timestamp;
		envelope.SentTimestampUtc = timestamp.AddMinutes(-5);

		// Assert
		envelope.MessageId.ShouldBe("msg-123");
		envelope.ExternalId.ShouldBe("ext-456");
		envelope.UserId.ShouldBe("user-789");
		envelope.CorrelationId.ShouldBe("corr-abc");
		envelope.CausationId.ShouldBe("cause-def");
		envelope.TraceParent.ShouldBe("trace-ghi");
		envelope.SerializerVersion.ShouldBe("1.0");
		envelope.MessageVersion.ShouldBe("2.0");
		envelope.ContractVersion.ShouldBe("3.0");
		envelope.DesiredVersion.ShouldBe(5);
		envelope.TenantId.ShouldBe("tenant-xyz");
		envelope.Source.ShouldBe("source-app");
		envelope.MessageType.ShouldBe("TestMessage");
		envelope.ContentType.ShouldBe("application/json");
		envelope.DeliveryCount.ShouldBe(3);
		envelope.Subject.ShouldBe("Test Subject");
		envelope.Body.ShouldBe("Test Body");
		envelope.PartitionKey.ShouldBe("partition-1");
		envelope.ReplyTo.ShouldBe("reply-queue");
		envelope.ReceivedTimestampUtc.ShouldBe(timestamp);
		envelope.SentTimestampUtc.ShouldBe(timestamp.AddMinutes(-5));
	}

	[Fact]
	public void Legacy_TraceId_Should_Map_To_TraceParent()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		envelope.TraceId = "trace-123";

		// Assert
		envelope.TraceId.ShouldBe("trace-123");
		envelope.TraceParent.ShouldBe("trace-123");

		// And reverse
		envelope.TraceParent = "trace-456";
		envelope.TraceId.ShouldBe("trace-456");
	}

	[Fact]
	public void Legacy_RetryCount_Should_Map_To_DeliveryCount()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		envelope.RetryCount = 5;

		// Assert
		envelope.RetryCount.ShouldBe(5);
		envelope.DeliveryCount.ShouldBe(5);

		// Null returns null when DeliveryCount is 0
		envelope.DeliveryCount = 0;
		envelope.RetryCount.ShouldBeNull();
	}

	[Fact]
	public void Legacy_Timestamp_Should_Map_To_ReceivedTimestampUtc()
	{
		// Arrange
		var envelope = CreateEnvelope();
		var time = DateTimeOffset.UtcNow;

		// Act
		envelope.Timestamp = time;

		// Assert
		envelope.Timestamp.ShouldBe(time);
		envelope.ReceivedTimestampUtc.ShouldBe(time);
	}

	[Fact]
	public void Legacy_ScheduledTime_Should_Map_To_SentTimestampUtc()
	{
		// Arrange
		var envelope = CreateEnvelope();
		var time = DateTimeOffset.UtcNow.AddHours(1);

		// Act
		envelope.ScheduledTime = time;

		// Assert
		envelope.ScheduledTime.ShouldBe(time);
		envelope.SentTimestampUtc.ShouldBe(time);
	}

	#endregion

	#region Cloud Provider Property Tests

	[Fact]
	public void Should_Get_And_Set_CloudProvider_Properties()
	{
		// Arrange
		var envelope = CreateEnvelope();
		var timeout = DateTimeOffset.UtcNow.AddMinutes(5);

		// Act
		envelope.ReceiptHandle = "receipt-123";
		envelope.VisibilityTimeout = timeout;
		envelope.IsDeadLettered = true;
		envelope.DeadLetterReason = "MaxRetries";
		envelope.DeadLetterErrorDescription = "Too many failures";
		envelope.SessionId = "session-456";
		envelope.WorkflowId = "workflow-789";
		envelope.MessageGroupId = "group-abc";
		envelope.MessageDeduplicationId = "dedup-def";

		// Assert
		envelope.ReceiptHandle.ShouldBe("receipt-123");
		envelope.VisibilityTimeout.ShouldBe(timeout);
		envelope.IsDeadLettered.ShouldBeTrue();
		envelope.DeadLetterReason.ShouldBe("MaxRetries");
		envelope.DeadLetterErrorDescription.ShouldBe("Too many failures");
		envelope.SessionId.ShouldBe("session-456");
		envelope.WorkflowId.ShouldBe("workflow-789");
		envelope.MessageGroupId.ShouldBe("group-abc");
		envelope.MessageDeduplicationId.ShouldBe("dedup-def");
	}

	#endregion

	#region Serverless Property Tests

	[Fact]
	public void Should_Get_And_Set_Serverless_Properties()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		envelope.RequestId = "req-123";
		envelope.FunctionName = "MyFunction";
		envelope.FunctionVersion = "1.0.0";
		envelope.CloudProvider = "AWS";
		envelope.Region = "us-east-1";

		// Assert
		envelope.RequestId.ShouldBe("req-123");
		envelope.FunctionName.ShouldBe("MyFunction");
		envelope.FunctionVersion.ShouldBe("1.0.0");
		envelope.CloudProvider.ShouldBe("AWS");
		envelope.Region.ShouldBe("us-east-1");
	}

	#endregion

	#region Hot-Path Property Tests

	[Fact]
	public void Should_Get_And_Set_HotPath_Properties()
	{
		// Arrange
		var envelope = CreateEnvelope();
		var firstAttempt = DateTimeOffset.UtcNow;
		var validationTime = DateTimeOffset.UtcNow;

		// Act
		envelope.ProcessingAttempts = 3;
		envelope.FirstAttemptTime = firstAttempt;
		envelope.IsRetry = true;
		envelope.ValidationPassed = true;
		envelope.ValidationTimestamp = validationTime;
		envelope.Transaction = new object();
		envelope.TransactionId = "txn-123";
		envelope.TimeoutExceeded = true;
		envelope.TimeoutElapsed = TimeSpan.FromSeconds(30);
		envelope.RateLimitExceeded = true;
		envelope.RateLimitRetryAfter = TimeSpan.FromMinutes(1);

		// Assert
		envelope.ProcessingAttempts.ShouldBe(3);
		envelope.FirstAttemptTime.ShouldBe(firstAttempt);
		envelope.IsRetry.ShouldBeTrue();
		envelope.ValidationPassed.ShouldBeTrue();
		envelope.ValidationTimestamp.ShouldBe(validationTime);
		_ = envelope.Transaction.ShouldNotBeNull();
		envelope.TransactionId.ShouldBe("txn-123");
		envelope.TimeoutExceeded.ShouldBeTrue();
		envelope.TimeoutElapsed.ShouldBe(TimeSpan.FromSeconds(30));
		envelope.RateLimitExceeded.ShouldBeTrue();
		envelope.RateLimitRetryAfter.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion

	#region Items Dictionary Tests (IMessageContext)

	[Fact]
	public void SetItem_Should_Add_Item_To_Items_Dictionary()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		envelope.SetItem("key1", "value1");

		// Assert
		envelope.Items.ShouldContainKey("key1");
		envelope.Items["key1"].ShouldBe("value1");
	}

	[Fact]
	public void SetItem_With_Null_Should_Remove_Item()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetItem("key1", "value1");

		// Act
		envelope.SetItem<string?>("key1", null);

		// Assert
		envelope.Items.ShouldNotContainKey("key1");
	}

	[Fact]
	public void GetItem_Should_Return_Value_When_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetItem("key1", 42);

		// Act
		var result = envelope.GetItem<int>("key1");

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public void GetItem_Should_Return_Default_When_Not_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		var result = envelope.GetItem<int>("missing");

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void GetItem_With_DefaultValue_Should_Return_Default_When_Not_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		var result = envelope.GetItem("missing", 99);

		// Assert
		result.ShouldBe(99);
	}

	[Fact]
	public void GetItem_With_DefaultValue_Should_Return_Value_When_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetItem("key1", 42);

		// Act
		var result = envelope.GetItem("key1", 99);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public void ContainsItem_Should_Return_True_When_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetItem("key1", "value1");

		// Act & Assert
		envelope.ContainsItem("key1").ShouldBeTrue();
	}

	[Fact]
	public void ContainsItem_Should_Return_False_When_Not_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act & Assert
		envelope.ContainsItem("missing").ShouldBeFalse();
	}

	[Fact]
	public void RemoveItem_Should_Remove_Existing_Item()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetItem("key1", "value1");

		// Act
		envelope.RemoveItem("key1");

		// Assert
		envelope.ContainsItem("key1").ShouldBeFalse();
	}

	[Fact]
	public void RemoveItem_Should_Not_Throw_When_Item_Not_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act & Assert - should not throw
		Should.NotThrow(() => envelope.RemoveItem("missing"));
	}

	[Fact]
	public void SetItem_Should_Throw_When_Key_Is_Null()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => envelope.SetItem(null!, "value"));
	}

	[Fact]
	public void GetItem_Should_Throw_When_Key_Is_Null()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => envelope.GetItem<string>(null!));
	}

	[Fact]
	public void ContainsItem_Should_Throw_When_Key_Is_Null()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => envelope.ContainsItem(null!));
	}

	[Fact]
	public void RemoveItem_Should_Throw_When_Key_Is_Null()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => envelope.RemoveItem(null!));
	}

	#endregion

	#region Headers Dictionary Tests

	[Fact]
	public void SetHeader_Should_Add_Header()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		envelope.SetHeader("Content-Type", "application/json");

		// Assert
		envelope.Headers.ShouldContainKey("Content-Type");
		envelope.Headers["Content-Type"].ShouldBe("application/json");
	}

	[Fact]
	public void SetHeader_With_Null_Should_Remove_Header()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetHeader("Content-Type", "application/json");

		// Act
		envelope.SetHeader("Content-Type", null);

		// Assert
		envelope.Headers.ShouldNotContainKey("Content-Type");
	}

	[Fact]
	public void GetHeader_Should_Return_Value_When_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetHeader("X-Custom", "value123");

		// Act
		var result = envelope.GetHeader("X-Custom");

		// Assert
		result.ShouldBe("value123");
	}

	[Fact]
	public void GetHeader_Should_Return_Null_When_Not_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		var result = envelope.GetHeader("Missing");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Headers_Should_Be_Case_Insensitive()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetHeader("Content-Type", "application/json");

		// Act & Assert
		envelope.Headers.ShouldContainKey("content-type");
		envelope.Headers["CONTENT-TYPE"].ShouldBe("application/json");
	}

	#endregion

	#region Provider Metadata Tests

	[Fact]
	public void SetProviderMetadata_Should_Add_Metadata()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		envelope.SetProviderMetadata("aws.region", "us-east-1");

		// Assert
		envelope.AllProviderMetadata.ShouldContainKey("aws.region");
		envelope.AllProviderMetadata["aws.region"].ShouldBe("us-east-1");
	}

	[Fact]
	public void SetProviderMetadata_With_Null_Should_Remove_Metadata()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetProviderMetadata("aws.region", "us-east-1");

		// Act
		envelope.SetProviderMetadata<string?>("aws.region", null);

		// Assert
		envelope.AllProviderMetadata.ShouldNotContainKey("aws.region");
	}

	[Fact]
	public void GetProviderMetadata_Should_Return_Value_When_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetProviderMetadata("count", 42);

		// Act
		var result = envelope.GetProviderMetadata<int>("count");

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public void GetProviderMetadata_Should_Return_Default_When_Not_Exists()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		var result = envelope.GetProviderMetadata<int>("missing");

		// Assert
		result.ShouldBe(0);
	}

	#endregion

	#region Validation/Authorization/Routing Result Tests

	// Note: IValidationResult tests are skipped because the interface has static abstract members
	// which cannot be used as type arguments in Shouldly extensions or mocked with FakeItEasy.
	// ValidationResult functionality is tested via Success property tests below.

	[Fact]
	public void AuthorizationResult_Should_Have_Default_Authorized_State()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Assert
		_ = envelope.AuthorizationResult.ShouldNotBeNull();
		envelope.AuthorizationResult.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void AuthorizationResult_Should_Reset_To_Default_When_Set_To_Null()
	{
		// Arrange
		var envelope = CreateEnvelope();
		var customResult = A.Fake<IAuthorizationResult>();
		envelope.AuthorizationResult = customResult;

		// Act
		envelope.AuthorizationResult = null!;

		// Assert
		_ = envelope.AuthorizationResult.ShouldNotBeNull();
		envelope.AuthorizationResult.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void RoutingDecision_Should_Have_Default_Success_State()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Assert
		_ = envelope.RoutingDecision.ShouldNotBeNull();
		envelope.RoutingDecision.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void RoutingDecision_Should_Allow_Null()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Act
		envelope.RoutingDecision = null;

		// Assert
		envelope.RoutingDecision.ShouldBeNull();
	}

	#endregion

	#region Success Property Tests

	[Fact]
	public void Success_Should_Be_True_When_All_Results_Pass()
	{
		// Arrange
		var envelope = CreateEnvelope();
		// Default state - all pass

		// Assert
		envelope.Success.ShouldBeTrue();
	}

	[Fact]
	public void Success_Should_Be_False_When_Authorization_Fails()
	{
		// Arrange
		var envelope = CreateEnvelope();
		var authResult = A.Fake<IAuthorizationResult>();
		_ = A.CallTo(() => authResult.IsAuthorized).Returns(false);
		envelope.AuthorizationResult = authResult;

		// Assert
		envelope.Success.ShouldBeFalse();
	}

	#endregion

	#region CreateChildContext Tests

	[Fact]
	public void CreateChildContext_Should_Generate_New_MessageId()
	{
		// Arrange
		var parent = CreateEnvelope();
		parent.MessageId = "parent-123";

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.MessageId.ShouldNotBeNullOrEmpty();
		child.MessageId.ShouldNotBe(parent.MessageId);
	}

	[Fact]
	public void CreateChildContext_Should_Propagate_CorrelationId()
	{
		// Arrange
		var parent = CreateEnvelope();
		parent.CorrelationId = "correlation-123";

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.CorrelationId.ShouldBe("correlation-123");
	}

	[Fact]
	public void CreateChildContext_Should_Set_CausationId_To_Parent_MessageId()
	{
		// Arrange
		var parent = CreateEnvelope();
		parent.MessageId = "parent-123";

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.CausationId.ShouldBe("parent-123");
	}

	[Fact]
	public void CreateChildContext_Should_Use_CorrelationId_When_MessageId_Is_Null()
	{
		// Arrange
		var parent = CreateEnvelope();
		parent.MessageId = null;
		parent.CorrelationId = "correlation-123";

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.CausationId.ShouldBe("correlation-123");
	}

	[Fact]
	public void CreateChildContext_Should_Propagate_TenantId()
	{
		// Arrange
		var parent = CreateEnvelope();
		parent.TenantId = "tenant-abc";

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void CreateChildContext_Should_Propagate_UserId()
	{
		// Arrange
		var parent = CreateEnvelope();
		parent.UserId = "user-xyz";

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.UserId.ShouldBe("user-xyz");
	}

	[Fact]
	public void CreateChildContext_Should_Propagate_SessionId()
	{
		// Arrange
		var parent = CreateEnvelope();
		parent.SessionId = "session-123";

		// Act
		var child = parent.CreateChildContext();

		// Assert
		(child as MessageEnvelope).SessionId.ShouldBe("session-123");
	}

	[Fact]
	public void CreateChildContext_Should_Propagate_WorkflowId()
	{
		// Arrange
		var parent = CreateEnvelope();
		parent.WorkflowId = "workflow-456";

		// Act
		var child = parent.CreateChildContext();

		// Assert
		(child as MessageEnvelope).WorkflowId.ShouldBe("workflow-456");
	}

	[Fact]
	public void CreateChildContext_Should_Propagate_RequestServices()
	{
		// Arrange
		var parent = CreateEnvelope();
		parent.RequestServices = _serviceProvider;

		// Act
		var child = parent.CreateChildContext();

		// Assert
		child.RequestServices.ShouldBe(_serviceProvider);
	}

	#endregion

	#region Clone Tests

	[Fact]
	public void Clone_Should_Copy_All_Core_Properties()
	{
		// Arrange
		var original = CreateEnvelope();
		original.MessageId = "msg-123";
		original.CorrelationId = "corr-456";
		original.TenantId = "tenant-789";
		original.MessageType = "TestType";
		original.DeliveryCount = 5;

		// Act
		var clone = original.Clone();
		_envelopes.Add(clone);

		// Assert
		clone.MessageId.ShouldBe("msg-123");
		clone.CorrelationId.ShouldBe("corr-456");
		clone.TenantId.ShouldBe("tenant-789");
		clone.MessageType.ShouldBe("TestType");
		clone.DeliveryCount.ShouldBe(5);
	}

	[Fact]
	public void Clone_Should_Copy_Items_Dictionary()
	{
		// Arrange
		var original = CreateEnvelope();
		original.SetItem("key1", "value1");
		original.SetItem("key2", 42);

		// Act
		var clone = original.Clone();
		_envelopes.Add(clone);

		// Assert
		clone.GetItem<string>("key1").ShouldBe("value1");
		clone.GetItem<int>("key2").ShouldBe(42);
	}

	[Fact]
	public void Clone_Should_Copy_Headers_Dictionary()
	{
		// Arrange
		var original = CreateEnvelope();
		original.SetHeader("X-Custom", "header-value");

		// Act
		var clone = original.Clone();
		_envelopes.Add(clone);

		// Assert
		clone.GetHeader("X-Custom").ShouldBe("header-value");
	}

	[Fact]
	public void Clone_Should_Copy_ProviderMetadata_Dictionary()
	{
		// Arrange
		var original = CreateEnvelope();
		original.SetProviderMetadata("aws.region", "us-west-2");

		// Act
		var clone = original.Clone();
		_envelopes.Add(clone);

		// Assert
		clone.GetProviderMetadata<string>("aws.region").ShouldBe("us-west-2");
	}

	[Fact]
	public void Clone_Modifications_Should_Not_Affect_Original()
	{
		// Arrange
		var original = CreateEnvelope();
		original.MessageId = "original-id";
		original.SetItem("key1", "original-value");

		// Act
		var clone = original.Clone();
		_envelopes.Add(clone);
		clone.MessageId = "clone-id";
		clone.SetItem("key1", "clone-value");

		// Assert
		original.MessageId.ShouldBe("original-id");
		original.GetItem<string>("key1").ShouldBe("original-value");
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void Reset_Should_Generate_New_MessageId()
	{
		// Arrange
		var envelope = CreateEnvelope();
		var originalId = envelope.MessageId;

		// Act
		envelope.Reset();

		// Assert
		envelope.MessageId.ShouldNotBe(originalId);
		envelope.MessageId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Reset_Should_Clear_All_Nullable_Properties()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.CorrelationId = "corr-123";
		envelope.CausationId = "cause-456";
		envelope.TenantId = "tenant-789";
		envelope.UserId = "user-abc";

		// Act
		envelope.Reset();

		// Assert
		envelope.CorrelationId.ShouldBeNull();
		envelope.CausationId.ShouldBeNull();
		envelope.TenantId.ShouldBeNull();
		envelope.UserId.ShouldBeNull();
	}

	[Fact]
	public void Reset_Should_Clear_Items_Dictionary()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetItem("key1", "value1");

		// Act
		envelope.Reset();

		// Assert
		envelope.Items.ShouldBeEmpty();
	}

	[Fact]
	public void Reset_Should_Clear_Headers_Dictionary()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetHeader("X-Custom", "value");

		// Act
		envelope.Reset();

		// Assert
		envelope.Headers.ShouldBeEmpty();
	}

	[Fact]
	public void Reset_Should_Clear_ProviderMetadata_Dictionary()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetProviderMetadata("key", "value");

		// Act
		envelope.Reset();

		// Assert
		envelope.AllProviderMetadata.ShouldBeEmpty();
	}

	[Fact]
	public void Reset_Should_Reset_DeliveryCount_To_Zero()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.DeliveryCount = 5;

		// Act
		envelope.Reset();

		// Assert
		envelope.DeliveryCount.ShouldBe(0);
	}

	[Fact]
	public void Reset_Should_Reset_HotPath_Properties()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.ProcessingAttempts = 3;
		envelope.IsRetry = true;
		envelope.ValidationPassed = true;
		envelope.TimeoutExceeded = true;
		envelope.RateLimitExceeded = true;

		// Act
		envelope.Reset();

		// Assert
		envelope.ProcessingAttempts.ShouldBe(0);
		envelope.IsRetry.ShouldBeFalse();
		envelope.ValidationPassed.ShouldBeFalse();
		envelope.TimeoutExceeded.ShouldBeFalse();
		envelope.RateLimitExceeded.ShouldBeFalse();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_Should_Clear_Collections()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		envelope.SetItem("key1", "value1");
		envelope.SetHeader("X-Custom", "value");
		envelope.SetProviderMetadata("meta", "data");

		// Act
		envelope.Dispose();

		// Assert
		envelope.Items.ShouldBeEmpty();
		envelope.Headers.ShouldBeEmpty();
		envelope.AllProviderMetadata.ShouldBeEmpty();
	}

	[Fact]
	public void Dispose_Should_Dispose_Disposable_Items()
	{
		// Arrange
		var disposableItem = A.Fake<IDisposable>();
		var envelope = new MessageEnvelope();
		envelope.SetItem("disposable", disposableItem);

		// Act
		envelope.Dispose();

		// Assert
		_ = A.CallTo(() => disposableItem.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Dispose_Should_Be_Idempotent()
	{
		// Arrange
		var disposableItem = A.Fake<IDisposable>();
		var envelope = new MessageEnvelope();
		envelope.SetItem("disposable", disposableItem);

		// Act
		envelope.Dispose();
		envelope.Dispose();
		envelope.Dispose();

		// Assert - should only dispose once
		_ = A.CallTo(() => disposableItem.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Dispose_Should_Not_Throw_When_Item_Dispose_Throws()
	{
		// Arrange
		var disposableItem = A.Fake<IDisposable>();
		_ = A.CallTo(() => disposableItem.Dispose()).Throws<InvalidOperationException>();
		var envelope = new MessageEnvelope();
		envelope.SetItem("disposable", disposableItem);

		// Act & Assert - should not throw
		Should.NotThrow(() => envelope.Dispose());
	}

	#endregion

	#region Channel Support Tests

	[Fact]
	public async Task AcknowledgeAsync_Should_Invoke_Callback()
	{
		// Arrange
		var acknowledged = false;
		var envelope = CreateEnvelope();
		envelope.AcknowledgeAsync = () =>
		{
			acknowledged = true;
			return Task.CompletedTask;
		};

		// Act
		await envelope.AcknowledgeAsync();

		// Assert
		acknowledged.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectAsync_Should_Invoke_Callback_With_Reason()
	{
		// Arrange
		string? rejectedReason = null;
		var envelope = CreateEnvelope();
		envelope.RejectAsync = reason =>
		{
			rejectedReason = reason;
			return Task.CompletedTask;
		};

		// Act
		await envelope.RejectAsync("Test rejection");

		// Assert
		rejectedReason.ShouldBe("Test rejection");
	}

	#endregion

	#region Properties Dictionary Tests

	[Fact]
	public void Properties_Should_Return_Items_As_NullableValues()
	{
		// Arrange
		var envelope = CreateEnvelope();
		envelope.SetItem("key1", "value1");
		envelope.SetItem("key2", 42);

		// Act
		var properties = envelope.Properties;

		// Assert
		properties.ShouldContainKey("key1");
		properties["key1"].ShouldBe("value1");
		properties.ShouldContainKey("key2");
		properties["key2"].ShouldBe(42);
	}

	#endregion

	#region VersionMetadata Tests

	[Fact]
	public void VersionMetadata_Should_Have_Default_Values()
	{
		// Arrange
		var envelope = CreateEnvelope();

		// Assert
		_ = envelope.VersionMetadata.ShouldNotBeNull();
		envelope.VersionMetadata.Version.ShouldBe(1);
		envelope.VersionMetadata.SchemaVersion.ShouldBe(1);
		envelope.VersionMetadata.SerializerVersion.ShouldBe(1);
	}

	[Fact]
	public void VersionMetadata_Should_Accept_Custom_Metadata()
	{
		// Arrange
		var envelope = CreateEnvelope();
		var customMetadata = A.Fake<IMessageVersionMetadata>();
		_ = A.CallTo(() => customMetadata.Version).Returns(5);

		// Act
		envelope.VersionMetadata = customMetadata;

		// Assert
		envelope.VersionMetadata.Version.ShouldBe(5);
	}

	#endregion
}
