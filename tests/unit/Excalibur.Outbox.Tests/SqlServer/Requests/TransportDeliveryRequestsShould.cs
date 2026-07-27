// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for <see cref="InsertTransportDeliveryRequest"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
			new InsertTransportDeliveryRequest(null!, delivery, "tenant-a", null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnEmptyTableName()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new InsertTransportDeliveryRequest("", delivery, "tenant-a", null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnWhitespaceTableName()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new InsertTransportDeliveryRequest("   ", delivery, "tenant-a", null, 30, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullDelivery()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InsertTransportDeliveryRequest(TestTableName, null!, "tenant-a", null, 30, CancellationToken.None));
	}

	#endregion

	#region Command Creation Tests

	[Fact]
	public void CreateCommandWithValidParameters()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, "tenant-a", null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.Command.CommandText.ShouldContain("INSERT INTO");
		request.Command.CommandText.ShouldContain(TestTableName);
	}

	/// <summary>
	/// The INSERT must name the tenant column and bind a value for it.
	/// </summary>
	/// <remarks>
	/// The shipped schema declares <c>TenantId NOT NULL DEFAULT '__untenanted__'</c>. A writer that omits
	/// the column therefore does not fail — SQL Server silently applies the default, and every delivery row
	/// claims the untenanted partition regardless of which tenant its parent message belongs to. That is
	/// wrong data written without an error, and it is exactly what shipped when the schema landed ahead of
	/// this code.
	/// <para>
	/// Passing a tenant to the constructor does not prove any of this: the argument can be accepted and
	/// discarded, and every other test here would still pass. These assertions bind the emitted SQL —
	/// the column named, the parameter bound — so dropping the term is RED rather than silent.
	/// </para>
	/// </remarks>
	[Fact]
	public void NameAndBindTheTenantColumnInTheInsert()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, "tenant-a", null, 30, CancellationToken.None);

		// Assert — the column is in the INSERT list, and a parameter is bound for it.
		request.Command.CommandText.ShouldContain(
			"TenantId",
			Case.Sensitive,
			"the INSERT must name TenantId; omitting it lets the column DEFAULT strand every row in the untenanted partition.");
		request.Command.CommandText.ShouldContain(
			"@TenantId",
			Case.Sensitive,
			"the INSERT must bind a TenantId parameter, not rely on the schema default.");
		((DynamicParameters)request.Command.Parameters).ParameterNames.ShouldContain(
			"TenantId",
			"the tenant term must actually be supplied by the request, not merely mentioned in the SQL text.");
	}

	/// <summary>
	/// An unscoped caller writes the reserved sentinel, never <see langword="null"/> and never empty.
	/// </summary>
	/// <remarks>
	/// LIVENESS beside the safety arm above: asserting only that the column is named would also pass for a
	/// request that bound <c>NULL</c>, which the NOT NULL column would then reject at runtime. Untenanted is
	/// a named partition, not an absent tenant.
	/// </remarks>
	[Fact]
	public void BindTheReservedSentinelWhenTheCallerSuppliesNoTenant()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, null, null, 30, CancellationToken.None);

		// Assert
		((DynamicParameters)request.Command.Parameters).Get<string>("TenantId")
			.ShouldBe("__untenanted__", "an unscoped write must bind the reserved sentinel, never NULL or empty.");
	}

	[Fact]
	public void CreateCommandWithSpecifiedTimeout()
	{
		// Arrange
		var delivery = CreateTestDelivery();
		const int timeout = 60;

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, "tenant-a", null, timeout, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void CreateCommandWithDefaultTimeout()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, "tenant-a", null, 30, CancellationToken.None);

		// Assert
		request.Command.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void SetResolveAsyncDelegate()
	{
		// Arrange
		var delivery = CreateTestDelivery();

		// Act
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, "tenant-a", null, 30, CancellationToken.None);

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
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, "tenant-a", null, 30, CancellationToken.None);

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
		var request = new InsertTransportDeliveryRequest(TestTableName, delivery, "tenant-a", null, 30, CancellationToken.None);

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
