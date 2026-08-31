// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Validation;

namespace Excalibur.Dispatch.Tests;

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
	public void Clone_CopiesAllProperties()
	{
		// Arrange
		var envelope = new MessageEnvelope
		{
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			TenantId = "tenant-1",
			UserId = "user-1",
			MessageType = "TestType",
			ContentType = "application/json",
			DeliveryCount = 3,
			ReplyTo = "reply-queue",
			ReceiptHandle = "handle-1",
			DeadLetterReason = "MaxRetries",
			MessageGroupId = "group-1",
			MessageDeduplicationId = "dedup-1",
		};
		envelope.SetItem("key1", "val1");
		envelope.SetHeader("X-Test", "headerVal");

		// Act
		var clone = envelope.Clone();

		// Assert
		clone.CorrelationId.ShouldBe("corr-1");
		clone.CausationId.ShouldBe("cause-1");
		clone.TenantId.ShouldBe("tenant-1");
		clone.UserId.ShouldBe("user-1");
		clone.MessageType.ShouldBe("TestType");
		clone.DeliveryCount.ShouldBe(3);
		clone.DeadLetterReason.ShouldBe("MaxRetries");
		clone.GetItem<string>("key1").ShouldBe("val1");
		clone.GetHeader("X-Test").ShouldBe("headerVal");
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
		};
		envelope.SetItem("key1", "val1");
		envelope.SetHeader("X-Test", "headerVal");

		// Act
		envelope.Reset();

		// Assert
		envelope.CorrelationId.ShouldBeNull();
		envelope.TenantId.ShouldBeNull();
		envelope.DeliveryCount.ShouldBe(0);
		envelope.ContainsItem("key1").ShouldBeFalse();
		envelope.GetHeader("X-Test").ShouldBeNull();
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
		};

		// Set identity and routing via Features (the new decomposed model)
		envelope.SetFeature<IMessageIdentityFeature>(new MessageIdentityFeature
		{
			TenantId = "tenant-1",
			UserId = "user-1",
			TraceParent = "trace-1",
		});
		envelope.SetFeature<IMessageRoutingFeature>(new MessageRoutingFeature
		{
		});

		// Act
		var child = envelope.CreateChildContext();

		// Assert
		child.CorrelationId.ShouldBe("corr-1");
		child.CausationId.ShouldBe("parent-msg"); // Current becomes cause
		child.GetTenantId().ShouldBe("tenant-1");
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
