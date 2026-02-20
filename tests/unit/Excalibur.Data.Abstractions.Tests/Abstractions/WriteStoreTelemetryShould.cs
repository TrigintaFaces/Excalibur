// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Observability;

using FakeItEasy;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="WriteStoreTelemetry"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "Observability")]
public sealed class WriteStoreTelemetryShould : UnitTestBase
{
	#region Constants Tests

	[Fact]
	public void Have_MeterName_Constant()
	{
		// Assert
		WriteStoreTelemetry.MeterName.ShouldBe("Excalibur.Dispatch.WriteStores");
	}

	[Fact]
	public void Have_MeterVersion_Constant()
	{
		// Assert
		WriteStoreTelemetry.MeterVersion.ShouldBe("1.0.0");
	}

	#endregion

	#region Stores Constants Tests

	[Fact]
	public void Stores_Have_EventStore_Constant()
	{
		// Assert
		WriteStoreTelemetry.Stores.EventStore.ShouldBe("event_store");
	}

	[Fact]
	public void Stores_Have_SnapshotStore_Constant()
	{
		// Assert
		WriteStoreTelemetry.Stores.SnapshotStore.ShouldBe("snapshot_store");
	}

	[Fact]
	public void Stores_Have_OutboxStore_Constant()
	{
		// Assert
		WriteStoreTelemetry.Stores.OutboxStore.ShouldBe("outbox_store");
	}

	[Fact]
	public void Stores_Have_InboxStore_Constant()
	{
		// Assert
		WriteStoreTelemetry.Stores.InboxStore.ShouldBe("inbox_store");
	}

	[Fact]
	public void Stores_Have_SagaStore_Constant()
	{
		// Assert
		WriteStoreTelemetry.Stores.SagaStore.ShouldBe("saga_store");
	}

	[Fact]
	public void Stores_Have_SagaTimeoutStore_Constant()
	{
		// Assert
		WriteStoreTelemetry.Stores.SagaTimeoutStore.ShouldBe("saga_timeout_store");
	}

	[Fact]
	public void Stores_Have_CdcStateStore_Constant()
	{
		// Assert
		WriteStoreTelemetry.Stores.CdcStateStore.ShouldBe("cdc_state_store");
	}

	[Fact]
	public void Stores_Have_DeadLetterStore_Constant()
	{
		// Assert
		WriteStoreTelemetry.Stores.DeadLetterStore.ShouldBe("dead_letter_store");
	}

	#endregion

	#region Providers Constants Tests

	[Fact]
	public void Providers_Have_SqlServer_Constant()
	{
		// Assert
		WriteStoreTelemetry.Providers.SqlServer.ShouldBe("sqlserver");
	}

	[Fact]
	public void Providers_Have_Postgres_Constant()
	{
		// Assert
		WriteStoreTelemetry.Providers.Postgres.ShouldBe("postgres");
	}

	[Fact]
	public void Providers_Have_MongoDb_Constant()
	{
		// Assert
		WriteStoreTelemetry.Providers.MongoDb.ShouldBe("mongodb");
	}

	[Fact]
	public void Providers_Have_CosmosDb_Constant()
	{
		// Assert
		WriteStoreTelemetry.Providers.CosmosDb.ShouldBe("cosmosdb");
	}

	[Fact]
	public void Providers_Have_DynamoDb_Constant()
	{
		// Assert
		WriteStoreTelemetry.Providers.DynamoDb.ShouldBe("dynamodb");
	}

	[Fact]
	public void Providers_Have_Firestore_Constant()
	{
		// Assert
		WriteStoreTelemetry.Providers.Firestore.ShouldBe("firestore");
	}

	[Fact]
	public void Providers_Have_Redis_Constant()
	{
		// Assert
		WriteStoreTelemetry.Providers.Redis.ShouldBe("redis");
	}

	[Fact]
	public void Providers_Have_InMemory_Constant()
	{
		// Assert
		WriteStoreTelemetry.Providers.InMemory.ShouldBe("inmemory");
	}

	#endregion

	#region Results Constants Tests

	[Fact]
	public void Results_Have_Success_Constant()
	{
		// Assert
		WriteStoreTelemetry.Results.Success.ShouldBe("success");
	}

	[Fact]
	public void Results_Have_Failure_Constant()
	{
		// Assert
		WriteStoreTelemetry.Results.Failure.ShouldBe("failure");
	}

	[Fact]
	public void Results_Have_Conflict_Constant()
	{
		// Assert
		WriteStoreTelemetry.Results.Conflict.ShouldBe("conflict");
	}

	[Fact]
	public void Results_Have_NotFound_Constant()
	{
		// Assert
		WriteStoreTelemetry.Results.NotFound.ShouldBe("not_found");
	}

	#endregion

	#region Tags Constants Tests

	[Fact]
	public void Tags_Have_Store_Constant()
	{
		// Assert
		WriteStoreTelemetry.Tags.Store.ShouldBe("store");
	}

	[Fact]
	public void Tags_Have_Provider_Constant()
	{
		// Assert
		WriteStoreTelemetry.Tags.Provider.ShouldBe("provider");
	}

	[Fact]
	public void Tags_Have_Operation_Constant()
	{
		// Assert
		WriteStoreTelemetry.Tags.Operation.ShouldBe("operation");
	}

	[Fact]
	public void Tags_Have_Result_Constant()
	{
		// Assert
		WriteStoreTelemetry.Tags.Result.ShouldBe("result");
	}

	[Fact]
	public void Tags_Have_CorrelationId_Constant()
	{
		// Assert
		WriteStoreTelemetry.Tags.CorrelationId.ShouldBe("correlation.id");
	}

	[Fact]
	public void Tags_Have_CausationId_Constant()
	{
		// Assert
		WriteStoreTelemetry.Tags.CausationId.ShouldBe("causation.id");
	}

	[Fact]
	public void Tags_Have_MessageId_Constant()
	{
		// Assert
		WriteStoreTelemetry.Tags.MessageId.ShouldBe("message.id");
	}

	[Fact]
	public void Tags_Have_TraceId_Constant()
	{
		// Assert
		WriteStoreTelemetry.Tags.TraceId.ShouldBe("trace.id");
	}

	[Fact]
	public void Tags_Have_SpanId_Constant()
	{
		// Assert
		WriteStoreTelemetry.Tags.SpanId.ShouldBe("span.id");
	}

	#endregion

	#region RecordOperation Tests

	[Fact]
	public void RecordOperation_DoesNotThrow_WithValidParameters()
	{
		// Arrange
		var store = WriteStoreTelemetry.Stores.EventStore;
		var provider = WriteStoreTelemetry.Providers.SqlServer;
		var operation = "Append";
		var result = WriteStoreTelemetry.Results.Success;
		var duration = TimeSpan.FromMilliseconds(25);

		// Act & Assert - should not throw
		Should.NotThrow(() =>
			WriteStoreTelemetry.RecordOperation(store, provider, operation, result, duration));
	}

	[Fact]
	public void RecordOperation_ThrowsArgumentNullException_WhenStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			WriteStoreTelemetry.RecordOperation(null!, "provider", "operation", "success", TimeSpan.Zero));
	}

	[Fact]
	public void RecordOperation_ThrowsArgumentNullException_WhenProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			WriteStoreTelemetry.RecordOperation("store", null!, "operation", "success", TimeSpan.Zero));
	}

	[Fact]
	public void RecordOperation_ThrowsArgumentNullException_WhenOperationIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			WriteStoreTelemetry.RecordOperation("store", "provider", null!, "success", TimeSpan.Zero));
	}

	[Fact]
	public void RecordOperation_ThrowsArgumentNullException_WhenResultIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			WriteStoreTelemetry.RecordOperation("store", "provider", "operation", null!, TimeSpan.Zero));
	}

	#endregion

	#region BeginLogScope Tests

	[Fact]
	public void BeginLogScope_ReturnsScope_WithValidParameters()
	{
		// Arrange
		var logger = A.Fake<ILogger>();

		// Act
		var scope = WriteStoreTelemetry.BeginLogScope(
			logger,
			WriteStoreTelemetry.Stores.EventStore,
			WriteStoreTelemetry.Providers.SqlServer);

		// Assert
		scope.ShouldNotBeNull();
	}

	[Fact]
	public void BeginLogScope_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			WriteStoreTelemetry.BeginLogScope(null!, "store", "provider"));
	}

	[Fact]
	public void BeginLogScope_ThrowsArgumentNullException_WhenStoreIsNull()
	{
		// Arrange
		var logger = A.Fake<ILogger>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			WriteStoreTelemetry.BeginLogScope(logger, null!, "provider"));
	}

	[Fact]
	public void BeginLogScope_ThrowsArgumentNullException_WhenProviderIsNull()
	{
		// Arrange
		var logger = A.Fake<ILogger>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			WriteStoreTelemetry.BeginLogScope(logger, "store", null!));
	}

	[Fact]
	public void BeginLogScope_WithOperation_ReturnsScope()
	{
		// Arrange
		var logger = A.Fake<ILogger>();

		// Act
		var scope = WriteStoreTelemetry.BeginLogScope(
			logger,
			WriteStoreTelemetry.Stores.OutboxStore,
			WriteStoreTelemetry.Providers.Postgres,
			operation: "Publish");

		// Assert
		scope.ShouldNotBeNull();
	}

	[Fact]
	public void BeginLogScope_WithAllOptionalParameters_ReturnsScope()
	{
		// Arrange
		var logger = A.Fake<ILogger>();

		// Act
		var scope = WriteStoreTelemetry.BeginLogScope(
			logger,
			WriteStoreTelemetry.Stores.SagaStore,
			WriteStoreTelemetry.Providers.CosmosDb,
			operation: "UpdateState",
			messageId: "msg-123",
			correlationId: "corr-456",
			causationId: "caus-789");

		// Assert
		scope.ShouldNotBeNull();
	}

	[Fact]
	public void BeginLogScope_WithWhitespaceOperation_ExcludesFromScope()
	{
		// Arrange
		var logger = A.Fake<ILogger>();

		// Act - should not throw even with whitespace
		var scope = WriteStoreTelemetry.BeginLogScope(
			logger,
			"store",
			"provider",
			operation: "   ");

		// Assert
		scope.ShouldNotBeNull();
	}

	#endregion
}
