// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="InsertOutboxMessageRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InsertOutboxMessageRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessages]";

	private static OutboundMessage CreateTestMessage()
	{
		var message = new OutboundMessage(
			messageType: "TestMessage",
			payload: "test payload"u8.ToArray(),
			destination: "test-destination",
			headers: new Dictionary<string, object>(StringComparer.Ordinal) { ["key"] = "value" });

		message.Priority = 1;

		return message;
	}

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Arrange
		var message = CreateTestMessage();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new InsertOutboxMessageRequest(null!, message, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Arrange
		var message = CreateTestMessage();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new InsertOutboxMessageRequest("", message, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Arrange
		var message = CreateTestMessage();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new InsertOutboxMessageRequest("   ", message, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessage()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InsertOutboxMessageRequest(TestTableName, null!, null, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Arrange
		var message = CreateTestMessage();

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("INSERT INTO");
		request.Command.CommandText.ShouldContain(TestTableName);
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		var message = CreateTestMessage();
		const int timeout = 60;

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Arrange
		var message = CreateTestMessage();

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Arrange
		var message = CreateTestMessage();

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region Headers Serialization Tests

	[Fact]
	public void CreateCommandWithEmptyHeaders()
	{
		// Arrange
		var message = new OutboundMessage(
			messageType: "TestMessage",
			payload: "test"u8.ToArray(),
			destination: "test-destination");

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateCommandWithMultipleHeaders()
	{
		// Arrange
		var message = new OutboundMessage(
			messageType: "TestMessage",
			payload: "test"u8.ToArray(),
			destination: "test-destination",
			headers: new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["header1"] = "value1",
				["header2"] = "value2"
			});

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion

	#region Optional Parameters Tests

	[Fact]
	public void CreateCommandWithScheduledAt()
	{
		// Arrange
		var message = CreateTestMessage();
		message.ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(30);

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateCommandWithCorrelationId()
	{
		// Arrange
		var message = CreateTestMessage();
		message.CorrelationId = "correlation-123";

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateCommandWithCausationId()
	{
		// Arrange
		var message = CreateTestMessage();
		message.CausationId = "causation-456";

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateCommandWithTenantId()
	{
		// Arrange
		var message = CreateTestMessage();
		message.TenantId = "tenant-789";

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateCommandWithMultiTransport()
	{
		// Arrange
		var message = CreateTestMessage();
		message.IsMultiTransport = true;
		message.TargetTransports = "kafka,rabbitmq";

		// Act
		var request = new InsertOutboxMessageRequest(TestTableName, message, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}
