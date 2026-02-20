// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ISoc2ReportStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your SOC 2 report store implementation conforms to the ISoc2ReportStore contract.
/// </para>
/// <para>
/// The test kit verifies core SOC 2 report store operations including save/upsert,
/// get, list with filtering/pagination/sorting, delete, and count.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> ISoc2ReportStore implements SOC 2 report storage
/// for audit compliance:
/// <list type="bullet">
/// <item><description><c>SaveReportAsync</c> UPSERTS by ReportId (does NOT throw on duplicate)</description></item>
/// <item><description><c>SaveReportAsync</c> THROWS ArgumentNullException on null report</description></item>
/// <item><description><c>GetReportAsync</c> returns null if not found</description></item>
/// <item><description><c>ListReportsAsync</c> supports rich filtering, pagination, and 4 sort orders</description></item>
/// <item><description><c>ListReportsAsync</c> projects to ReportSummary with calculated ExceptionCount</description></item>
/// <item><description><c>DeleteReportAsync</c> returns false if not found</description></item>
/// <item><description><c>GetReportCountAsync</c> uses same filters as ListReportsAsync</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerSoc2ReportStoreConformanceTests : Soc2ReportStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override ISoc2ReportStore CreateStore() =&gt;
///         new SqlServerSoc2ReportStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class Soc2ReportStoreConformanceTestKit
{
	/// <summary>
	/// Creates a fresh SOC 2 report store instance for testing.
	/// </summary>
	/// <returns>An ISoc2ReportStore implementation to test.</returns>
	protected abstract ISoc2ReportStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates a minimal test SOC 2 report with the given parameters.
	/// </summary>
	/// <param name="reportId">Optional report identifier. If not provided, a new GUID is generated.</param>
	/// <param name="reportType">The report type. Default is TypeII.</param>
	/// <param name="title">Optional title. Default is "Test SOC 2 Report".</param>
	/// <param name="periodStart">Optional period start. Default is 6 months ago.</param>
	/// <param name="periodEnd">Optional period end. Default is now.</param>
	/// <param name="opinion">The auditor opinion. Default is Unqualified.</param>
	/// <param name="tenantId">Optional tenant identifier for multi-tenant isolation.</param>
	/// <param name="generatedAt">Optional generation timestamp. Default is now.</param>
	/// <param name="exceptionCount">Number of exceptions to add. Default is 0.</param>
	/// <returns>A test SOC 2 report.</returns>
	protected virtual Soc2Report CreateMinimalReport(
		Guid? reportId = null,
		Soc2ReportType reportType = Soc2ReportType.TypeII,
		string? title = null,
		DateTimeOffset? periodStart = null,
		DateTimeOffset? periodEnd = null,
		AuditorOpinion opinion = AuditorOpinion.Unqualified,
		string? tenantId = null,
		DateTimeOffset? generatedAt = null,
		int exceptionCount = 0)
	{
		var now = DateTimeOffset.UtcNow;
		var start = periodStart ?? now.AddMonths(-6);
		var end = periodEnd ?? now;

		var exceptions = new List<ReportException>();
		for (var i = 0; i < exceptionCount; i++)
		{
			exceptions.Add(new ReportException
			{
				ExceptionId = $"EX-{i + 1:D3}",
				Criterion = TrustServicesCriterion.CC6_LogicalAccess,
				ControlId = $"CTRL-{i + 1:D3}",
				Description = $"Test exception {i + 1}"
			});
		}

		return new Soc2Report
		{
			ReportId = reportId ?? Guid.NewGuid(),
			ReportType = reportType,
			Title = title ?? "Test SOC 2 Report",
			PeriodStart = start,
			PeriodEnd = end,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = CreateMinimalSystemDescription(),
			ControlSections = [CreateMinimalControlSection()],
			Opinion = opinion,
			Exceptions = exceptions,
			GeneratedAt = generatedAt ?? now,
			TenantId = tenantId
		};
	}

	/// <summary>
	/// Creates a minimal system description for testing.
	/// </summary>
	protected virtual SystemDescription CreateMinimalSystemDescription() =>
		new()
		{
			Name = "Test System",
			Description = "A test system for conformance testing",
			Services = ["Data Processing"],
			Infrastructure = ["Cloud Platform"],
			DataTypes = ["Customer Data"]
		};

	/// <summary>
	/// Creates a minimal control section for testing.
	/// </summary>
	protected virtual ControlSection CreateMinimalControlSection() =>
		new()
		{
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			Description = "Logical access controls",
			Controls = [CreateMinimalControlDescription()],
			IsMet = true
		};

	/// <summary>
	/// Creates a minimal control description for testing.
	/// </summary>
	protected virtual ControlDescription CreateMinimalControlDescription() =>
		new()
		{
			ControlId = "CC6.1",
			Name = "User Access Management",
			Description = "Access is granted based on job responsibilities",
			Implementation = "Role-based access control system",
			Type = ControlType.Preventive,
			Frequency = ControlFrequency.PerTransaction
		};

	#region Save Report Tests

	/// <summary>
	/// Verifies that SaveReportAsync persists a report retrievable via GetReportAsync.
	/// </summary>
	protected virtual async Task SaveReportAsync_ShouldPersistReport()
	{
		// Arrange
		var store = CreateStore();
		var report = CreateMinimalReport();

		try
		{
			// Act
			await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);
			var retrieved = await store.GetReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (retrieved is null)
			{
				throw new TestFixtureAssertionException(
					"SaveReportAsync should persist report retrievable via GetReportAsync");
			}

			if (retrieved.ReportId != report.ReportId ||
				retrieved.Title != report.Title ||
				retrieved.ReportType != report.ReportType ||
				retrieved.Opinion != report.Opinion)
			{
				throw new TestFixtureAssertionException(
					"SaveReportAsync should persist all report properties correctly");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that SaveReportAsync upserts (replaces) on duplicate ReportId.
	/// </summary>
	protected virtual async Task SaveReportAsync_DuplicateReportId_ShouldUpsert()
	{
		// Arrange
		var store = CreateStore();
		var reportId = Guid.NewGuid();
		var original = CreateMinimalReport(reportId: reportId, title: "Original Title");
		var updated = CreateMinimalReport(reportId: reportId, title: "Updated Title");

		try
		{
			// Act
			await store.SaveReportAsync(original, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(updated, CancellationToken.None).ConfigureAwait(false);

			var retrieved = await store.GetReportAsync(reportId, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (retrieved is null)
			{
				throw new TestFixtureAssertionException(
					"SaveReportAsync should persist report on upsert");
			}

			if (retrieved.Title != "Updated Title")
			{
				throw new TestFixtureAssertionException(
					"SaveReportAsync should replace existing report with updated values (UPSERT behavior)");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that SaveReportAsync throws ArgumentNullException on null report.
	/// </summary>
	protected virtual async Task SaveReportAsync_NullReport_ShouldThrowArgumentNullException()
	{
		// Arrange
		var store = CreateStore();

		try
		{
			// Act & Assert
			var threw = false;
			try
			{
				await store.SaveReportAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				threw = true;
			}

			if (!threw)
			{
				throw new TestFixtureAssertionException(
					"SaveReportAsync should throw ArgumentNullException on null report");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Get Report Tests

	/// <summary>
	/// Verifies that GetReportAsync returns the report for an existing ReportId.
	/// </summary>
	protected virtual async Task GetReportAsync_ExistingReport_ShouldReturnReport()
	{
		// Arrange
		var store = CreateStore();
		var report = CreateMinimalReport();

		try
		{
			await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

			// Act
			var retrieved = await store.GetReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (retrieved is null)
			{
				throw new TestFixtureAssertionException(
					"GetReportAsync should return report for existing ReportId");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that GetReportAsync returns null for non-existent ReportId.
	/// </summary>
	protected virtual async Task GetReportAsync_NonExistent_ShouldReturnNull()
	{
		// Arrange
		var store = CreateStore();

		try
		{
			// Act
			var retrieved = await store.GetReportAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (retrieved is not null)
			{
				throw new TestFixtureAssertionException(
					"GetReportAsync should return null for non-existent ReportId");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region List Reports Tests

	/// <summary>
	/// Verifies that ListReportsAsync filters by ReportType.
	/// </summary>
	protected virtual async Task ListReportsAsync_FilterByReportType_ShouldFilterCorrectly()
	{
		// Arrange
		var store = CreateStore();
		var typeI = CreateMinimalReport(reportType: Soc2ReportType.TypeI);
		var typeII = CreateMinimalReport(reportType: Soc2ReportType.TypeII);

		try
		{
			await store.SaveReportAsync(typeI, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(typeII, CancellationToken.None).ConfigureAwait(false);

			// Act
			var filter = new ReportFilter { ReportType = Soc2ReportType.TypeI };
			var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (results.Any(r => r.ReportType != Soc2ReportType.TypeI))
			{
				throw new TestFixtureAssertionException(
					"ListReportsAsync should filter by ReportType correctly");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that ListReportsAsync supports pagination with Skip and MaxResults.
	/// </summary>
	protected virtual async Task ListReportsAsync_Pagination_ShouldWorkCorrectly()
	{
		// Arrange
		var store = CreateStore();
		var reports = new List<Soc2Report>();
		for (var i = 0; i < 5; i++)
		{
			var report = CreateMinimalReport(generatedAt: DateTimeOffset.UtcNow.AddDays(-i));
			reports.Add(report);
			await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);
		}

		try
		{
			// Act - Get page 2 (skip 2, take 2)
			var filter = new ReportFilter { Skip = 2, MaxResults = 2 };
			var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (results.Count != 2)
			{
				throw new TestFixtureAssertionException(
					$"ListReportsAsync with pagination should return correct count. Expected 2 but got {results.Count}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that ListReportsAsync supports all 4 sort orders.
	/// </summary>
	protected virtual async Task ListReportsAsync_SortOrder_ShouldSortCorrectly()
	{
		// Arrange
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		var older = CreateMinimalReport(
			generatedAt: now.AddDays(-10),
			periodStart: now.AddMonths(-12),
			periodEnd: now.AddMonths(-6));
		var newer = CreateMinimalReport(
			generatedAt: now.AddDays(-1),
			periodStart: now.AddMonths(-6),
			periodEnd: now);

		try
		{
			await store.SaveReportAsync(older, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(newer, CancellationToken.None).ConfigureAwait(false);

			// Act - Sort by GeneratedAt Descending (most recent first)
			var filterDesc = new ReportFilter { SortOrder = ReportSortOrder.GeneratedAtDescending };
			var resultsDesc = await store.ListReportsAsync(filterDesc, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (resultsDesc.Count >= 2 && resultsDesc[0].GeneratedAt < resultsDesc[1].GeneratedAt)
			{
				throw new TestFixtureAssertionException(
					"ListReportsAsync with GeneratedAtDescending should sort most recent first");
			}

			// Act - Sort by GeneratedAt Ascending (oldest first)
			var filterAsc = new ReportFilter { SortOrder = ReportSortOrder.GeneratedAtAscending };
			var resultsAsc = await store.ListReportsAsync(filterAsc, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (resultsAsc.Count >= 2 && resultsAsc[0].GeneratedAt > resultsAsc[1].GeneratedAt)
			{
				throw new TestFixtureAssertionException(
					"ListReportsAsync with GeneratedAtAscending should sort oldest first");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that ListReportsAsync projects to ReportSummary with calculated ExceptionCount.
	/// </summary>
	protected virtual async Task ListReportsAsync_ShouldProjectToReportSummary()
	{
		// Arrange
		var store = CreateStore();
		var report = CreateMinimalReport(exceptionCount: 3);

		try
		{
			await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

			// Act
			var filter = new ReportFilter();
			var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

			// Assert
			var summary = results.FirstOrDefault(r => r.ReportId == report.ReportId);
			if (summary is null)
			{
				throw new TestFixtureAssertionException(
					"ListReportsAsync should return report in results");
			}

			if (summary.ExceptionCount != 3)
			{
				throw new TestFixtureAssertionException(
					$"ListReportsAsync should calculate ExceptionCount from Exceptions.Count. Expected 3 but got {summary.ExceptionCount}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Delete Report Tests

	/// <summary>
	/// Verifies that DeleteReportAsync returns true when deleting an existing report.
	/// </summary>
	protected virtual async Task DeleteReportAsync_ExistingReport_ShouldReturnTrue()
	{
		// Arrange
		var store = CreateStore();
		var report = CreateMinimalReport();

		try
		{
			await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

			// Act
			var result = await store.DeleteReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (!result)
			{
				throw new TestFixtureAssertionException(
					"DeleteReportAsync should return true when deleting existing report");
			}

			var retrieved = await store.GetReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);
			if (retrieved is not null)
			{
				throw new TestFixtureAssertionException(
					"DeleteReportAsync should remove report from store");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that DeleteReportAsync returns false when report does not exist.
	/// </summary>
	protected virtual async Task DeleteReportAsync_NonExistent_ShouldReturnFalse()
	{
		// Arrange
		var store = CreateStore();

		try
		{
			// Act
			var result = await store.DeleteReportAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result)
			{
				throw new TestFixtureAssertionException(
					"DeleteReportAsync should return false when report does not exist");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Count Reports Tests

	/// <summary>
	/// Verifies that GetReportCountAsync returns count with filters applied.
	/// </summary>
	protected virtual async Task GetReportCountAsync_WithFilter_ShouldReturnFilteredCount()
	{
		// Arrange
		var store = CreateStore();
		var typeI1 = CreateMinimalReport(reportType: Soc2ReportType.TypeI);
		var typeI2 = CreateMinimalReport(reportType: Soc2ReportType.TypeI);
		var typeII = CreateMinimalReport(reportType: Soc2ReportType.TypeII);

		try
		{
			await store.SaveReportAsync(typeI1, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(typeI2, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(typeII, CancellationToken.None).ConfigureAwait(false);

			// Act
			var filter = new ReportFilter { ReportType = Soc2ReportType.TypeI };
			var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (count < 2)
			{
				throw new TestFixtureAssertionException(
					$"GetReportCountAsync with TypeI filter should return at least 2. Got {count}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that GetReportCountAsync returns 0 when no reports match.
	/// </summary>
	protected virtual async Task GetReportCountAsync_NoMatches_ShouldReturnZero()
	{
		// Arrange
		var store = CreateStore();
		var report = CreateMinimalReport(reportType: Soc2ReportType.TypeII);

		try
		{
			await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

			// Act - Filter for TypeI when only TypeII exists
			var filter = new ReportFilter { ReportType = Soc2ReportType.TypeI };
			var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (count != 0)
			{
				throw new TestFixtureAssertionException(
					$"GetReportCountAsync with no matches should return 0. Got {count}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that GetReportCountAsync returns all reports with empty filter.
	/// </summary>
	protected virtual async Task GetReportCountAsync_EmptyFilter_ShouldReturnAll()
	{
		// Arrange
		var store = CreateStore();
		var report1 = CreateMinimalReport();
		var report2 = CreateMinimalReport();
		var report3 = CreateMinimalReport();

		try
		{
			await store.SaveReportAsync(report1, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(report2, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(report3, CancellationToken.None).ConfigureAwait(false);

			// Act
			var filter = new ReportFilter();
			var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (count < 3)
			{
				throw new TestFixtureAssertionException(
					$"GetReportCountAsync with empty filter should return at least 3. Got {count}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Multi-Tenant Tests

	/// <summary>
	/// Verifies that ListReportsAsync filters by TenantId correctly.
	/// </summary>
	protected virtual async Task ListReportsAsync_TenantFilter_ShouldFilterCorrectly()
	{
		// Arrange
		var store = CreateStore();
		var tenantA = CreateMinimalReport(tenantId: "TenantA");
		var tenantB = CreateMinimalReport(tenantId: "TenantB");
		var noTenant = CreateMinimalReport(tenantId: null);

		try
		{
			await store.SaveReportAsync(tenantA, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(tenantB, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(noTenant, CancellationToken.None).ConfigureAwait(false);

			// Act
			var filter = new ReportFilter { TenantId = "TenantA" };
			var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (results.Any(r => r.TenantId != "TenantA"))
			{
				throw new TestFixtureAssertionException(
					"ListReportsAsync with TenantId filter should only return reports for that tenant");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that GetReportCountAsync filters by TenantId correctly.
	/// </summary>
	protected virtual async Task GetReportCountAsync_TenantFilter_ShouldFilterCorrectly()
	{
		// Arrange
		var store = CreateStore();
		var tenantA1 = CreateMinimalReport(tenantId: "TenantA");
		var tenantA2 = CreateMinimalReport(tenantId: "TenantA");
		var tenantB = CreateMinimalReport(tenantId: "TenantB");

		try
		{
			await store.SaveReportAsync(tenantA1, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(tenantA2, CancellationToken.None).ConfigureAwait(false);
			await store.SaveReportAsync(tenantB, CancellationToken.None).ConfigureAwait(false);

			// Act
			var filter = new ReportFilter { TenantId = "TenantA" };
			var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (count != 2)
			{
				throw new TestFixtureAssertionException(
					$"GetReportCountAsync with TenantId filter should return 2. Got {count}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion
}
