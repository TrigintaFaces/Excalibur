using Excalibur.Dispatch.AuditLogging.Diagnostics;

namespace Excalibur.Dispatch.AuditLogging.Tests.Diagnostics;

public class AuditLoggingEventIdShould
{
    [Fact]
    public void Define_core_event_ids_in_93000_range()
    {
        AuditLoggingEventId.AuditLoggerCreated.ShouldBeInRange(93000, 93099);
        AuditLoggingEventId.AuditEventCaptured.ShouldBeInRange(93000, 93099);
        AuditLoggingEventId.AuditEventEnriched.ShouldBeInRange(93000, 93099);
        AuditLoggingEventId.AuditEventFiltered.ShouldBeInRange(93000, 93099);
        AuditLoggingEventId.AuditEventValidated.ShouldBeInRange(93000, 93099);
        AuditLoggingEventId.AuditMiddlewareExecuting.ShouldBeInRange(93000, 93099);
    }

    [Fact]
    public void Define_writer_event_ids_in_93100_range()
    {
        AuditLoggingEventId.AuditWriterCreated.ShouldBeInRange(93100, 93199);
        AuditLoggingEventId.AuditEventWritten.ShouldBeInRange(93100, 93199);
        AuditLoggingEventId.AuditBatchWritten.ShouldBeInRange(93100, 93199);
        AuditLoggingEventId.AuditWriteFailed.ShouldBeInRange(93100, 93199);
        AuditLoggingEventId.AuditWriteRetried.ShouldBeInRange(93100, 93199);
    }

    [Fact]
    public void Define_storage_event_ids_in_93200_range()
    {
        AuditLoggingEventId.SqlServerAuditStoreCreated.ShouldBeInRange(93200, 93299);
        AuditLoggingEventId.SplunkAuditAdapterCreated.ShouldBeInRange(93200, 93299);
        AuditLoggingEventId.DatadogAuditAdapterCreated.ShouldBeInRange(93200, 93299);
        AuditLoggingEventId.SentinelAuditAdapterCreated.ShouldBeInRange(93200, 93299);
        AuditLoggingEventId.AuditEventStored.ShouldBeInRange(93200, 93299);
        AuditLoggingEventId.AuditStorageCompacted.ShouldBeInRange(93200, 93299);
    }

    [Fact]
    public void Define_query_event_ids_in_93300_range()
    {
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

    [Fact]
    public void Define_siem_event_ids_in_93400_range()
    {
        AuditLoggingEventId.SiemConnectorCreated.ShouldBeInRange(93400, 93499);
        AuditLoggingEventId.SiemEventForwarded.ShouldBeInRange(93400, 93499);
        AuditLoggingEventId.SiemBatchForwarded.ShouldBeInRange(93400, 93499);
        AuditLoggingEventId.SiemConnectionEstablished.ShouldBeInRange(93400, 93499);
        AuditLoggingEventId.SiemConnectionLost.ShouldBeInRange(93400, 93499);
        AuditLoggingEventId.SiemForwardFailed.ShouldBeInRange(93400, 93499);
        AuditLoggingEventId.SiemForwardRetried.ShouldBeInRange(93400, 93499);
        AuditLoggingEventId.SiemHealthCheckFailed.ShouldBeInRange(93400, 93499);
    }

    [Fact]
    public void Define_health_check_event_ids_in_93500_range()
    {
        AuditLoggingEventId.AuditStoreHealthCheckPassed.ShouldBeInRange(93500, 93509);
        AuditLoggingEventId.AuditStoreHealthCheckDegraded.ShouldBeInRange(93500, 93509);
        AuditLoggingEventId.AuditStoreHealthCheckFailed.ShouldBeInRange(93500, 93509);
    }

    [Fact]
    public void Have_no_duplicate_event_ids()
    {
        var eventIds = new[]
        {
            AuditLoggingEventId.AuditLoggerCreated,
            AuditLoggingEventId.AuditEventCaptured,
            AuditLoggingEventId.AuditEventEnriched,
            AuditLoggingEventId.AuditEventFiltered,
            AuditLoggingEventId.AuditEventValidated,
            AuditLoggingEventId.AuditMiddlewareExecuting,
            AuditLoggingEventId.AuditWriterCreated,
            AuditLoggingEventId.AuditEventWritten,
            AuditLoggingEventId.AuditBatchWritten,
            AuditLoggingEventId.AuditWriteFailed,
            AuditLoggingEventId.AuditWriteRetried,
            AuditLoggingEventId.SqlServerAuditStoreCreated,
            AuditLoggingEventId.SplunkAuditAdapterCreated,
            AuditLoggingEventId.DatadogAuditAdapterCreated,
            AuditLoggingEventId.SentinelAuditAdapterCreated,
            AuditLoggingEventId.AuditEventStored,
            AuditLoggingEventId.AuditStorageCompacted,
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
            AuditLoggingEventId.SiemConnectorCreated,
            AuditLoggingEventId.SiemEventForwarded,
            AuditLoggingEventId.SiemBatchForwarded,
            AuditLoggingEventId.SiemConnectionEstablished,
            AuditLoggingEventId.SiemConnectionLost,
            AuditLoggingEventId.SiemForwardFailed,
            AuditLoggingEventId.SiemForwardRetried,
            AuditLoggingEventId.SiemHealthCheckFailed,
            AuditLoggingEventId.AuditStoreHealthCheckPassed,
            AuditLoggingEventId.AuditStoreHealthCheckDegraded,
            AuditLoggingEventId.AuditStoreHealthCheckFailed
        };

        var uniqueIds = eventIds.Distinct().ToArray();
        uniqueIds.Length.ShouldBe(eventIds.Length);
    }
}
