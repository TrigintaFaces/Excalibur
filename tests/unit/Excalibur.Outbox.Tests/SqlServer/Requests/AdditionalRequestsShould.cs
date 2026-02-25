// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="MarkTransportSkippedRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MarkTransportSkippedRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessageTransports]";
	private const string TestMessageId = "msg-12345";
	private const string TestTransportName = "kafka";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSkippedRequest(null!, TestMessageId, TestTransportName, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSkippedRequest("", TestMessageId, TestTransportName, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSkippedRequest("   ", TestMessageId, TestTransportName, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSkippedRequest(TestTableName, null!, TestTransportName, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSkippedRequest(TestTableName, "", TestTransportName, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullTransportName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSkippedRequest(TestTableName, TestMessageId, null!, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTransportName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSkippedRequest(TestTableName, TestMessageId, "", null, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new MarkTransportSkippedRequest(TestTableName, TestMessageId, TestTransportName, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("UPDATE");
		request.Command.CommandText.ShouldContain(TestTableName);
		request.Command.CommandText.ShouldContain("Status = 4");
	}

	[Fact]
	public void CreateCommandWithReason()
	{
		// Arrange
		const string reason = "Transport disabled for tenant";

		// Act
		var request = new MarkTransportSkippedRequest(TestTableName, TestMessageId, TestTransportName, reason, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("LastError = @Reason");
	}

	[Fact]
	public void CreateCommandWithNullReason()
	{
		// Act
		var request = new MarkTransportSkippedRequest(TestTableName, TestMessageId, TestTransportName, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateCommandThatSetsAttemptedAt()
	{
		// Act
		var request = new MarkTransportSkippedRequest(TestTableName, TestMessageId, TestTransportName, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("AttemptedAt = @AttemptedAt");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new MarkTransportSkippedRequest(TestTableName, TestMessageId, TestTransportName, null, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new MarkTransportSkippedRequest(TestTableName, TestMessageId, TestTransportName, null, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="GetTransportDeliveriesRequest"/>.
/// </summary>
public sealed class GetTransportDeliveriesRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessageTransports]";
	private const string TestMessageId = "msg-12345";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetTransportDeliveriesRequest(null!, TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetTransportDeliveriesRequest("", TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetTransportDeliveriesRequest("   ", TestMessageId, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetTransportDeliveriesRequest(TestTableName, null!, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetTransportDeliveriesRequest(TestTableName, "", 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetTransportDeliveriesRequest(TestTableName, "   ", 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new GetTransportDeliveriesRequest(TestTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain(TestTableName);
	}

	[Fact]
	public void CreateCommandThatSelectsAllColumns()
	{
		// Act
		var request = new GetTransportDeliveriesRequest(TestTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("Id");
		request.Command.CommandText.ShouldContain("MessageId");
		request.Command.CommandText.ShouldContain("TransportName");
		request.Command.CommandText.ShouldContain("Destination");
		request.Command.CommandText.ShouldContain("Status");
		request.Command.CommandText.ShouldContain("CreatedAt");
		request.Command.CommandText.ShouldContain("AttemptedAt");
		request.Command.CommandText.ShouldContain("SentAt");
		request.Command.CommandText.ShouldContain("RetryCount");
		request.Command.CommandText.ShouldContain("LastError");
		request.Command.CommandText.ShouldContain("TransportMetadata");
	}

	[Fact]
	public void CreateCommandWithWhereClause()
	{
		// Act
		var request = new GetTransportDeliveriesRequest(TestTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("WHERE MessageId = @MessageId");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new GetTransportDeliveriesRequest(TestTableName, TestMessageId, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new GetTransportDeliveriesRequest(TestTableName, TestMessageId, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="GetOutboxStatisticsRequest"/>.
/// </summary>
public sealed class GetOutboxStatisticsRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetOutboxStatisticsRequest(null!, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetOutboxStatisticsRequest("", 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new GetOutboxStatisticsRequest("   ", 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new GetOutboxStatisticsRequest(TestTableName, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain(TestTableName);
	}

	[Fact]
	public void CreateCommandThatCountsAllStatuses()
	{
		// Act
		var request = new GetOutboxStatisticsRequest(TestTableName, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("Status = 0"); // Staged
		request.Command.CommandText.ShouldContain("Status = 1"); // Sending
		request.Command.CommandText.ShouldContain("Status = 2"); // Sent
		request.Command.CommandText.ShouldContain("Status = 3"); // Failed
		request.Command.CommandText.ShouldContain("Status = 4"); // PartiallyFailed
	}

	[Fact]
	public void CreateCommandThatCountsScheduledMessages()
	{
		// Act
		var request = new GetOutboxStatisticsRequest(TestTableName, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("ScheduledAt IS NOT NULL");
	}

	[Fact]
	public void CreateCommandThatGetsOldestUnsentCreatedAt()
	{
		// Act
		var request = new GetOutboxStatisticsRequest(TestTableName, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("MIN");
		request.Command.CommandText.ShouldContain("OldestUnsentCreatedAt");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new GetOutboxStatisticsRequest(TestTableName, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Act
		var request = new GetOutboxStatisticsRequest(TestTableName, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new GetOutboxStatisticsRequest(TestTableName, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion
}
