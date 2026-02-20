// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="GetUnsentMessagesRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GetUnsentMessagesRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetUnsentMessagesRequest(null!, 100, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetUnsentMessagesRequest("", 100, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetUnsentMessagesRequest("   ", 100, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("SELECT TOP");
		request.Command.CommandText.ShouldContain(TestTableName);
	}

	[Fact]
	public void CreateCommandThatFiltersCorrectStatuses()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert - Should include Staged (0), Failed (3), PartiallyFailed (4)
		request.Command.CommandText.ShouldContain("Status IN (0, 3, 4)");
	}

	[Fact]
	public void CreateCommandWithScheduleFilter()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("ScheduledAt IS NULL OR ScheduledAt <= @Now");
	}

	[Fact]
	public void CreateCommandWithPriorityOrdering()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("ORDER BY Priority DESC, CreatedAt ASC");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, 100, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region Batch Size Tests

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public void AcceptValidBatchSize(int batchSize)
	{
		// Act
		var request = new GetUnsentMessagesRequest(TestTableName, batchSize, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="OutboxMessageRow"/>.
/// </summary>
public sealed class OutboxMessageRowShould : UnitTestBase
{
	#region Default Value Tests

	[Fact]
	public void HaveEmptyIdByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.Id.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyMessageTypeByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.MessageType.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyPayloadByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.Payload.ShouldBeEmpty();
	}

	[Fact]
	public void HaveNullHeadersByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.Headers.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyDestinationByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.Destination.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveNullScheduledAtByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.ScheduledAt.ShouldBeNull();
	}

	[Fact]
	public void HaveNullSentAtByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.SentAt.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroRetryCountByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.RetryCount.ShouldBe(0);
	}

	[Fact]
	public void HaveNullLastErrorByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.LastError.ShouldBeNull();
	}

	[Fact]
	public void HaveNullLastAttemptAtByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.LastAttemptAt.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCorrelationIdByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCausationIdByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.CausationId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullTenantIdByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.TenantId.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroPriorityByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.Priority.ShouldBe(0);
	}

	[Fact]
	public void HaveNullTargetTransportsByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.TargetTransports.ShouldBeNull();
	}

	[Fact]
	public void HaveFalseIsMultiTransportByDefault()
	{
		// Arrange & Act
		var row = new OutboxMessageRow();

		// Assert
		row.IsMultiTransport.ShouldBeFalse();
	}

	#endregion

	#region Property Setting Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow;
		var scheduledAt = createdAt.AddMinutes(30);
		var sentAt = createdAt.AddMinutes(1);
		var lastAttemptAt = createdAt.AddSeconds(30);

		// Act
		var row = new OutboxMessageRow
		{
			Id = "msg-123",
			MessageType = "TestMessage",
			Payload = "test"u8.ToArray(),
			Headers = """{"key":"value"}""",
			Destination = "queue",
			CreatedAt = createdAt,
			ScheduledAt = scheduledAt,
			SentAt = sentAt,
			Status = 2,
			RetryCount = 3,
			LastError = "Error",
			LastAttemptAt = lastAttemptAt,
			CorrelationId = "corr-1",
			CausationId = "caus-1",
			TenantId = "tenant-1",
			Priority = 5,
			TargetTransports = "kafka,rabbitmq",
			IsMultiTransport = true
		};

		// Assert
		row.Id.ShouldBe("msg-123");
		row.MessageType.ShouldBe("TestMessage");
		row.Payload.ShouldBe("test"u8.ToArray());
		row.Headers.ShouldBe("""{"key":"value"}""");
		row.Destination.ShouldBe("queue");
		row.CreatedAt.ShouldBe(createdAt);
		row.ScheduledAt.ShouldBe(scheduledAt);
		row.SentAt.ShouldBe(sentAt);
		row.Status.ShouldBe(2);
		row.RetryCount.ShouldBe(3);
		row.LastError.ShouldBe("Error");
		row.LastAttemptAt.ShouldBe(lastAttemptAt);
		row.CorrelationId.ShouldBe("corr-1");
		row.CausationId.ShouldBe("caus-1");
		row.TenantId.ShouldBe("tenant-1");
		row.Priority.ShouldBe(5);
		row.TargetTransports.ShouldBe("kafka,rabbitmq");
		row.IsMultiTransport.ShouldBeTrue();
	}

	#endregion
}
