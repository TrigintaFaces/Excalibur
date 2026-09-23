// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.Inbox;

/// <summary>
/// Unit tests for the <see cref="InboxEntry"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class InboxEntryShould
{
	/// <remarks>
	/// Every inbox store persists a dedicated correlation field alongside the metadata bag, and every store
	/// builds its entry from this constructor. The writers that produce the bag put the correlation id in
	/// it under this key rather than assigning the property, so if the constructor does not lift it the
	/// dedicated field is written null for every entry the framework creates -- and a query filtering the
	/// inbox on correlation returns nothing rather than reporting that it cannot answer.
	/// </remarks>
	[Fact]
	public void ParameterizedConstructor_Should_LiftCorrelationIdOutOfTheMetadata()
	{
		var entry = new InboxEntry(
			"msg-1",
			"handler-1",
			"type-1",
			[1],
			new Dictionary<string, object>(StringComparer.Ordinal) { ["CorrelationId"] = "corr-7" });

		entry.CorrelationId.ShouldBe(
			"corr-7",
			"the correlation id the caller supplied is the value the store writes to its correlation "
			+ "field, and it only gets there through this property");
	}

	[Fact]
	public void ParameterizedConstructor_Should_LeaveCorrelationIdNull_WhenTheMetadataCarriesNone()
	{
		var entry = new InboxEntry(
			"msg-1",
			"handler-1",
			"type-1",
			[1],
			new Dictionary<string, object>(StringComparer.Ordinal) { ["MessageType"] = "type-1" });

		entry.CorrelationId.ShouldBeNull(
			"inventing a correlation id would be worse than reporting none, so the absence must survive");
	}

	[Fact]
	public void ParameterizedConstructor_Should_LeaveCorrelationIdNull_WhenTheMetadataValueIsNotUsableAsOne()
	{
		var entry = new InboxEntry(
			"msg-1",
			"handler-1",
			"type-1",
			[1],
			new Dictionary<string, object>(StringComparer.Ordinal) { ["CorrelationId"] = "   " });

		entry.CorrelationId.ShouldBeNull(
			"a blank value identifies nothing, and storing it would make the field look populated");
	}

	/// <remarks>
	/// Same lift, same reason, as <see cref="ParameterizedConstructor_Should_LiftCorrelationIdOutOfTheMetadata"/>:
	/// InboxMiddleware.ExtractMetadata writes CausationId into the bag rather than assigning the property.
	/// </remarks>
	[Fact]
	public void ParameterizedConstructor_Should_LiftCausationIdOutOfTheMetadata()
	{
		var entry = new InboxEntry(
			"msg-1",
			"handler-1",
			"type-1",
			[1],
			new Dictionary<string, object>(StringComparer.Ordinal) { ["CausationId"] = "cause-7" });

		entry.CausationId.ShouldBe(
			"cause-7",
			"the causation id the caller supplied is the value the store writes to its causation "
			+ "field, and it only gets there through this property");
	}

	[Fact]
	public void ParameterizedConstructor_Should_LeaveCausationIdNull_WhenTheMetadataCarriesNone()
	{
		var entry = new InboxEntry(
			"msg-1",
			"handler-1",
			"type-1",
			[1],
			new Dictionary<string, object>(StringComparer.Ordinal) { ["MessageType"] = "type-1" });

		entry.CausationId.ShouldBeNull(
			"inventing a causation id would be worse than reporting none, so the absence must survive");
	}

	[Fact]
	public void ParameterizedConstructor_Should_LeaveCausationIdNull_WhenTheMetadataValueIsNotUsableAsOne()
	{
		var entry = new InboxEntry(
			"msg-1",
			"handler-1",
			"type-1",
			[1],
			new Dictionary<string, object>(StringComparer.Ordinal) { ["CausationId"] = "   " });

		entry.CausationId.ShouldBeNull(
			"a blank value identifies nothing, and storing it would make the field look populated");
	}

	[Fact]
	public void DefaultConstructor_Should_SetDefaults()
	{
		// Act
		var entry = new InboxEntry();

		// Assert
		entry.ReceivedAt.ShouldNotBe(default);
		entry.Status.ShouldBe(InboxStatus.Received);
	}

	[Fact]
	public void ParameterizedConstructor_Should_SetAllValues()
	{
		// Arrange
		byte[] payload = [1, 2, 3];

		// Act
		var entry = new InboxEntry("msg-1", "MyHandler", "OrderCreated", payload);

		// Assert
		entry.MessageId.ShouldBe("msg-1");
		entry.HandlerType.ShouldBe("MyHandler");
		entry.MessageType.ShouldBe("OrderCreated");
		entry.Payload.ShouldBe(payload);
		entry.Status.ShouldBe(InboxStatus.Received);
		entry.Metadata.ShouldNotBeNull();
	}

	[Fact]
	public void ParameterizedConstructor_Should_ThrowOnNullMessageId()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InboxEntry(null!, "handler", "type", [1]));
	}

	[Fact]
	public void ParameterizedConstructor_Should_ThrowOnNullHandlerType()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InboxEntry("msg-1", null!, "type", [1]));
	}

	[Fact]
	public void ParameterizedConstructor_Should_ThrowOnNullMessageType()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InboxEntry("msg-1", "handler", null!, [1]));
	}

	[Fact]
	public void ParameterizedConstructor_Should_ThrowOnNullPayload()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InboxEntry("msg-1", "handler", "type", null!));
	}

	[Fact]
	public void ParameterizedConstructor_Should_CreateDefaultMetadata_WhenNull()
	{
		// Act
		var entry = new InboxEntry("msg-1", "handler", "type", [1], null);

		// Assert
		entry.Metadata.ShouldNotBeNull();
		entry.Metadata.Count.ShouldBe(0);
	}

	[Fact]
	public void MarkProcessing_Should_UpdateStatusAndTimestamp()
	{
		// Arrange
		var entry = new InboxEntry();

		// Act
		entry.MarkProcessing();

		// Assert
		entry.Status.ShouldBe(InboxStatus.Processing);
		entry.LastAttemptAt.ShouldNotBeNull();
	}

	[Fact]
	public void MarkProcessed_Should_UpdateStatusAndClearError()
	{
		// Arrange
		var entry = new InboxEntry { LastError = "previous" };

		// Act
		entry.MarkProcessed();

		// Assert
		entry.Status.ShouldBe(InboxStatus.Processed);
		entry.ProcessedAt.ShouldNotBeNull();
		entry.LastError.ShouldBeNull();
	}

	[Fact]
	public void MarkFailed_Should_IncrementRetryCount()
	{
		// Arrange
		var entry = new InboxEntry();

		// Act
		entry.MarkFailed("timeout");

		// Assert
		entry.Status.ShouldBe(InboxStatus.Failed);
		entry.LastError.ShouldBe("timeout");
		entry.RetryCount.ShouldBe(1);
		entry.LastAttemptAt.ShouldNotBeNull();
	}

	[Fact]
	public void MarkFailed_Should_ThrowOnNullOrEmptyError()
	{
		var entry = new InboxEntry();
		Should.Throw<ArgumentException>(() => entry.MarkFailed(null!));
		Should.Throw<ArgumentException>(() => entry.MarkFailed(string.Empty));
	}

	[Fact]
	public void IsEligibleForRetry_Should_ReturnFalse_WhenNotFailed()
	{
		// Arrange
		var entry = new InboxEntry();

		// Act & Assert
		entry.IsEligibleForRetry().ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_Should_ReturnFalse_WhenMaxRetriesReached()
	{
		// Arrange
		var entry = new InboxEntry { Status = InboxStatus.Failed, RetryCount = 3 };

		// Act & Assert
		entry.IsEligibleForRetry(maxRetries: 3).ShouldBeFalse();
	}

	[Fact]
	public void IsEligibleForRetry_Should_ReturnTrue_WhenFailedAndUnderMax()
	{
		// Arrange
		var entry = new InboxEntry { Status = InboxStatus.Failed, RetryCount = 1 };

		// Act & Assert
		entry.IsEligibleForRetry(maxRetries: 3).ShouldBeTrue();
	}

	[Fact]
	public void ToString_Should_IncludeRelevantInfo()
	{
		// Arrange
		var entry = new InboxEntry("msg-123", "OrderHandler", "OrderCreated", [1]);

		// Act
		var result = entry.ToString();

		// Assert
		result.ShouldContain("msg-123");
		result.ShouldContain("OrderHandler");
		result.ShouldContain("OrderCreated");
		result.ShouldContain("Received");
	}
}
