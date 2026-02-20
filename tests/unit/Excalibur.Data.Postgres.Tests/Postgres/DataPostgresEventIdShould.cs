// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Diagnostics;

namespace Excalibur.Data.Tests.Postgres;

/// <summary>
/// Unit tests for <see cref="DataPostgresEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.Postgres")]
[Trait("Priority", "0")]
public sealed class DataPostgresEventIdShould : UnitTestBase
{
	#region Connection Management Event ID Tests (101000-101099)

	[Fact]
	public void HaveConnectionOpenedInConnectionRange()
	{
		DataPostgresEventId.ConnectionOpened.ShouldBe(101000);
	}

	[Fact]
	public void HaveAllConnectionManagementEventIdsInExpectedRange()
	{
		DataPostgresEventId.ConnectionOpened.ShouldBeInRange(101000, 101099);
		DataPostgresEventId.ConnectionClosed.ShouldBeInRange(101000, 101099);
		DataPostgresEventId.ConnectionPoolCreated.ShouldBeInRange(101000, 101099);
		DataPostgresEventId.ConnectionAcquired.ShouldBeInRange(101000, 101099);
		DataPostgresEventId.ConnectionReturned.ShouldBeInRange(101000, 101099);
		DataPostgresEventId.ConnectionFailed.ShouldBeInRange(101000, 101099);
		DataPostgresEventId.SslConnectionEstablished.ShouldBeInRange(101000, 101099);
	}

	#endregion

	#region Query Execution Event ID Tests (101100-101199)

	[Fact]
	public void HaveQueryExecutingInQueryRange()
	{
		DataPostgresEventId.QueryExecuting.ShouldBe(101100);
	}

	[Fact]
	public void HaveAllQueryExecutionEventIdsInExpectedRange()
	{
		DataPostgresEventId.QueryExecuting.ShouldBeInRange(101100, 101199);
		DataPostgresEventId.QueryExecuted.ShouldBeInRange(101100, 101199);
		DataPostgresEventId.QueryFailed.ShouldBeInRange(101100, 101199);
		DataPostgresEventId.QueryTimeout.ShouldBeInRange(101100, 101199);
		DataPostgresEventId.PreparedStatementCreated.ShouldBeInRange(101100, 101199);
		DataPostgresEventId.CopyOperationExecuted.ShouldBeInRange(101100, 101199);
	}

	#endregion

	#region Transaction Management Event ID Tests (101200-101299)

	[Fact]
	public void HaveTransactionStartedInTransactionRange()
	{
		DataPostgresEventId.TransactionStarted.ShouldBe(101200);
	}

	[Fact]
	public void HaveAllTransactionEventIdsInExpectedRange()
	{
		DataPostgresEventId.TransactionStarted.ShouldBeInRange(101200, 101299);
		DataPostgresEventId.TransactionCommitted.ShouldBeInRange(101200, 101299);
		DataPostgresEventId.TransactionRolledBack.ShouldBeInRange(101200, 101299);
		DataPostgresEventId.SavepointCreated.ShouldBeInRange(101200, 101299);
		DataPostgresEventId.SavepointReleased.ShouldBeInRange(101200, 101299);
		DataPostgresEventId.AdvisoryLockAcquired.ShouldBeInRange(101200, 101299);
		DataPostgresEventId.AdvisoryLockReleased.ShouldBeInRange(101200, 101299);
	}

	#endregion

	#region Outbox Store Event ID Tests (101900-101999)

	[Fact]
	public void HaveOutboxSaveMessagesInOutboxRange()
	{
		DataPostgresEventId.OutboxSaveMessages.ShouldBe(101900);
	}

	[Fact]
	public void HaveAllOutboxStoreEventIdsInExpectedRange()
	{
		DataPostgresEventId.OutboxSaveMessages.ShouldBeInRange(101900, 101999);
		DataPostgresEventId.OutboxReserveMessages.ShouldBeInRange(101900, 101999);
		DataPostgresEventId.OutboxUnreserveMessages.ShouldBeInRange(101900, 101999);
		DataPostgresEventId.OutboxDeleteRecord.ShouldBeInRange(101900, 101999);
		DataPostgresEventId.OutboxIncreaseAttempts.ShouldBeInRange(101900, 101999);
		DataPostgresEventId.OutboxMoveToDeadLetter.ShouldBeInRange(101900, 101999);
	}

	#endregion

	#region CDC Processor Event ID Tests (102300-102399)

	[Fact]
	public void HaveCdcProcessorStartingInCdcRange()
	{
		DataPostgresEventId.CdcProcessorStarting.ShouldBe(102300);
	}

	[Fact]
	public void HaveAllCdcProcessorEventIdsInExpectedRange()
	{
		DataPostgresEventId.CdcProcessorStarting.ShouldBeInRange(102300, 102399);
		DataPostgresEventId.CdcResumingFromPosition.ShouldBeInRange(102300, 102399);
		DataPostgresEventId.CdcConnectedToReplicationStream.ShouldBeInRange(102300, 102399);
		DataPostgresEventId.CdcCreatedReplicationSlot.ShouldBeInRange(102300, 102399);
		DataPostgresEventId.CdcReplicationSlotExists.ShouldBeInRange(102300, 102399);
		DataPostgresEventId.CdcProcessedChange.ShouldBeInRange(102300, 102399);
		DataPostgresEventId.CdcConfirmedPosition.ShouldBeInRange(102300, 102399);
		DataPostgresEventId.CdcProcessorStopping.ShouldBeInRange(102300, 102399);
		DataPostgresEventId.CdcProcessingError.ShouldBeInRange(102300, 102399);
	}

	#endregion

	#region Postgres Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInPostgresReservedRange()
	{
		// Postgres reserved range is 101000-102999
		var allEventIds = GetAllPostgresEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(101000, 102999,
				$"Event ID {eventId} is outside Postgres reserved range (101000-102999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllPostgresEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllPostgresEventIds();
		allEventIds.Length.ShouldBeGreaterThan(50);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllPostgresEventIds()
	{
		return
		[
			// Connection Management (101000-101099)
			DataPostgresEventId.ConnectionOpened,
			DataPostgresEventId.ConnectionClosed,
			DataPostgresEventId.ConnectionPoolCreated,
			DataPostgresEventId.ConnectionAcquired,
			DataPostgresEventId.ConnectionReturned,
			DataPostgresEventId.ConnectionFailed,
			DataPostgresEventId.SslConnectionEstablished,

			// Query Execution (101100-101199)
			DataPostgresEventId.QueryExecuting,
			DataPostgresEventId.QueryExecuted,
			DataPostgresEventId.QueryFailed,
			DataPostgresEventId.QueryTimeout,
			DataPostgresEventId.PreparedStatementCreated,
			DataPostgresEventId.CopyOperationExecuted,

			// Transaction Management (101200-101299)
			DataPostgresEventId.TransactionStarted,
			DataPostgresEventId.TransactionCommitted,
			DataPostgresEventId.TransactionRolledBack,
			DataPostgresEventId.SavepointCreated,
			DataPostgresEventId.SavepointReleased,
			DataPostgresEventId.AdvisoryLockAcquired,
			DataPostgresEventId.AdvisoryLockReleased,

			// Npgsql Integration (101300-101399)
			DataPostgresEventId.NpgsqlDataSourceCreated,
			DataPostgresEventId.NpgsqlMultiplexingEnabled,
			DataPostgresEventId.TypeMappingConfigured,
			DataPostgresEventId.NotificationReceived,

			// Array/JSON Operations (101400-101499)
			DataPostgresEventId.ArrayParameterBound,
			DataPostgresEventId.JsonColumnRead,
			DataPostgresEventId.JsonColumnWritten,
			DataPostgresEventId.JsonbOperationExecuted,

			// Performance (101500-101599)
			DataPostgresEventId.SlowQueryDetected,
			DataPostgresEventId.QueryStatisticsCollected,
			DataPostgresEventId.ExecutionPlanRetrieved,
			DataPostgresEventId.WaitEventDetected,

			// Error Handling (101600-101699)
			DataPostgresEventId.DeadlockDetected,
			DataPostgresEventId.SerializationFailure,
			DataPostgresEventId.UniqueViolation,
			DataPostgresEventId.ForeignKeyViolation,
			DataPostgresEventId.PostgresError,

			// Retry Policy (101700-101799)
			DataPostgresEventId.RetryAttempt,

			// Persistence Provider (101800-101899)
			DataPostgresEventId.ExecutingDataRequest,
			DataPostgresEventId.ExecutingDataRequestInTransaction,
			DataPostgresEventId.FailedToExecuteDataRequest,
			DataPostgresEventId.ConnectionTestSuccessful,
			DataPostgresEventId.ConnectionTestFailed,
			DataPostgresEventId.InitializingProvider,
			DataPostgresEventId.DisposingProvider,
			DataPostgresEventId.ClearedConnectionPools,
			DataPostgresEventId.ErrorDisposingProvider,
			DataPostgresEventId.PersistenceRetryAttempt,

			// Outbox Store (101900-101999)
			DataPostgresEventId.OutboxSaveMessages,
			DataPostgresEventId.OutboxReserveMessages,
			DataPostgresEventId.OutboxUnreserveMessages,
			DataPostgresEventId.OutboxDeleteRecord,
			DataPostgresEventId.OutboxIncreaseAttempts,
			DataPostgresEventId.OutboxMoveToDeadLetter,
			DataPostgresEventId.OutboxBatchOperation,
			DataPostgresEventId.OutboxOperationCompleted,

			// Dead Letter Store (102000-102099)
			DataPostgresEventId.StoredDeadLetterMessage,
			DataPostgresEventId.MarkedDeadLetterMessageAsReplayed,
			DataPostgresEventId.DeletedDeadLetterMessage,
			DataPostgresEventId.CleanedUpOldDeadLetterMessages,

			// Health Checker (102100-102199)
			DataPostgresEventId.HealthCheckSucceeded,
			DataPostgresEventId.HealthCheckFailed,
			DataPostgresEventId.QuickCheckFailed,

			// Connection Factory (102200-102299)
			DataPostgresEventId.CreatedPostgresConnection,
			DataPostgresEventId.CreatePostgresConnectionFailed,
			DataPostgresEventId.ConnectionValidationFailed,
			DataPostgresEventId.RepairConnectionFailed,
			DataPostgresEventId.DisposeConnectionWarning,

			// CDC Processor (102300-102399)
			DataPostgresEventId.CdcProcessorStarting,
			DataPostgresEventId.CdcResumingFromPosition,
			DataPostgresEventId.CdcConnectedToReplicationStream,
			DataPostgresEventId.CdcCreatedReplicationSlot,
			DataPostgresEventId.CdcReplicationSlotExists,
			DataPostgresEventId.CdcProcessedChange,
			DataPostgresEventId.CdcConfirmedPosition,
			DataPostgresEventId.CdcProcessorStopping,
			DataPostgresEventId.CdcProcessingError
		];
	}

	#endregion
}
