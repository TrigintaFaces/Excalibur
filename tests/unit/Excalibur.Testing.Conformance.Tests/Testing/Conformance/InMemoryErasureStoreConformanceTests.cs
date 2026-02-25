// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryErasureStore"/> validating IErasureStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryErasureStore uses instance-level ConcurrentDictionary collections with no static state,
/// so no special isolation is required beyond using fresh store instances.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> IErasureStore implements GDPR Article 17 "Right to Erasure".
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>SaveRequestAsync THROWS InvalidOperationException on duplicate RequestId</description></item>
/// <item><description>DataSubjectId automatically SHA256-hashed for privacy</description></item>
/// <item><description>STATE MACHINE: RecordCancellationAsync only works for Pending/Scheduled status</description></item>
/// <item><description>RecordCompletionAsync THROWS KeyNotFoundException if request not found</description></item>
/// <item><description>SaveCertificateAsync THROWS InvalidOperationException on duplicate CertificateId</description></item>
/// <item><description>UpdateStatusAsync sets ExecutedAt when status changes to InProgress</description></item>
/// <item><description>GetScheduledRequestsAsync returns requests where ScheduledExecutionAt &lt;= now</description></item>
/// <item><description>CleanupExpiredCertificatesAsync removes where RetainUntil &lt; now</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "STORE")]
public class InMemoryErasureStoreConformanceTests : ErasureStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override IErasureStore CreateStore() => new InMemoryErasureStore();

	#region Request Lifecycle Tests

	[Fact]
	public Task SaveRequestAsync_ShouldPersistRequest_Test() =>
		SaveRequestAsync_ShouldPersistRequest();

	[Fact]
	public Task SaveRequestAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() =>
		SaveRequestAsync_DuplicateId_ShouldThrowInvalidOperationException();

	[Fact]
	public Task SaveRequestAsync_ShouldHashDataSubjectId_Test() =>
		SaveRequestAsync_ShouldHashDataSubjectId();

	[Fact]
	public Task GetStatusAsync_NonExistent_ShouldReturnNull_Test() =>
		GetStatusAsync_NonExistent_ShouldReturnNull();

	#endregion Request Lifecycle Tests

	#region Status Update Tests

	[Fact]
	public Task UpdateStatusAsync_ShouldUpdateStatus_Test() =>
		UpdateStatusAsync_ShouldUpdateStatus();

	[Fact]
	public Task UpdateStatusAsync_ToInProgress_ShouldSetExecutedAt_Test() =>
		UpdateStatusAsync_ToInProgress_ShouldSetExecutedAt();

	[Fact]
	public Task UpdateStatusAsync_NonExistent_ShouldReturnFalse_Test() =>
		UpdateStatusAsync_NonExistent_ShouldReturnFalse();

	#endregion Status Update Tests

	#region Completion Tests

	[Fact]
	public Task RecordCompletionAsync_ShouldMarkCompleted_Test() =>
		RecordCompletionAsync_ShouldMarkCompleted();

	[Fact]
	public Task RecordCompletionAsync_NonExistent_ShouldThrowKeyNotFoundException_Test() =>
		RecordCompletionAsync_NonExistent_ShouldThrowKeyNotFoundException();

	#endregion Completion Tests

	#region Cancellation Tests (STATE MACHINE)

	[Fact]
	public Task RecordCancellationAsync_Scheduled_ShouldCancel_Test() =>
		RecordCancellationAsync_Scheduled_ShouldCancel();

	[Fact]
	public Task RecordCancellationAsync_Pending_ShouldCancel_Test() =>
		RecordCancellationAsync_Pending_ShouldCancel();

	[Fact]
	public Task RecordCancellationAsync_InProgress_ShouldReturnFalse_Test() =>
		RecordCancellationAsync_InProgress_ShouldReturnFalse();

	[Fact]
	public Task RecordCancellationAsync_NonExistent_ShouldReturnFalse_Test() =>
		RecordCancellationAsync_NonExistent_ShouldReturnFalse();

	#endregion Cancellation Tests (STATE MACHINE)

	#region Scheduled Query Tests

	[Fact]
	public Task GetScheduledRequestsAsync_ShouldReturnDueRequests_Test() =>
		GetScheduledRequestsAsync_ShouldReturnDueRequests();

	[Fact]
	public Task GetScheduledRequestsAsync_ShouldOrderByScheduledTime_Test() =>
		GetScheduledRequestsAsync_ShouldOrderByScheduledTime();

	#endregion Scheduled Query Tests

	#region List Query Tests

	[Fact]
	public Task ListRequestsAsync_WithStatusFilter_ShouldFilterByStatus_Test() =>
		ListRequestsAsync_WithStatusFilter_ShouldFilterByStatus();

	[Fact]
	public Task ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant_Test() =>
		ListRequestsAsync_WithTenantFilter_ShouldFilterByTenant();

	[Fact]
	public Task ListRequestsAsync_WithDateRange_ShouldFilterByDates_Test() =>
		ListRequestsAsync_WithDateRange_ShouldFilterByDates();

	#endregion List Query Tests

	#region Certificate Tests

	[Fact]
	public Task SaveCertificateAsync_ShouldPersistCertificate_Test() =>
		SaveCertificateAsync_ShouldPersistCertificate();

	[Fact]
	public Task SaveCertificateAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() =>
		SaveCertificateAsync_DuplicateId_ShouldThrowInvalidOperationException();

	[Fact]
	public Task GetCertificateAsync_ByRequestId_ShouldReturnCertificate_Test() =>
		GetCertificateAsync_ByRequestId_ShouldReturnCertificate();

	[Fact]
	public Task GetCertificateByIdAsync_ShouldReturnCertificate_Test() =>
		GetCertificateByIdAsync_ShouldReturnCertificate();

	#endregion Certificate Tests

	#region Cleanup Tests

	[Fact]
	public Task CleanupExpiredCertificatesAsync_ShouldRemoveExpired_Test() =>
		CleanupExpiredCertificatesAsync_ShouldRemoveExpired();

	[Fact]
	public Task CleanupExpiredCertificatesAsync_ShouldKeepValid_Test() =>
		CleanupExpiredCertificatesAsync_ShouldKeepValid();

	#endregion Cleanup Tests
}
