// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="InsertTransportDeliveryRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InsertTransportDeliveryRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessageTransports]";

	private static OutboundMessageTransport CreateTestDelivery() => new()
	{
		Id = Guid.NewGuid().ToString(),
		MessageId = "msg-123",
		TransportName = "kafka",
		Destination = "orders-topic",
		Status = TransportDeliveryStatus.Pending,
		CreatedAt = DateTimeOffset.UtcNow,
		RetryCount = 0
	};

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new InsertTransportDeliveryRequest(null!, delivery, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new InsertTransportDeliveryRequest("", delivery, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new InsertTransportDeliveryRequest("   ", delivery, null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullDelivery()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InsertTransportDeliveryRequest(TestTableName, null!, null, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("INSERT INTO");
		request.Command.CommandText.ShouldContain(TestTableName);
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		var delivery = CreateTestDelivery();
		const int timeout = 60;

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, null, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, null, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region Transport Metadata Tests

	[Fact]
	public void CreateCommandWithTransportMetadata()
	{
		// Arrange
		var delivery = CreateTestDelivery();
		delivery.TransportMetadata = """{"partition":3,"key":"order-123"}""";

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateCommandWithNullTransportMetadata()
	{
		// Arrange
		var delivery = CreateTestDelivery();
		delivery.TransportMetadata = null;

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="MarkTransportSentRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MarkTransportSentRequestShould : UnitTestBase
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
			new MarkTransportSentRequest(null!, TestMessageId, TestTransportName, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSentRequest("", TestMessageId, TestTransportName, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSentRequest("   ", TestMessageId, TestTransportName, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSentRequest(TestTableName, null!, TestTransportName, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSentRequest(TestTableName, "", TestTransportName, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSentRequest(TestTableName, "   ", TestTransportName, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullTransportName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSentRequest(TestTableName, TestMessageId, null!, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTransportName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSentRequest(TestTableName, TestMessageId, "", 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTransportName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportSentRequest(TestTableName, TestMessageId, "   ", 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new MarkTransportSentRequest(TestTableName, TestMessageId, TestTransportName, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("UPDATE");
		request.Command.CommandText.ShouldContain(TestTableName);
		request.Command.CommandText.ShouldContain("Status = 2");
	}

	[Fact]
	public void CreateCommandWithCompositeWhereClause()
	{
		// Act
		var request = new MarkTransportSentRequest(TestTableName, TestMessageId, TestTransportName, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("WHERE MessageId = @MessageId AND TransportName = @TransportName");
	}

	[Fact]
	public void CreateCommandThatSetsSentAt()
	{
		// Act
		var request = new MarkTransportSentRequest(TestTableName, TestMessageId, TestTransportName, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("SentAt = @SentAt");
	}

	[Fact]
	public void CreateCommandThatClearsLastError()
	{
		// Act
		var request = new MarkTransportSentRequest(TestTableName, TestMessageId, TestTransportName, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("LastError = NULL");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new MarkTransportSentRequest(TestTableName, TestMessageId, TestTransportName, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new MarkTransportSentRequest(TestTableName, TestMessageId, TestTransportName, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion
}

/// <summary>
/// Unit tests for <see cref="MarkTransportFailedRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MarkTransportFailedRequestShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessageTransports]";
	private const string TestMessageId = "msg-12345";
	private const string TestTransportName = "kafka";
	private const string TestErrorMessage = "Connection refused";

	#region Constructor Validation Tests

	[Fact]
	public void ThrowOnNullTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportFailedRequest(null!, TestMessageId, TestTransportName, TestErrorMessage, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportFailedRequest("", TestMessageId, TestTransportName, TestErrorMessage, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportFailedRequest("   ", TestMessageId, TestTransportName, TestErrorMessage, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportFailedRequest(TestTableName, null!, TestTransportName, TestErrorMessage, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportFailedRequest(TestTableName, "", TestTransportName, TestErrorMessage, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceMessageId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportFailedRequest(TestTableName, "   ", TestTransportName, TestErrorMessage, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullTransportName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportFailedRequest(TestTableName, TestMessageId, null!, TestErrorMessage, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTransportName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new MarkTransportFailedRequest(TestTableName, TestMessageId, "", TestErrorMessage, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullErrorMessage()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MarkTransportFailedRequest(TestTableName, TestMessageId, TestTransportName, null!, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Act
		var request = new MarkTransportFailedRequest(TestTableName, TestMessageId, TestTransportName, TestErrorMessage, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("UPDATE");
		request.Command.CommandText.ShouldContain(TestTableName);
		request.Command.CommandText.ShouldContain("Status = 3");
	}

	[Fact]
	public void CreateCommandThatIncrementsRetryCount()
	{
		// Act
		var request = new MarkTransportFailedRequest(TestTableName, TestMessageId, TestTransportName, TestErrorMessage, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("RetryCount = RetryCount + 1");
	}

	[Fact]
	public void CreateCommandThatSetsLastError()
	{
		// Act
		var request = new MarkTransportFailedRequest(TestTableName, TestMessageId, TestTransportName, TestErrorMessage, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("LastError = @ErrorMessage");
	}

	[Fact]
	public void CreateCommandThatSetsAttemptedAt()
	{
		// Act
		var request = new MarkTransportFailedRequest(TestTableName, TestMessageId, TestTransportName, TestErrorMessage, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("AttemptedAt = @AttemptedAt");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		const int timeout = 60;

		// Act
		var request = new MarkTransportFailedRequest(TestTableName, TestMessageId, TestTransportName, TestErrorMessage, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Act
		var request = new MarkTransportFailedRequest(TestTableName, TestMessageId, TestTransportName, TestErrorMessage, 30, CancellationToken.None);

		// Assert
		_ = request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion

	#region Empty Error Message Tests

	[Fact]
	public void AcceptEmptyErrorMessage()
	{
		// Act
		var request = new MarkTransportFailedRequest(TestTableName, TestMessageId, TestTransportName, "", 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}
