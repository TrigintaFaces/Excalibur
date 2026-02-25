// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="GetFailedMessagesRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GetFailedMessagesRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetFailedMessagesRequest(null!, maxRetries: 3, olderThan: null, batchSize: 100, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetFailedMessagesRequest("", maxRetries: 3, olderThan: null, batchSize: 100, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetFailedMessagesRequest("   ", maxRetries: 3, olderThan: null, batchSize: 100, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: 3, olderThan: null, batchSize: 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("SELECT TOP");
		request.Command.CommandText.ShouldContain(TestTableName);
	}

	[Fact]
	public void CreateCommandThatFiltersFailedStatuses()
	{
		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: 3, olderThan: null, batchSize: 100, 30, CancellationToken.None);

		// Assert - Status 3 = Failed, Status 4 = PartiallyFailed
		request.Command.CommandText.ShouldContain("Status IN (3, 4)");
	}

	[Fact]
	public void CreateCommandThatFiltersRetryCount()
	{
		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: 5, olderThan: null, batchSize: 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("RetryCount < @MaxRetries");
	}

	[Fact]
	public void CreateCommandWithOlderThanFilter()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddMinutes(-30);

		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: 3, olderThan: olderThan, batchSize: 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("@OlderThan IS NULL OR LastAttemptAt < @OlderThan");
	}

	[Fact]
	public void CreateCommandWithNullOlderThan()
	{
		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: 3, olderThan: null, batchSize: 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateCommandOrderedByLastAttemptAt()
	{
		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: 3, olderThan: null, batchSize: 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("ORDER BY LastAttemptAt ASC");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: 3, olderThan: null, batchSize: 100, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: 3, olderThan: null, batchSize: 100, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region MaxRetries Tests

	[Theory]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	public void AcceptValidMaxRetries(int maxRetries)
	{
		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: maxRetries, olderThan: null, batchSize: 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion

	#region BatchSize Tests

	[Theory]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(100)]
	[InlineData(1000)]
	public void AcceptValidBatchSize(int batchSize)
	{
		// Act
		var request = new GetFailedMessagesRequest(TestTableName, maxRetries: 3, olderThan: null, batchSize: batchSize, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="GetScheduledMessagesRequest"/>.
/// </summary>
public sealed class GetScheduledMessagesRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetScheduledMessagesRequest(null!, DateTimeOffset.UtcNow, 100, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetScheduledMessagesRequest("", DateTimeOffset.UtcNow, 100, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetScheduledMessagesRequest("   ", DateTimeOffset.UtcNow, 100, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Arrange
		var scheduledBefore = DateTimeOffset.UtcNow;

		// Act
		var request = new GetScheduledMessagesRequest(TestTableName, scheduledBefore, 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("SELECT TOP");
		request.Command.CommandText.ShouldContain(TestTableName);
	}

	[Fact]
	public void CreateCommandThatFiltersStagedStatus()
	{
		// Arrange
		var scheduledBefore = DateTimeOffset.UtcNow;

		// Act
		var request = new GetScheduledMessagesRequest(TestTableName, scheduledBefore, 100, 30, CancellationToken.None);

		// Assert - Status = 0 is Staged
		request.Command.CommandText.ShouldContain("Status = 0");
	}

	[Fact]
	public void CreateCommandThatFiltersScheduledMessages()
	{
		// Arrange
		var scheduledBefore = DateTimeOffset.UtcNow;

		// Act
		var request = new GetScheduledMessagesRequest(TestTableName, scheduledBefore, 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("ScheduledAt IS NOT NULL");
		request.Command.CommandText.ShouldContain("ScheduledAt <= @ScheduledBefore");
	}

	[Fact]
	public void CreateCommandOrderedByScheduledAt()
	{
		// Arrange
		var scheduledBefore = DateTimeOffset.UtcNow;

		// Act
		var request = new GetScheduledMessagesRequest(TestTableName, scheduledBefore, 100, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("ORDER BY ScheduledAt ASC");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		var scheduledBefore = DateTimeOffset.UtcNow;
		const int timeout = 60;

		// Act
		var request = new GetScheduledMessagesRequest(TestTableName, scheduledBefore, 100, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Arrange
		var scheduledBefore = DateTimeOffset.UtcNow;

		// Act
		var request = new GetScheduledMessagesRequest(TestTableName, scheduledBefore, 100, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region BatchSize Tests

	[Theory]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(100)]
	[InlineData(1000)]
	public void AcceptValidBatchSize(int batchSize)
	{
		// Arrange
		var scheduledBefore = DateTimeOffset.UtcNow;

		// Act
		var request = new GetScheduledMessagesRequest(TestTableName, scheduledBefore, batchSize, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="UpdateAggregateStatusRequest"/>.
/// </summary>
public sealed class UpdateAggregateStatusRequestShould : UnitTestBase
{
	private const string TestOutboxTableName = "[dbo].[OutboxMessages]";
	private const string TestTransportsTableName = "[dbo].[OutboxMessageTransports]";
	private const string TestMessageId = "msg-12345";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullOutboxTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new UpdateAggregateStatusRequest(null!, TestTransportsTableName, TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyOutboxTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new UpdateAggregateStatusRequest("", TestTransportsTableName, TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceOutboxTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new UpdateAggregateStatusRequest("   ", TestTransportsTableName, TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullTransportsTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new UpdateAggregateStatusRequest(TestOutboxTableName, null!, TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTransportsTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new UpdateAggregateStatusRequest(TestOutboxTableName, "", TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTransportsTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new UpdateAggregateStatusRequest(TestOutboxTableName, "   ", TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, null!, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, "", 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, "   ", 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateCommandThatSelectsFromTransportsTable()
	{
		// Act
		var request = new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain(TestTransportsTableName);
	}

	[Fact]
	public void CreateCommandThatUpdatesOutboxTable()
	{
		// Act
		var request = new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("UPDATE");
		request.Command.CommandText.ShouldContain(TestOutboxTableName);
	}

	[Fact]
	public void CreateCommandWithStatusCalculation()
	{
		// Act
		var request = new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, TestMessageId, 30, CancellationToken.None);

		// Assert - Contains all status mappings
		request.Command.CommandText.ShouldContain("@AllSent = 1 THEN 2"); // Sent
		request.Command.CommandText.ShouldContain("@AnySending = 1 THEN 1"); // Sending
		request.Command.CommandText.ShouldContain("@AllFailed = 1 THEN 3"); // Failed
		request.Command.CommandText.ShouldContain("@AnyFailed = 1 THEN 4"); // PartiallyFailed
	}

	[Fact]
	public void CreateCommandThatSetsSentAtWhenAllSent()
	{
		// Act
		var request = new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("SentAt = CASE WHEN @AllSent = 1 THEN SYSDATETIMEOFFSET() ELSE SentAt END");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, TestMessageId, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Act
		var request = new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new UpdateAggregateStatusRequest(TestOutboxTableName, TestTransportsTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion
}
