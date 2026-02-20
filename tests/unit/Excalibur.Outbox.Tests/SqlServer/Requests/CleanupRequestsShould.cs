// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="CleanupSentMessagesRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CleanupSentMessagesRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CleanupSentMessagesRequest(null!, DateTimeOffset.UtcNow, 100, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CleanupSentMessagesRequest("", DateTimeOffset.UtcNow, 100, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CleanupSentMessagesRequest("   ", DateTimeOffset.UtcNow, 100, null, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupSentMessagesRequest(TestTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("DELETE TOP");
		request.Command.CommandText.ShouldContain(TestTableName);
	}

	[Fact]
	public void CreateCommandThatFiltersSentStatus()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupSentMessagesRequest(TestTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert - Status = 2 is Sent
		request.Command.CommandText.ShouldContain("Status = 2");
	}

	[Fact]
	public void CreateCommandThatFiltersOlderThan()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupSentMessagesRequest(TestTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("SentAt < @OlderThan");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);
		const int timeout = 120;

		// Act
		var request = new CleanupSentMessagesRequest(TestTableName, olderThan, 100, null, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupSentMessagesRequest(TestTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupSentMessagesRequest(TestTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region Batch Size Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(1000)]
	[InlineData(10000)]
	public void AcceptValidBatchSize(int batchSize)
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupSentMessagesRequest(TestTableName, olderThan, batchSize, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion

	#region OlderThan Tests

	[Theory]
	[InlineData(-1)]
	[InlineData(-7)]
	[InlineData(-30)]
	[InlineData(-365)]
	public void AcceptValidOlderThanDaysAgo(int daysAgo)
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(daysAgo);

		// Act
		var request = new CleanupSentMessagesRequest(TestTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="CleanupTransportDeliveriesRequest"/>.
/// </summary>
public sealed class CleanupTransportDeliveriesRequestShould : UnitTestBase
{
	private const string TestOutboxTableName = "[dbo].[OutboxMessages]";
	private const string TestTransportsTableName = "[dbo].[OutboxMessageTransports]";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullOutboxTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CleanupTransportDeliveriesRequest(null!, TestTransportsTableName, DateTimeOffset.UtcNow, 100, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyOutboxTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CleanupTransportDeliveriesRequest("", TestTransportsTableName, DateTimeOffset.UtcNow, 100, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceOutboxTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CleanupTransportDeliveriesRequest("   ", TestTransportsTableName, DateTimeOffset.UtcNow, 100, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullTransportsTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CleanupTransportDeliveriesRequest(TestOutboxTableName, null!, DateTimeOffset.UtcNow, 100, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTransportsTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CleanupTransportDeliveriesRequest(TestOutboxTableName, "", DateTimeOffset.UtcNow, 100, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTransportsTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new CleanupTransportDeliveriesRequest(TestOutboxTableName, "   ", DateTimeOffset.UtcNow, 100, null, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupTransportDeliveriesRequest(TestOutboxTableName, TestTransportsTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("DELETE FROM");
		request.Command.CommandText.ShouldContain(TestTransportsTableName);
	}

	[Fact]
	public void CreateCommandWithSubquery()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupTransportDeliveriesRequest(TestOutboxTableName, TestTransportsTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("MessageId IN");
		request.Command.CommandText.ShouldContain("SELECT TOP");
		request.Command.CommandText.ShouldContain(TestOutboxTableName);
	}

	[Fact]
	public void CreateCommandThatFiltersSentStatus()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupTransportDeliveriesRequest(TestOutboxTableName, TestTransportsTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert - Status = 2 is Sent
		request.Command.CommandText.ShouldContain("Status = 2");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);
		const int timeout = 120;

		// Act
		var request = new CleanupTransportDeliveriesRequest(
			TestOutboxTableName, TestTransportsTableName, olderThan, 100, null, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Arrange
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var request = new CleanupTransportDeliveriesRequest(TestOutboxTableName, TestTransportsTableName, olderThan, 100, null, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion
}
