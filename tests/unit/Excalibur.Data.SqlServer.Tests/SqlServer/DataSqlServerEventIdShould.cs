// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Diagnostics;

namespace Excalibur.Data.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="DataSqlServerEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait("Priority", "0")]
public sealed class DataSqlServerEventIdShould : UnitTestBase
{
	#region Connection Management Event ID Tests (100000-100099)

	[Fact]
	public void HaveConnectionOpenedInConnectionRange()
	{
		DataSqlServerEventId.ConnectionOpened.ShouldBe(100000);
	}

	[Fact]
	public void HaveAllConnectionManagementEventIdsInExpectedRange()
	{
		DataSqlServerEventId.ConnectionOpened.ShouldBeInRange(100000, 100099);
		DataSqlServerEventId.ConnectionClosed.ShouldBeInRange(100000, 100099);
		DataSqlServerEventId.ConnectionPoolCreated.ShouldBeInRange(100000, 100099);
		DataSqlServerEventId.ConnectionAcquired.ShouldBeInRange(100000, 100099);
		DataSqlServerEventId.ConnectionReturned.ShouldBeInRange(100000, 100099);
		DataSqlServerEventId.ConnectionFailed.ShouldBeInRange(100000, 100099);
		DataSqlServerEventId.ConnectionRetry.ShouldBeInRange(100000, 100099);
	}

	#endregion

	#region Query Execution Event ID Tests (100100-100199)

	[Fact]
	public void HaveQueryExecutingInQueryRange()
	{
		DataSqlServerEventId.QueryExecuting.ShouldBe(100100);
	}

	[Fact]
	public void HaveAllQueryExecutionEventIdsInExpectedRange()
	{
		DataSqlServerEventId.QueryExecuting.ShouldBeInRange(100100, 100199);
		DataSqlServerEventId.QueryExecuted.ShouldBeInRange(100100, 100199);
		DataSqlServerEventId.QueryFailed.ShouldBeInRange(100100, 100199);
		DataSqlServerEventId.QueryTimeout.ShouldBeInRange(100100, 100199);
		DataSqlServerEventId.QueryPlanCached.ShouldBeInRange(100100, 100199);
		DataSqlServerEventId.ParameterizedQueryExecuted.ShouldBeInRange(100100, 100199);
	}

	#endregion

	#region Transaction Management Event ID Tests (100200-100299)

	[Fact]
	public void HaveTransactionStartedInTransactionRange()
	{
		DataSqlServerEventId.TransactionStarted.ShouldBe(100200);
	}

	[Fact]
	public void HaveAllTransactionEventIdsInExpectedRange()
	{
		DataSqlServerEventId.TransactionStarted.ShouldBeInRange(100200, 100299);
		DataSqlServerEventId.TransactionCommitted.ShouldBeInRange(100200, 100299);
		DataSqlServerEventId.TransactionRolledBack.ShouldBeInRange(100200, 100299);
		DataSqlServerEventId.SavepointCreated.ShouldBeInRange(100200, 100299);
		DataSqlServerEventId.SavepointRolledBack.ShouldBeInRange(100200, 100299);
		DataSqlServerEventId.DistributedTransactionEnlisted.ShouldBeInRange(100200, 100299);
	}

	#endregion

	#region CDC Processor Event ID Tests (100700-100749)

	[Fact]
	public void HaveCdcRunStartingInCdcRange()
	{
		DataSqlServerEventId.CdcRunStarting.ShouldBe(100700);
	}

	[Fact]
	public void HaveAllCdcProcessorEventIdsInExpectedRange()
	{
		DataSqlServerEventId.CdcRunStarting.ShouldBeInRange(100700, 100749);
		DataSqlServerEventId.CdcRunCompleted.ShouldBeInRange(100700, 100749);
		DataSqlServerEventId.CdcRunSkippedNoChanges.ShouldBeInRange(100700, 100749);
		DataSqlServerEventId.CdcProcessorStarting.ShouldBeInRange(100700, 100749);
		DataSqlServerEventId.CdcProcessorStopped.ShouldBeInRange(100700, 100749);
		DataSqlServerEventId.CdcProcessorError.ShouldBeInRange(100700, 100749);
	}

	#endregion

	#region SQL Server Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInSqlServerReservedRange()
	{
		// SQL Server reserved range is 100000-100999
		var allEventIds = GetAllSqlServerEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(100000, 100999,
				$"Event ID {eventId} is outside SQL Server reserved range (100000-100999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllSqlServerEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllSqlServerEventIds();
		allEventIds.Length.ShouldBeGreaterThan(75);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllSqlServerEventIds()
	{
		return
		[
			// Connection Management (100000-100099)
			DataSqlServerEventId.ConnectionOpened,
			DataSqlServerEventId.ConnectionClosed,
			DataSqlServerEventId.ConnectionPoolCreated,
			DataSqlServerEventId.ConnectionAcquired,
			DataSqlServerEventId.ConnectionReturned,
			DataSqlServerEventId.ConnectionFailed,
			DataSqlServerEventId.ConnectionRetry,

			// Query Execution (100100-100199)
			DataSqlServerEventId.QueryExecuting,
			DataSqlServerEventId.QueryExecuted,
			DataSqlServerEventId.QueryFailed,
			DataSqlServerEventId.QueryTimeout,
			DataSqlServerEventId.QueryPlanCached,
			DataSqlServerEventId.ParameterizedQueryExecuted,

			// Transaction Management (100200-100299)
			DataSqlServerEventId.TransactionStarted,
			DataSqlServerEventId.TransactionCommitted,
			DataSqlServerEventId.TransactionRolledBack,
			DataSqlServerEventId.SavepointCreated,
			DataSqlServerEventId.SavepointRolledBack,
			DataSqlServerEventId.DistributedTransactionEnlisted,

			// Stored Procedures (100300-100399)
			DataSqlServerEventId.StoredProcedureExecuting,
			DataSqlServerEventId.StoredProcedureExecuted,
			DataSqlServerEventId.StoredProcedureFailed,
			DataSqlServerEventId.OutputParametersRetrieved,

			// Bulk Operations (100400-100499)
			DataSqlServerEventId.BulkInsertStarted,
			DataSqlServerEventId.BulkInsertCompleted,
			DataSqlServerEventId.BulkUpdateStarted,
			DataSqlServerEventId.BulkUpdateCompleted,
			DataSqlServerEventId.BulkOperationFailed,

			// Performance (100500-100599)
			DataSqlServerEventId.SlowQueryDetected,
			DataSqlServerEventId.QueryStatisticsCollected,
			DataSqlServerEventId.IndexHintApplied,
			DataSqlServerEventId.ExecutionPlanRetrieved,

			// Error Handling (100600-100699)
			DataSqlServerEventId.DeadlockDetected,
			DataSqlServerEventId.ConcurrencyException,
			DataSqlServerEventId.ConstraintViolation,
			DataSqlServerEventId.SqlError,
			DataSqlServerEventId.DataTruncationWarning,

			// CDC Processor (100700-100749)
			DataSqlServerEventId.CdcRunStarting,
			DataSqlServerEventId.CdcRunCompleted,
			DataSqlServerEventId.CdcRunSkippedNoChanges,
			DataSqlServerEventId.CdcRunError,
			DataSqlServerEventId.CdcChangesRetrieved,
			DataSqlServerEventId.CdcChangeProcessed,
			DataSqlServerEventId.CdcChangeProcessingError,
			DataSqlServerEventId.CdcBatchCompleted,
			DataSqlServerEventId.CdcBatchError,
			DataSqlServerEventId.CdcProcessorStarting,
			DataSqlServerEventId.CdcProcessorStopped,
			DataSqlServerEventId.CdcProcessorError,
			DataSqlServerEventId.CdcTableRegistered,
			DataSqlServerEventId.CdcTableUnregistered,
			DataSqlServerEventId.CdcLsnUpdated,
			DataSqlServerEventId.CdcLsnRetrieved,
			DataSqlServerEventId.CdcLsnError,

			// Persistence Provider (100810-100829)
			DataSqlServerEventId.PersistenceProviderInitialized,
			DataSqlServerEventId.PersistenceConnectionCreated,
			DataSqlServerEventId.PersistenceConnectionOpened,
			DataSqlServerEventId.PersistenceDataRequestExecuted,
			DataSqlServerEventId.PersistenceDataRequestError,

			// Root Persistence (100850-100867)
			DataSqlServerEventId.RootRetryAttempt,
			DataSqlServerEventId.RootExecutingDataRequest,
			DataSqlServerEventId.RootExecutingInTransaction,
			DataSqlServerEventId.RootConnectionTestSuccessful,
			DataSqlServerEventId.RootConnectionTestFailed,

			// Health Check (100830-100849)
			DataSqlServerEventId.PersistenceHealthCheckFailed,
			DataSqlServerEventId.PersistenceHealthCheckSucceeded,
			DataSqlServerEventId.PersistenceHealthCheckTimeout,
			DataSqlServerEventId.PersistenceHealthCheckStarted,
			DataSqlServerEventId.PersistenceHealthCheckCompleted,

			// Connection Factory (100870-100879)
			DataSqlServerEventId.ConnectionFactoryCreateFailed,
			DataSqlServerEventId.ConnectionValidationError,
			DataSqlServerEventId.ConnectionFactoryRetry,
			DataSqlServerEventId.HealthCheckerSucceeded,
			DataSqlServerEventId.HealthCheckerFailed,

			// Dead Letter (100880-100889)
			DataSqlServerEventId.DeadLetterMessageStored,
			DataSqlServerEventId.DeadLetterStoreError,
			DataSqlServerEventId.DeadLetterRetrievalError,
			DataSqlServerEventId.DeadLetterCleanupCompleted,

			// Retry Policy (100900-100909)
			DataSqlServerEventId.SqlServerOperationRetry
		];
	}

	#endregion
}
