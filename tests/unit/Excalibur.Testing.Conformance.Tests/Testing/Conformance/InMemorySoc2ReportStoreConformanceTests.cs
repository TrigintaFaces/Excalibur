// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemorySoc2ReportStore"/> validating ISoc2ReportStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemorySoc2ReportStore uses instance-level ConcurrentDictionary with no static state,
/// so no special isolation is required beyond using fresh store instances.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> ISoc2ReportStore implements SOC 2 report storage (ADR-055).
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>SaveReportAsync UPSERTS by ReportId (does NOT throw on duplicate)</description></item>
/// <item><description>SaveReportAsync THROWS ArgumentNullException on null report</description></item>
/// <item><description>GetReportAsync returns null if not found</description></item>
/// <item><description>ListReportsAsync supports rich filtering, pagination, and 4 sort orders</description></item>
/// <item><description>ListReportsAsync projects to ReportSummary with calculated ExceptionCount</description></item>
/// <item><description>DeleteReportAsync returns false if not found</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "STORE")]
public class InMemorySoc2ReportStoreConformanceTests : Soc2ReportStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override ISoc2ReportStore CreateStore() => new InMemorySoc2ReportStore();

	#region Save Report Tests

	[Fact]
	public Task SaveReportAsync_ShouldPersistReport_Test() =>
		SaveReportAsync_ShouldPersistReport();

	[Fact]
	public Task SaveReportAsync_DuplicateReportId_ShouldUpsert_Test() =>
		SaveReportAsync_DuplicateReportId_ShouldUpsert();

	[Fact]
	public Task SaveReportAsync_NullReport_ShouldThrowArgumentNullException_Test() =>
		SaveReportAsync_NullReport_ShouldThrowArgumentNullException();

	#endregion Save Report Tests

	#region Get Report Tests

	[Fact]
	public Task GetReportAsync_ExistingReport_ShouldReturnReport_Test() =>
		GetReportAsync_ExistingReport_ShouldReturnReport();

	[Fact]
	public Task GetReportAsync_NonExistent_ShouldReturnNull_Test() =>
		GetReportAsync_NonExistent_ShouldReturnNull();

	#endregion Get Report Tests

	#region List Reports Tests

	[Fact]
	public Task ListReportsAsync_FilterByReportType_ShouldFilterCorrectly_Test() =>
		ListReportsAsync_FilterByReportType_ShouldFilterCorrectly();

	[Fact]
	public Task ListReportsAsync_Pagination_ShouldWorkCorrectly_Test() =>
		ListReportsAsync_Pagination_ShouldWorkCorrectly();

	[Fact]
	public Task ListReportsAsync_SortOrder_ShouldSortCorrectly_Test() =>
		ListReportsAsync_SortOrder_ShouldSortCorrectly();

	[Fact]
	public Task ListReportsAsync_ShouldProjectToReportSummary_Test() =>
		ListReportsAsync_ShouldProjectToReportSummary();

	#endregion List Reports Tests

	#region Delete Report Tests

	[Fact]
	public Task DeleteReportAsync_ExistingReport_ShouldReturnTrue_Test() =>
		DeleteReportAsync_ExistingReport_ShouldReturnTrue();

	[Fact]
	public Task DeleteReportAsync_NonExistent_ShouldReturnFalse_Test() =>
		DeleteReportAsync_NonExistent_ShouldReturnFalse();

	#endregion Delete Report Tests

	#region Count Reports Tests

	[Fact]
	public Task GetReportCountAsync_WithFilter_ShouldReturnFilteredCount_Test() =>
		GetReportCountAsync_WithFilter_ShouldReturnFilteredCount();

	[Fact]
	public Task GetReportCountAsync_NoMatches_ShouldReturnZero_Test() =>
		GetReportCountAsync_NoMatches_ShouldReturnZero();

	[Fact]
	public Task GetReportCountAsync_EmptyFilter_ShouldReturnAll_Test() =>
		GetReportCountAsync_EmptyFilter_ShouldReturnAll();

	#endregion Count Reports Tests

	#region Multi-Tenant Tests

	[Fact]
	public Task ListReportsAsync_TenantFilter_ShouldFilterCorrectly_Test() =>
		ListReportsAsync_TenantFilter_ShouldFilterCorrectly();

	[Fact]
	public Task GetReportCountAsync_TenantFilter_ShouldFilterCorrectly_Test() =>
		GetReportCountAsync_TenantFilter_ShouldFilterCorrectly();

	#endregion Multi-Tenant Tests
}
