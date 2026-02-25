// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Diagnostics;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Diagnostics;

/// <summary>
/// Unit tests for <see cref="AuditLoggingEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "AuditLogging")]
[Trait("Priority", "0")]
public sealed class AuditLoggingEventIdShould : UnitTestBase
{
	#region Audit Logging Core Event ID Tests (93000-93099)

	[Fact]
	public void HaveAuditLoggerCreatedInCoreRange()
	{
		AuditLoggingEventId.AuditLoggerCreated.ShouldBe(93000);
	}

	[Fact]
	public void HaveAuditEventCapturedInCoreRange()
	{
		AuditLoggingEventId.AuditEventCaptured.ShouldBe(93001);
	}

	[Fact]
	public void HaveAuditEventEnrichedInCoreRange()
	{
		AuditLoggingEventId.AuditEventEnriched.ShouldBe(93002);
	}

	[Fact]
	public void HaveAuditEventFilteredInCoreRange()
	{
		AuditLoggingEventId.AuditEventFiltered.ShouldBe(93003);
	}

	[Fact]
	public void HaveAuditEventValidatedInCoreRange()
	{
		AuditLoggingEventId.AuditEventValidated.ShouldBe(93004);
	}

	[Fact]
	public void HaveAuditMiddlewareExecutingInCoreRange()
	{
		AuditLoggingEventId.AuditMiddlewareExecuting.ShouldBe(93005);
	}

	[Fact]
	public void HaveAllCoreEventIdsInExpectedRange()
	{
		// Audit Logging Core IDs are in range 93000-93099
		AuditLoggingEventId.AuditLoggerCreated.ShouldBeInRange(93000, 93099);
		AuditLoggingEventId.AuditEventCaptured.ShouldBeInRange(93000, 93099);
		AuditLoggingEventId.AuditEventEnriched.ShouldBeInRange(93000, 93099);
		AuditLoggingEventId.AuditEventFiltered.ShouldBeInRange(93000, 93099);
		AuditLoggingEventId.AuditEventValidated.ShouldBeInRange(93000, 93099);
		AuditLoggingEventId.AuditMiddlewareExecuting.ShouldBeInRange(93000, 93099);
	}

	#endregion

	#region Audit Event Writers Event ID Tests (93100-93199)

	[Fact]
	public void HaveAuditWriterCreatedInWritersRange()
	{
		AuditLoggingEventId.AuditWriterCreated.ShouldBe(93100);
	}

	[Fact]
	public void HaveAuditEventWrittenInWritersRange()
	{
		AuditLoggingEventId.AuditEventWritten.ShouldBe(93101);
	}

	[Fact]
	public void HaveAuditBatchWrittenInWritersRange()
	{
		AuditLoggingEventId.AuditBatchWritten.ShouldBe(93102);
	}

	[Fact]
	public void HaveAuditWriteFailedInWritersRange()
	{
		AuditLoggingEventId.AuditWriteFailed.ShouldBe(93103);
	}

	[Fact]
	public void HaveAuditWriteRetriedInWritersRange()
	{
		AuditLoggingEventId.AuditWriteRetried.ShouldBe(93104);
	}

	[Fact]
	public void HaveAllWritersEventIdsInExpectedRange()
	{
		// Audit Event Writers IDs are in range 93100-93199
		AuditLoggingEventId.AuditWriterCreated.ShouldBeInRange(93100, 93199);
		AuditLoggingEventId.AuditEventWritten.ShouldBeInRange(93100, 93199);
		AuditLoggingEventId.AuditBatchWritten.ShouldBeInRange(93100, 93199);
		AuditLoggingEventId.AuditWriteFailed.ShouldBeInRange(93100, 93199);
		AuditLoggingEventId.AuditWriteRetried.ShouldBeInRange(93100, 93199);
	}

	#endregion

	#region Audit Storage Event ID Tests (93200-93299)

	[Fact]
	public void HaveSqlServerAuditStoreCreatedInStorageRange()
	{
		AuditLoggingEventId.SqlServerAuditStoreCreated.ShouldBe(93200);
	}

	[Fact]
	public void HaveSplunkAuditAdapterCreatedInStorageRange()
	{
		AuditLoggingEventId.SplunkAuditAdapterCreated.ShouldBe(93201);
	}

	[Fact]
	public void HaveDatadogAuditAdapterCreatedInStorageRange()
	{
		AuditLoggingEventId.DatadogAuditAdapterCreated.ShouldBe(93202);
	}

	[Fact]
	public void HaveSentinelAuditAdapterCreatedInStorageRange()
	{
		AuditLoggingEventId.SentinelAuditAdapterCreated.ShouldBe(93203);
	}

	[Fact]
	public void HaveAuditEventStoredInStorageRange()
	{
		AuditLoggingEventId.AuditEventStored.ShouldBe(93204);
	}

	[Fact]
	public void HaveAuditStorageCompactedInStorageRange()
	{
		AuditLoggingEventId.AuditStorageCompacted.ShouldBe(93205);
	}

	[Fact]
	public void HaveAllStorageEventIdsInExpectedRange()
	{
		// Audit Storage IDs are in range 93200-93299
		AuditLoggingEventId.SqlServerAuditStoreCreated.ShouldBeInRange(93200, 93299);
		AuditLoggingEventId.SplunkAuditAdapterCreated.ShouldBeInRange(93200, 93299);
		AuditLoggingEventId.DatadogAuditAdapterCreated.ShouldBeInRange(93200, 93299);
		AuditLoggingEventId.SentinelAuditAdapterCreated.ShouldBeInRange(93200, 93299);
		AuditLoggingEventId.AuditEventStored.ShouldBeInRange(93200, 93299);
		AuditLoggingEventId.AuditStorageCompacted.ShouldBeInRange(93200, 93299);
	}

	#endregion

	#region Audit Query Event ID Tests (93300-93399)

	[Fact]
	public void HaveAuditQueryServiceCreatedInQueryRange()
	{
		AuditLoggingEventId.AuditQueryServiceCreated.ShouldBe(93300);
	}

	[Fact]
	public void HaveAuditQueryExecutedInQueryRange()
	{
		AuditLoggingEventId.AuditQueryExecuted.ShouldBe(93301);
	}

	[Fact]
	public void HaveAuditReportGeneratedInQueryRange()
	{
		AuditLoggingEventId.AuditReportGenerated.ShouldBe(93302);
	}

	[Fact]
	public void HaveAuditExportCompletedInQueryRange()
	{
		AuditLoggingEventId.AuditExportCompleted.ShouldBe(93303);
	}

	[Fact]
	public void HaveAuditSearchCompletedInQueryRange()
	{
		AuditLoggingEventId.AuditSearchCompleted.ShouldBe(93304);
	}

	[Fact]
	public void HaveAuditIntegrityVerificationStartedInQueryRange()
	{
		AuditLoggingEventId.AuditIntegrityVerificationStarted.ShouldBe(93305);
	}

	[Fact]
	public void HaveAuditIntegrityVerificationCompletedInQueryRange()
	{
		AuditLoggingEventId.AuditIntegrityVerificationCompleted.ShouldBe(93306);
	}

	[Fact]
	public void HaveAuditIntegrityVerificationFailedInQueryRange()
	{
		AuditLoggingEventId.AuditIntegrityVerificationFailed.ShouldBe(93307);
	}

	[Fact]
	public void HaveAuditLogAccessDeniedInQueryRange()
	{
		AuditLoggingEventId.AuditLogAccessDenied.ShouldBe(93308);
	}

	[Fact]
	public void HaveAuditIntegrityVerificationAccessDeniedInQueryRange()
	{
		AuditLoggingEventId.AuditIntegrityVerificationAccessDenied.ShouldBe(93309);
	}

	[Fact]
	public void HaveMetaAuditLoggingFailedInQueryRange()
	{
		AuditLoggingEventId.MetaAuditLoggingFailed.ShouldBe(93310);
	}

	[Fact]
	public void HaveAuditIntegrityVerificationErrorInQueryRange()
	{
		AuditLoggingEventId.AuditIntegrityVerificationError.ShouldBe(93311);
	}

	[Fact]
	public void HaveAllQueryEventIdsInExpectedRange()
	{
		// Audit Query IDs are in range 93300-93399
		AuditLoggingEventId.AuditQueryServiceCreated.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditQueryExecuted.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditReportGenerated.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditExportCompleted.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditSearchCompleted.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditIntegrityVerificationStarted.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditIntegrityVerificationCompleted.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditIntegrityVerificationFailed.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditLogAccessDenied.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditIntegrityVerificationAccessDenied.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.MetaAuditLoggingFailed.ShouldBeInRange(93300, 93399);
		AuditLoggingEventId.AuditIntegrityVerificationError.ShouldBeInRange(93300, 93399);
	}

	#endregion

	#region SIEM Integration Event ID Tests (93400-93499)

	[Fact]
	public void HaveSiemConnectorCreatedInSiemRange()
	{
		AuditLoggingEventId.SiemConnectorCreated.ShouldBe(93400);
	}

	[Fact]
	public void HaveSiemEventForwardedInSiemRange()
	{
		AuditLoggingEventId.SiemEventForwarded.ShouldBe(93401);
	}

	[Fact]
	public void HaveSiemBatchForwardedInSiemRange()
	{
		AuditLoggingEventId.SiemBatchForwarded.ShouldBe(93402);
	}

	[Fact]
	public void HaveSiemConnectionEstablishedInSiemRange()
	{
		AuditLoggingEventId.SiemConnectionEstablished.ShouldBe(93403);
	}

	[Fact]
	public void HaveSiemConnectionLostInSiemRange()
	{
		AuditLoggingEventId.SiemConnectionLost.ShouldBe(93404);
	}

	[Fact]
	public void HaveSiemForwardFailedInSiemRange()
	{
		AuditLoggingEventId.SiemForwardFailed.ShouldBe(93405);
	}

	[Fact]
	public void HaveSiemForwardRetriedInSiemRange()
	{
		AuditLoggingEventId.SiemForwardRetried.ShouldBe(93406);
	}

	[Fact]
	public void HaveSiemHealthCheckFailedInSiemRange()
	{
		AuditLoggingEventId.SiemHealthCheckFailed.ShouldBe(93407);
	}

	[Fact]
	public void HaveAllSiemEventIdsInExpectedRange()
	{
		// SIEM Integration IDs are in range 93400-93499
		AuditLoggingEventId.SiemConnectorCreated.ShouldBeInRange(93400, 93499);
		AuditLoggingEventId.SiemEventForwarded.ShouldBeInRange(93400, 93499);
		AuditLoggingEventId.SiemBatchForwarded.ShouldBeInRange(93400, 93499);
		AuditLoggingEventId.SiemConnectionEstablished.ShouldBeInRange(93400, 93499);
		AuditLoggingEventId.SiemConnectionLost.ShouldBeInRange(93400, 93499);
		AuditLoggingEventId.SiemForwardFailed.ShouldBeInRange(93400, 93499);
		AuditLoggingEventId.SiemForwardRetried.ShouldBeInRange(93400, 93499);
		AuditLoggingEventId.SiemHealthCheckFailed.ShouldBeInRange(93400, 93499);
	}

	#endregion

	#region AuditLogging Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInAuditLoggingReservedRange()
	{
		// AuditLogging reserved range is 93000-93999
		var allEventIds = GetAllAuditLoggingEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(93000, 93999,
				$"Event ID {eventId} is outside AuditLogging reserved range (93000-93999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllAuditLoggingEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllAuditLoggingEventIds();

		// Total: 6 Core + 5 Writers + 6 Storage + 12 Query + 8 SIEM = 37 event IDs
		allEventIds.Length.ShouldBe(37);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllAuditLoggingEventIds()
	{
		return
		[
			// Audit Logging Core (93000-93099)
			AuditLoggingEventId.AuditLoggerCreated,
			AuditLoggingEventId.AuditEventCaptured,
			AuditLoggingEventId.AuditEventEnriched,
			AuditLoggingEventId.AuditEventFiltered,
			AuditLoggingEventId.AuditEventValidated,
			AuditLoggingEventId.AuditMiddlewareExecuting,

			// Audit Event Writers (93100-93199)
			AuditLoggingEventId.AuditWriterCreated,
			AuditLoggingEventId.AuditEventWritten,
			AuditLoggingEventId.AuditBatchWritten,
			AuditLoggingEventId.AuditWriteFailed,
			AuditLoggingEventId.AuditWriteRetried,

			// Audit Storage (93200-93299)
			AuditLoggingEventId.SqlServerAuditStoreCreated,
			AuditLoggingEventId.SplunkAuditAdapterCreated,
			AuditLoggingEventId.DatadogAuditAdapterCreated,
			AuditLoggingEventId.SentinelAuditAdapterCreated,
			AuditLoggingEventId.AuditEventStored,
			AuditLoggingEventId.AuditStorageCompacted,

			// Audit Query (93300-93399)
			AuditLoggingEventId.AuditQueryServiceCreated,
			AuditLoggingEventId.AuditQueryExecuted,
			AuditLoggingEventId.AuditReportGenerated,
			AuditLoggingEventId.AuditExportCompleted,
			AuditLoggingEventId.AuditSearchCompleted,
			AuditLoggingEventId.AuditIntegrityVerificationStarted,
			AuditLoggingEventId.AuditIntegrityVerificationCompleted,
			AuditLoggingEventId.AuditIntegrityVerificationFailed,
			AuditLoggingEventId.AuditLogAccessDenied,
			AuditLoggingEventId.AuditIntegrityVerificationAccessDenied,
			AuditLoggingEventId.MetaAuditLoggingFailed,
			AuditLoggingEventId.AuditIntegrityVerificationError,

			// SIEM Integration (93400-93499)
			AuditLoggingEventId.SiemConnectorCreated,
			AuditLoggingEventId.SiemEventForwarded,
			AuditLoggingEventId.SiemBatchForwarded,
			AuditLoggingEventId.SiemConnectionEstablished,
			AuditLoggingEventId.SiemConnectionLost,
			AuditLoggingEventId.SiemForwardFailed,
			AuditLoggingEventId.SiemForwardRetried,
			AuditLoggingEventId.SiemHealthCheckFailed
		];
	}

	#endregion
}
