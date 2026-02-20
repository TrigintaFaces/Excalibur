// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Depth coverage tests for <see cref="MessageEnvelope"/>.
/// Covers constructor, items, headers, provider metadata, clone, reset, dispose, child context, and legacy properties.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageEnvelopeDepthShould
{
	[Fact]
	public void DefaultConstructor_SetsMessageIdAndTimestamp()
	{
		// Act
		var envelope = new MessageEnvelope();

		// Assert
		envelope.MessageId.ShouldNotBeNullOrEmpty();
		envelope.ReceivedTimestampUtc.ShouldNotBe(default);
	}

	[Fact]
	public void Constructor_WithMessage_SetsMessageProperty()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		var envelope = new MessageEnvelope(message);

		// Assert
		envelope.Message.ShouldBeSameAs(message);
	}

	[Fact]
	public void Constructor_WithNullMessage_ThrowsArgumentNullException()
	{
		Should.Throw<ArgumentNullException>(() => new MessageEnvelope(null!));
	}

	[Fact]
	public void SetItem_StoresAndRetrieves()
	{
		// Arrange
		var envelope = new MessageEnvelope();

		// Act
		envelope.SetItem("key1", "value1");

		// Assert
		envelope.GetItem<string>("key1").ShouldBe("value1");
	}

	[Fact]
	public void SetItem_WithNull_RemovesItem()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		envelope.SetItem("key1", "value1");

		// Act
		envelope.SetItem<string?>("key1", null);

		// Assert
		envelope.GetItem<string>("key1").ShouldBeNull();
	}

	[Fact]
	public void GetItem_WithDefault_ReturnsDefaultWhenNotFound()
	{
		var envelope = new MessageEnvelope();
		envelope.GetItem("missing", 42).ShouldBe(42);
	}

	[Fact]
	public void GetItem_WithDefault_ReturnsValueWhenFound()
	{
		var envelope = new MessageEnvelope();
		envelope.SetItem("key", 99);
		envelope.GetItem("key", 42).ShouldBe(99);
	}

	[Fact]
	public void GetItem_ThrowsArgumentNullException_WhenKeyIsNull()
	{
		var envelope = new MessageEnvelope();
		Should.Throw<ArgumentNullException>(() => envelope.GetItem<string>(null!));
	}

	[Fact]
	public void SetItem_ThrowsArgumentNullException_WhenKeyIsNull()
	{
		var envelope = new MessageEnvelope();
		Should.Throw<ArgumentNullException>(() => envelope.SetItem<string>(null!, "val"));
	}

	[Fact]
	public void ContainsItem_ReturnsTrueWhenExists()
	{
		var envelope = new MessageEnvelope();
		envelope.SetItem("key", "val");
		envelope.ContainsItem("key").ShouldBeTrue();
	}

	[Fact]
	public void ContainsItem_ReturnsFalseWhenMissing()
	{
		var envelope = new MessageEnvelope();
		envelope.ContainsItem("missing").ShouldBeFalse();
	}

	[Fact]
	public void ContainsItem_ThrowsArgumentNullException_WhenKeyIsNull()
	{
		var envelope = new MessageEnvelope();
		Should.Throw<ArgumentNullException>(() => envelope.ContainsItem(null!));
	}

	[Fact]
	public void RemoveItem_RemovesExistingItem()
	{
		var envelope = new MessageEnvelope();
		envelope.SetItem("key", "val");
		envelope.RemoveItem("key");
		envelope.ContainsItem("key").ShouldBeFalse();
	}

	[Fact]
	public void RemoveItem_ThrowsArgumentNullException_WhenKeyIsNull()
	{
		var envelope = new MessageEnvelope();
		Should.Throw<ArgumentNullException>(() => envelope.RemoveItem(null!));
	}

	[Fact]
	public void SetHeader_StoresAndRetrievesHeader()
	{
		var envelope = new MessageEnvelope();
		envelope.SetHeader("X-Custom", "value");
		envelope.GetHeader("X-Custom").ShouldBe("value");
	}

	[Fact]
	public void SetHeader_WithNull_RemovesHeader()
	{
		var envelope = new MessageEnvelope();
		envelope.SetHeader("X-Custom", "value");
		envelope.SetHeader("X-Custom", null);
		envelope.GetHeader("X-Custom").ShouldBeNull();
	}

	[Fact]
	public void GetHeader_ReturnsNull_WhenMissing()
	{
		var envelope = new MessageEnvelope();
		envelope.GetHeader("missing").ShouldBeNull();
	}

	[Fact]
	public void SetProviderMetadata_StoresAndRetrieves()
	{
		var envelope = new MessageEnvelope();
		envelope.SetProviderMetadata("aws.receipt", "handle-123");
		envelope.GetProviderMetadata<string>("aws.receipt").ShouldBe("handle-123");
	}

	[Fact]
	public void SetProviderMetadata_WithNull_RemovesMetadata()
	{
		var envelope = new MessageEnvelope();
		envelope.SetProviderMetadata("aws.receipt", "handle-123");
		envelope.SetProviderMetadata<string?>("aws.receipt", null);
		envelope.GetProviderMetadata<string>("aws.receipt").ShouldBeNull();
	}

	[Fact]
	public void GetProviderMetadata_ReturnsDefault_WhenMissing()
	{
		var envelope = new MessageEnvelope();
		envelope.GetProviderMetadata<int>("missing").ShouldBe(0);
	}

	[Fact]
	public void Clone_CopiesAllProperties()
	{
		// Arrange
		var envelope = new MessageEnvelope
		{
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			TenantId = "tenant-1",
			UserId = "user-1",
			Source = "test",
			MessageType = "TestType",
			ContentType = "application/json",
			DeliveryCount = 3,
			PartitionKey = "pk-1",
			ReplyTo = "reply-queue",
			ReceiptHandle = "handle-1",
			IsDeadLettered = true,
			DeadLetterReason = "MaxRetries",
			SessionId = "session-1",
			WorkflowId = "workflow-1",
			MessageGroupId = "group-1",
			MessageDeduplicationId = "dedup-1",
			RequestId = "req-1",
			FunctionName = "MyFunc",
			FunctionVersion = "v2",
			CloudProvider = "AWS",
			Region = "us-east-1",
		};
		envelope.SetItem("key1", "val1");
		envelope.SetHeader("X-Test", "headerVal");
		envelope.SetProviderMetadata("meta1", "metaVal");

		// Act
		var clone = envelope.Clone();

		// Assert
		clone.CorrelationId.ShouldBe("corr-1");
		clone.CausationId.ShouldBe("cause-1");
		clone.TenantId.ShouldBe("tenant-1");
		clone.UserId.ShouldBe("user-1");
		clone.Source.ShouldBe("test");
		clone.MessageType.ShouldBe("TestType");
		clone.DeliveryCount.ShouldBe(3);
		clone.PartitionKey.ShouldBe("pk-1");
		clone.IsDeadLettered.ShouldBeTrue();
		clone.DeadLetterReason.ShouldBe("MaxRetries");
		clone.SessionId.ShouldBe("session-1");
		clone.WorkflowId.ShouldBe("workflow-1");
		clone.FunctionName.ShouldBe("MyFunc");
		clone.Region.ShouldBe("us-east-1");
		clone.GetItem<string>("key1").ShouldBe("val1");
		clone.GetHeader("X-Test").ShouldBe("headerVal");
		clone.GetProviderMetadata<string>("meta1").ShouldBe("metaVal");
	}

	[Fact]
	public void Reset_ClearsAllProperties()
	{
		// Arrange
		var envelope = new MessageEnvelope
		{
			CorrelationId = "corr-1",
			TenantId = "tenant-1",
			DeliveryCount = 5,
			IsDeadLettered = true,
			SessionId = "session-1",
			FunctionName = "MyFunc",
			ProcessingAttempts = 3,
			IsRetry = true,
			TimeoutExceeded = true,
			RateLimitExceeded = true,
		};
		envelope.SetItem("key1", "val1");
		envelope.SetHeader("X-Test", "headerVal");
		envelope.SetProviderMetadata("meta1", "metaVal");

		// Act
		envelope.Reset();

		// Assert
		envelope.CorrelationId.ShouldBeNull();
		envelope.TenantId.ShouldBeNull();
		envelope.DeliveryCount.ShouldBe(0);
		envelope.IsDeadLettered.ShouldBeFalse();
		envelope.SessionId.ShouldBeNull();
		envelope.FunctionName.ShouldBeNull();
		envelope.ProcessingAttempts.ShouldBe(0);
		envelope.IsRetry.ShouldBeFalse();
		envelope.TimeoutExceeded.ShouldBeFalse();
		envelope.RateLimitExceeded.ShouldBeFalse();
		envelope.ContainsItem("key1").ShouldBeFalse();
		envelope.GetHeader("X-Test").ShouldBeNull();
		envelope.GetProviderMetadata<string>("meta1").ShouldBeNull();
		envelope.MessageId.ShouldNotBeNullOrEmpty(); // Re-generates
	}

	[Fact]
	public void CreateChildContext_PropagatesCrossCuttingIds()
	{
		// Arrange
		var envelope = new MessageEnvelope
		{
			MessageId = "parent-msg",
			CorrelationId = "corr-1",
			TenantId = "tenant-1",
			UserId = "user-1",
			SessionId = "session-1",
			WorkflowId = "workflow-1",
			TraceParent = "trace-1",
			Source = "test-source",
		};

		// Act
		var child = envelope.CreateChildContext();

		// Assert
		child.CorrelationId.ShouldBe("corr-1");
		child.CausationId.ShouldBe("parent-msg"); // Current becomes cause
		child.TenantId.ShouldBe("tenant-1");
		child.MessageId.ShouldNotBe("parent-msg"); // New ID
		child.MessageId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Dispose_DisposesDisposableItems()
	{
		// Arrange
		var disposable = A.Fake<IDisposable>();
		var envelope = new MessageEnvelope();
		envelope.SetItem("disposable", disposable);

		// Act
		envelope.Dispose();

		// Assert
		A.CallTo(() => disposable.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Dispose_IsIdempotent()
	{
		// Arrange
		var disposable = A.Fake<IDisposable>();
		var envelope = new MessageEnvelope();
		envelope.SetItem("disposable", disposable);

		// Act
		envelope.Dispose();
		envelope.Dispose(); // Second call should not throw

		// Assert
		A.CallTo(() => disposable.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Success_ReturnsFalse_WhenValidationFails()
	{
		// Use concrete implementation because IValidationResult has static abstract members
		var envelope = new MessageEnvelope
		{
			ValidationResult = new TestValidationResult(false),
		};
		envelope.Success.ShouldBeFalse();
	}

	[Fact]
	public void Success_ReturnsFalse_WhenAuthorizationFails()
	{
		var envelope = new MessageEnvelope
		{
			AuthorizationResult = A.Fake<IAuthorizationResult>(o => o.ConfigureFake(f =>
				A.CallTo(() => f.IsAuthorized).Returns(false))),
		};
		envelope.Success.ShouldBeFalse();
	}

	[Fact]
	public void Success_ReturnsTrue_WhenAllPass()
	{
		var envelope = new MessageEnvelope();
		envelope.Success.ShouldBeTrue(); // Default results are all valid
	}

	[Fact]
	public void TraceId_IsAliasForTraceParent()
	{
		var envelope = new MessageEnvelope { TraceId = "trace-123" };
		envelope.TraceParent.ShouldBe("trace-123");
		envelope.TraceId.ShouldBe("trace-123");
	}

	[Fact]
	public void RetryCount_IsAliasForDeliveryCount()
	{
		var envelope = new MessageEnvelope { RetryCount = 5 };
		envelope.DeliveryCount.ShouldBe(5);
		envelope.RetryCount.ShouldBe(5);
	}

	[Fact]
	public void RetryCount_ReturnsNull_WhenDeliveryCountIsZero()
	{
		var envelope = new MessageEnvelope { DeliveryCount = 0 };
		envelope.RetryCount.ShouldBeNull();
	}

	[Fact]
	public void RetryCount_SetNull_SetsDeliveryCountToZero()
	{
		var envelope = new MessageEnvelope { DeliveryCount = 3 };
		envelope.RetryCount = null;
		envelope.DeliveryCount.ShouldBe(0);
	}

	[Fact]
	public void Timestamp_IsAliasForReceivedTimestampUtc()
	{
		var ts = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
		var envelope = new MessageEnvelope { Timestamp = ts };
		envelope.ReceivedTimestampUtc.ShouldBe(ts);
	}

	[Fact]
	public void ScheduledTime_IsAliasForSentTimestampUtc()
	{
		var ts = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
		var envelope = new MessageEnvelope { ScheduledTime = ts };
		envelope.SentTimestampUtc.ShouldBe(ts);
	}

	[Fact]
	public void ValidationResult_SetNull_DefaultsToValid()
	{
		var envelope = new MessageEnvelope();
		envelope.ValidationResult = null!;
		// Avoid ShouldNotBeNull<T>() since IValidationResult has static abstract members (CS8920)
		(envelope.ValidationResult is not null).ShouldBeTrue();
		envelope.ValidationResult.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void AuthorizationResult_SetNull_DefaultsToAuthorized()
	{
		var envelope = new MessageEnvelope();
		envelope.AuthorizationResult = null!;
		envelope.AuthorizationResult.ShouldNotBeNull();
		envelope.AuthorizationResult.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void Headers_AreCaseInsensitive()
	{
		var envelope = new MessageEnvelope();
		envelope.SetHeader("Content-Type", "application/json");
		envelope.GetHeader("content-type").ShouldBe("application/json");
	}

	[Fact]
	public void Items_Dictionary_IsAccessible()
	{
		var envelope = new MessageEnvelope();
		envelope.SetItem("key", "value");
		envelope.Items.ShouldContainKey("key");
	}

	[Fact]
	public void Properties_Dictionary_IsAccessible()
	{
		var envelope = new MessageEnvelope();
		envelope.SetItem("key", "value");
		envelope.Properties.ShouldContainKey("key");
	}

	/// <summary>
	/// Concrete test implementation since IValidationResult has static abstract members and cannot be faked.
	/// </summary>
	private sealed class TestValidationResult : IValidationResult
	{
		public TestValidationResult(bool isValid) => IsValid = isValid;

		public IReadOnlyCollection<object> Errors { get; } = [];
		public bool IsValid { get; }

		public static IValidationResult Failed(params object[] errors) =>
			new TestValidationResult(false);

		public static IValidationResult Success() =>
			new TestValidationResult(true);
	}
}
