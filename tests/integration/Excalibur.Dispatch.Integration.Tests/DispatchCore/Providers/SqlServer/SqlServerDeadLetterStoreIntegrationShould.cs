// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data.SqlServer.ErrorHandling;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Integration tests for <see cref="SqlServerDeadLetterStore"/> using TestContainers.
/// Tests real SQL Server database operations for dead letter queue persistence.
/// </summary>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", "Data")]
[Trait("Provider", "SqlServer")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerDeadLetterStoreIntegrationShould : IntegrationTestBase
{
	private readonly SqlServerFixture _sqlFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerDeadLetterStoreIntegrationShould"/> class.
	/// </summary>
	/// <param name="sqlFixture">The SQL Server container fixture.</param>
	public SqlServerDeadLetterStoreIntegrationShould(SqlServerFixture sqlFixture)
	{
		_sqlFixture = sqlFixture;
	}

	/// <summary>
	/// Tests that a dead letter message can be stored and retrieved by ID.
	/// </summary>
	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task StoreAndRetrieveDeadLetterMessage()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();
		var message = CreateTestDeadLetterMessage();

		// Act
		await store.StoreAsync(message, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetByIdAsync(message.MessageId, TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.MessageId.ShouldBe(message.MessageId);
		loaded.MessageType.ShouldBe(message.MessageType);
		loaded.MessageBody.ShouldBe(message.MessageBody);
		loaded.Reason.ShouldBe(message.Reason);
		loaded.ProcessingAttempts.ShouldBe(message.ProcessingAttempts);
	}

	/// <summary>
	/// Tests that retrieving a non-existent message returns null.
	/// </summary>
	[Fact]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task ReturnNullForNonExistentMessage()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();

		// Act
		var loaded = await store.GetByIdAsync("non-existent-id", TestCancellationToken).ConfigureAwait(true);

		// Assert
		loaded.ShouldBeNull();
	}

	/// <summary>
	/// Tests that dead letter messages can be filtered by message type.
	/// </summary>
	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task FilterMessagesByType()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		await ClearAllMessagesAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();

		var message1 = CreateTestDeadLetterMessage(messageType: "OrderCreated");
		var message2 = CreateTestDeadLetterMessage(messageType: "OrderCreated");
		var message3 = CreateTestDeadLetterMessage(messageType: "PaymentFailed");

		await store.StoreAsync(message1, TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(message2, TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(message3, TestCancellationToken).ConfigureAwait(true);

		// Act
		var filter = new DeadLetterFilter { MessageType = "OrderCreated" };
		var results = (await store.GetMessagesAsync(filter, TestCancellationToken).ConfigureAwait(true)).ToList();

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(m => m.MessageType == "OrderCreated");
	}

	/// <summary>
	/// Tests that the count of dead letter messages is correct.
	/// </summary>
	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task CountDeadLetterMessages()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		await ClearAllMessagesAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();

		await store.StoreAsync(CreateTestDeadLetterMessage(), TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(CreateTestDeadLetterMessage(), TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(CreateTestDeadLetterMessage(), TestCancellationToken).ConfigureAwait(true);

		// Act
		var count = await store.GetCountAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		count.ShouldBe(3);
	}

	/// <summary>
	/// Tests that a dead letter message can be deleted.
	/// </summary>
	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task DeleteDeadLetterMessage()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();
		var message = CreateTestDeadLetterMessage();

		await store.StoreAsync(message, TestCancellationToken).ConfigureAwait(true);

		// Act
		var deleted = await store.DeleteAsync(message.MessageId, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetByIdAsync(message.MessageId, TestCancellationToken).ConfigureAwait(true);

		// Assert
		deleted.ShouldBeTrue();
		loaded.ShouldBeNull();
	}

	/// <summary>
	/// Tests that deleting a non-existent message returns false.
	/// </summary>
	[Fact]
	public async Task ReturnFalseWhenDeletingNonExistentMessage()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();

		// Act
		var deleted = await store.DeleteAsync("non-existent-id", TestCancellationToken).ConfigureAwait(true);

		// Assert
		deleted.ShouldBeFalse();
	}

	/// <summary>
	/// Tests that a message can be marked as replayed.
	/// </summary>
	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task MarkMessageAsReplayed()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();
		var message = CreateTestDeadLetterMessage();

		await store.StoreAsync(message, TestCancellationToken).ConfigureAwait(true);

		// Act
		await store.MarkAsReplayedAsync(message.MessageId, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetByIdAsync(message.MessageId, TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.IsReplayed.ShouldBeTrue();
		loaded.ReplayedAt.ShouldNotBeNull();
	}

	/// <summary>
	/// Tests that old messages are cleaned up based on retention days.
	/// </summary>
	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task CleanupOldMessages()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		await ClearAllMessagesAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();

		// Store a message with old timestamp
		var oldMessage = CreateTestDeadLetterMessage();
		oldMessage.MovedToDeadLetterAt = DateTimeOffset.UtcNow.AddDays(-31);
		await store.StoreAsync(oldMessage, TestCancellationToken).ConfigureAwait(true);

		// Store a recent message
		var recentMessage = CreateTestDeadLetterMessage();
		await store.StoreAsync(recentMessage, TestCancellationToken).ConfigureAwait(true);

		// Act - cleanup messages older than 30 days
		var cleanedUp = await store.CleanupOldMessagesAsync(30, TestCancellationToken).ConfigureAwait(true);

		// Assert
		cleanedUp.ShouldBe(1);
		var remaining = await store.GetCountAsync(TestCancellationToken).ConfigureAwait(true);
		remaining.ShouldBe(1);
	}

	/// <summary>
	/// Tests that messages can be filtered by source system.
	/// </summary>
	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task FilterMessagesBySourceSystem()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		await ClearAllMessagesAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();

		var message1 = CreateTestDeadLetterMessage(sourceSystem: "OrderService");
		var message2 = CreateTestDeadLetterMessage(sourceSystem: "OrderService");
		var message3 = CreateTestDeadLetterMessage(sourceSystem: "PaymentService");

		await store.StoreAsync(message1, TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(message2, TestCancellationToken).ConfigureAwait(true);
		await store.StoreAsync(message3, TestCancellationToken).ConfigureAwait(true);

		// Act
		var filter = new DeadLetterFilter { SourceSystem = "OrderService" };
		var results = (await store.GetMessagesAsync(filter, TestCancellationToken).ConfigureAwait(true)).ToList();

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(m => m.SourceSystem == "OrderService");
	}

	/// <summary>
	/// Tests that custom properties are preserved through round-trip.
	/// </summary>
	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task PreserveCustomProperties()
	{
		// Arrange
		await InitializeDeadLetterTableAsync().ConfigureAwait(true);
		var store = CreateDeadLetterStore();
		var message = CreateTestDeadLetterMessage();
		message.Properties["CustomKey"] = "CustomValue";
		message.Properties["AnotherKey"] = "AnotherValue";

		// Act
		await store.StoreAsync(message, TestCancellationToken).ConfigureAwait(true);
		var loaded = await store.GetByIdAsync(message.MessageId, TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = loaded.ShouldNotBeNull();
		loaded.Properties.Count.ShouldBe(2);
		loaded.Properties["CustomKey"].ShouldBe("CustomValue");
		loaded.Properties["AnotherKey"].ShouldBe("AnotherValue");
	}

	private SqlServerDeadLetterStore CreateDeadLetterStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerDeadLetterOptions
		{
			ConnectionString = _sqlFixture.ConnectionString,
			SchemaName = "dbo",
			TableName = "DeadLetterMessages",
		});
		var logger = NullLogger<SqlServerDeadLetterStore>.Instance;
		return new SqlServerDeadLetterStore(options, logger);
	}

	private static DeadLetterMessage CreateTestDeadLetterMessage(
		string? messageType = null,
		string? sourceSystem = null)
	{
		return new DeadLetterMessage
		{
			MessageId = Guid.NewGuid().ToString("N"),
			MessageType = messageType ?? "TestMessageType",
			MessageBody = """{"key":"value"}""",
			MessageMetadata = """{"correlationId":"test-123"}""",
			Reason = "Processing failed after max retries",
			ExceptionDetails = "System.InvalidOperationException: Test failure",
			ProcessingAttempts = 3,
			MovedToDeadLetterAt = DateTimeOffset.UtcNow,
			FirstAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-5),
			LastAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-10),
			SourceSystem = sourceSystem ?? "TestService",
			CorrelationId = Guid.NewGuid().ToString("N"),
		};
	}

	private async Task ClearAllMessagesAsync()
	{
		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		_ = await connection.ExecuteAsync("DELETE FROM [dbo].[DeadLetterMessages]").ConfigureAwait(true);
	}

	private async Task InitializeDeadLetterTableAsync()
	{
		const string createTableSql = """
			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterMessages]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE [dbo].[DeadLetterMessages] (
			        Id NVARCHAR(100) NOT NULL PRIMARY KEY,
			        MessageId NVARCHAR(255) NOT NULL,
			        MessageType NVARCHAR(500) NOT NULL,
			        MessageBody NVARCHAR(MAX) NOT NULL,
			        MessageMetadata NVARCHAR(MAX) NOT NULL,
			        Reason NVARCHAR(MAX) NOT NULL,
			        ExceptionDetails NVARCHAR(MAX) NULL,
			        ProcessingAttempts INT NOT NULL DEFAULT 0,
			        MovedToDeadLetterAt DATETIMEOFFSET NOT NULL,
			        FirstAttemptAt DATETIMEOFFSET NULL,
			        LastAttemptAt DATETIMEOFFSET NULL,
			        IsReplayed BIT NOT NULL DEFAULT 0,
			        ReplayedAt DATETIMEOFFSET NULL,
			        SourceSystem NVARCHAR(255) NULL,
			        CorrelationId NVARCHAR(255) NULL,
			        Properties NVARCHAR(MAX) NULL,
			        INDEX IX_DeadLetterMessages_MessageId (MessageId),
			        INDEX IX_DeadLetterMessages_MessageType (MessageType),
			        INDEX IX_DeadLetterMessages_MovedToDeadLetterAt (MovedToDeadLetterAt)
			    );
			END
			""";

		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		_ = await connection.ExecuteAsync(createTableSql).ConfigureAwait(true);
	}
}
