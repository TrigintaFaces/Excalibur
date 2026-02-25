// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2;

/// <summary>
/// Unit tests for <see cref="InMemorySoc2ReportStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class InMemorySoc2ReportStoreShould
{
	private readonly InMemorySoc2ReportStore _sut;

	public InMemorySoc2ReportStoreShould()
	{
		_sut = new InMemorySoc2ReportStore();
	}

	#region SaveReportAsync Tests

	[Fact]
	public async Task SaveReportAsync_ThrowsArgumentNullException_WhenReportIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveReportAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SaveReportAsync_StoresReport()
	{
		// Arrange
		var report = CreateTestReport();

		// Act
		await _sut.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.ReportCount.ShouldBe(1);
	}

	[Fact]
	public async Task SaveReportAsync_OverwritesExistingReport()
	{
		// Arrange
		var report1 = CreateTestReport();
		var report2 = CreateTestReport(report1.ReportId) with { Title = "Updated Title" };

		// Act
		await _sut.SaveReportAsync(report1, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(report2, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.ReportCount.ShouldBe(1);
		var retrieved = await _sut.GetReportAsync(report1.ReportId, CancellationToken.None).ConfigureAwait(false);
		retrieved.Title.ShouldBe("Updated Title");
	}

	#endregion

	#region GetReportAsync Tests

	[Fact]
	public async Task GetReportAsync_ReturnsNull_WhenReportDoesNotExist()
	{
		// Act
		var result = await _sut.GetReportAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetReportAsync_ReturnsReport_WhenExists()
	{
		// Arrange
		var report = CreateTestReport();
		await _sut.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.ReportId.ShouldBe(report.ReportId);
		result.Title.ShouldBe(report.Title);
	}

	#endregion

	#region ListReportsAsync Tests

	[Fact]
	public async Task ListReportsAsync_ReturnsEmptyList_WhenNoReports()
	{
		// Act
		var result = await _sut.ListReportsAsync(new ReportFilter(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task ListReportsAsync_FiltersByReportType()
	{
		// Arrange
		var typeI = CreateTestReport() with { ReportType = Soc2ReportType.TypeI };
		var typeII = CreateTestReport() with { ReportType = Soc2ReportType.TypeII };
		await _sut.SaveReportAsync(typeI, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(typeII, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { ReportType = Soc2ReportType.TypeI },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].ReportType.ShouldBe(Soc2ReportType.TypeI);
	}

	[Fact]
	public async Task ListReportsAsync_FiltersByTenantId()
	{
		// Arrange
		var tenant1 = CreateTestReport() with { TenantId = "tenant-1" };
		var tenant2 = CreateTestReport() with { TenantId = "tenant-2" };
		await _sut.SaveReportAsync(tenant1, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(tenant2, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { TenantId = "tenant-1" },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public async Task ListReportsAsync_FiltersByGeneratedAfter()
	{
		// Arrange
		var cutoffDate = DateTimeOffset.UtcNow;
		var oldReport = CreateTestReport() with { GeneratedAt = cutoffDate.AddDays(-10) };
		var newReport = CreateTestReport() with { GeneratedAt = cutoffDate.AddDays(1) };
		await _sut.SaveReportAsync(oldReport, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(newReport, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { GeneratedAfter = cutoffDate },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].GeneratedAt.ShouldBeGreaterThan(cutoffDate);
	}

	[Fact]
	public async Task ListReportsAsync_FiltersByGeneratedBefore()
	{
		// Arrange
		var cutoffDate = DateTimeOffset.UtcNow;
		var oldReport = CreateTestReport() with { GeneratedAt = cutoffDate.AddDays(-10) };
		var newReport = CreateTestReport() with { GeneratedAt = cutoffDate.AddDays(1) };
		await _sut.SaveReportAsync(oldReport, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(newReport, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { GeneratedBefore = cutoffDate },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].GeneratedAt.ShouldBeLessThan(cutoffDate);
	}

	[Fact]
	public async Task ListReportsAsync_FiltersByPeriodStartAfter()
	{
		// Arrange
		var cutoffDate = DateTimeOffset.UtcNow;
		var oldReport = CreateTestReport() with { PeriodStart = cutoffDate.AddDays(-30) };
		var newReport = CreateTestReport() with { PeriodStart = cutoffDate.AddDays(1) };
		await _sut.SaveReportAsync(oldReport, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(newReport, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { PeriodStartAfter = cutoffDate },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].PeriodStart.ShouldBeGreaterThan(cutoffDate);
	}

	[Fact]
	public async Task ListReportsAsync_FiltersByPeriodEndBefore()
	{
		// Arrange
		var cutoffDate = DateTimeOffset.UtcNow;
		var earlyReport = CreateTestReport() with { PeriodEnd = cutoffDate.AddDays(-5) };
		var lateReport = CreateTestReport() with { PeriodEnd = cutoffDate.AddDays(5) };
		await _sut.SaveReportAsync(earlyReport, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(lateReport, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { PeriodEndBefore = cutoffDate },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].PeriodEnd.ShouldBeLessThan(cutoffDate);
	}

	[Fact]
	public async Task ListReportsAsync_FiltersByOpinion()
	{
		// Arrange
		var unqualified = CreateTestReport() with { Opinion = AuditorOpinion.Unqualified };
		var qualified = CreateTestReport() with { Opinion = AuditorOpinion.Qualified };
		await _sut.SaveReportAsync(unqualified, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(qualified, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { Opinion = AuditorOpinion.Unqualified },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].Opinion.ShouldBe(AuditorOpinion.Unqualified);
	}

	[Fact]
	public async Task ListReportsAsync_SortsByGeneratedAtDescending()
	{
		// Arrange
		var older = CreateTestReport() with { GeneratedAt = DateTimeOffset.UtcNow.AddDays(-5) };
		var newer = CreateTestReport() with { GeneratedAt = DateTimeOffset.UtcNow };
		await _sut.SaveReportAsync(older, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(newer, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { SortOrder = ReportSortOrder.GeneratedAtDescending },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
		result[0].GeneratedAt.ShouldBeGreaterThan(result[1].GeneratedAt);
	}

	[Fact]
	public async Task ListReportsAsync_SortsByGeneratedAtAscending()
	{
		// Arrange
		var older = CreateTestReport() with { GeneratedAt = DateTimeOffset.UtcNow.AddDays(-5) };
		var newer = CreateTestReport() with { GeneratedAt = DateTimeOffset.UtcNow };
		await _sut.SaveReportAsync(older, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(newer, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { SortOrder = ReportSortOrder.GeneratedAtAscending },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
		result[0].GeneratedAt.ShouldBeLessThan(result[1].GeneratedAt);
	}

	[Fact]
	public async Task ListReportsAsync_SortsByPeriodEndDescending()
	{
		// Arrange
		var earlier = CreateTestReport() with { PeriodEnd = DateTimeOffset.UtcNow.AddDays(-10) };
		var later = CreateTestReport() with { PeriodEnd = DateTimeOffset.UtcNow };
		await _sut.SaveReportAsync(earlier, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(later, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { SortOrder = ReportSortOrder.PeriodEndDescending },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
		result[0].PeriodEnd.ShouldBeGreaterThan(result[1].PeriodEnd);
	}

	[Fact]
	public async Task ListReportsAsync_SortsByPeriodEndAscending()
	{
		// Arrange
		var earlier = CreateTestReport() with { PeriodEnd = DateTimeOffset.UtcNow.AddDays(-10) };
		var later = CreateTestReport() with { PeriodEnd = DateTimeOffset.UtcNow };
		await _sut.SaveReportAsync(earlier, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(later, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { SortOrder = ReportSortOrder.PeriodEndAscending },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
		result[0].PeriodEnd.ShouldBeLessThan(result[1].PeriodEnd);
	}

	[Fact]
	public async Task ListReportsAsync_AppliesPagination_Skip()
	{
		// Arrange
		for (var i = 0; i < 5; i++)
		{
			await _sut.SaveReportAsync(CreateTestReport(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { Skip = 2 },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(3);
	}

	[Fact]
	public async Task ListReportsAsync_AppliesPagination_MaxResults()
	{
		// Arrange
		for (var i = 0; i < 5; i++)
		{
			await _sut.SaveReportAsync(CreateTestReport(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { MaxResults = 2 },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ListReportsAsync_AppliesPagination_SkipAndMaxResults()
	{
		// Arrange
		for (var i = 0; i < 10; i++)
		{
			await _sut.SaveReportAsync(CreateTestReport(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var result = await _sut.ListReportsAsync(
			new ReportFilter { Skip = 3, MaxResults = 4 },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(4);
	}

	[Fact]
	public async Task ListReportsAsync_ReturnsSummaries()
	{
		// Arrange
		var report = CreateTestReport();
		await _sut.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.ListReportsAsync(new ReportFilter(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		var summary = result[0];
		summary.ReportId.ShouldBe(report.ReportId);
		summary.ReportType.ShouldBe(report.ReportType);
		summary.Title.ShouldBe(report.Title);
		summary.PeriodStart.ShouldBe(report.PeriodStart);
		summary.PeriodEnd.ShouldBe(report.PeriodEnd);
		summary.GeneratedAt.ShouldBe(report.GeneratedAt);
		summary.Opinion.ShouldBe(report.Opinion);
		summary.ExceptionCount.ShouldBe(report.Exceptions.Count);
		summary.TenantId.ShouldBe(report.TenantId);
	}

	#endregion

	#region DeleteReportAsync Tests

	[Fact]
	public async Task DeleteReportAsync_ReturnsFalse_WhenReportDoesNotExist()
	{
		// Act
		var result = await _sut.DeleteReportAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task DeleteReportAsync_ReturnsTrue_WhenReportDeleted()
	{
		// Arrange
		var report = CreateTestReport();
		await _sut.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.DeleteReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		_sut.ReportCount.ShouldBe(0);
	}

	[Fact]
	public async Task DeleteReportAsync_RemovesReport()
	{
		// Arrange
		var report = CreateTestReport();
		await _sut.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

		// Act
		await _sut.DeleteReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);
		var retrieved = await _sut.GetReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		retrieved.ShouldBeNull();
	}

	#endregion

	#region GetReportCountAsync Tests

	[Fact]
	public async Task GetReportCountAsync_ReturnsZero_WhenNoReports()
	{
		// Act
		var result = await _sut.GetReportCountAsync(new ReportFilter(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task GetReportCountAsync_ReturnsCorrectCount()
	{
		// Arrange
		for (var i = 0; i < 5; i++)
		{
			await _sut.SaveReportAsync(CreateTestReport(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var result = await _sut.GetReportCountAsync(new ReportFilter(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(5);
	}

	[Fact]
	public async Task GetReportCountAsync_AppliesFilters()
	{
		// Arrange
		var typeI = CreateTestReport() with { ReportType = Soc2ReportType.TypeI };
		var typeII1 = CreateTestReport() with { ReportType = Soc2ReportType.TypeII };
		var typeII2 = CreateTestReport() with { ReportType = Soc2ReportType.TypeII };
		await _sut.SaveReportAsync(typeI, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(typeII1, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(typeII2, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetReportCountAsync(
			new ReportFilter { ReportType = Soc2ReportType.TypeII },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(2);
	}

	#endregion

	#region Clear Tests

	[Fact]
	public async Task Clear_RemovesAllReports()
	{
		// Arrange
		for (var i = 0; i < 5; i++)
		{
			await _sut.SaveReportAsync(CreateTestReport(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		_sut.Clear();

		// Assert
		_sut.ReportCount.ShouldBe(0);
	}

	#endregion

	#region ReportCount Tests

	[Fact]
	public void ReportCount_ReturnsZero_WhenEmpty()
	{
		// Assert
		_sut.ReportCount.ShouldBe(0);
	}

	[Fact]
	public async Task ReportCount_ReturnsCorrectCount()
	{
		// Arrange
		await _sut.SaveReportAsync(CreateTestReport(), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(CreateTestReport(), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveReportAsync(CreateTestReport(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.ReportCount.ShouldBe(3);
	}

	#endregion

	#region Helpers

	private static Soc2Report CreateTestReport(Guid? reportId = null) =>
		new()
		{
			ReportId = reportId ?? Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeI,
			Title = "Test Report",
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-180),
			PeriodEnd = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = new SystemDescription
			{
				Name = "Test System",
				Description = "Test",
				Services = ["Service1"],
				Infrastructure = ["Cloud"],
				DataTypes = ["Data"]
			},
			ControlSections = [],
			Opinion = AuditorOpinion.Unqualified,
			Exceptions = [],
			GeneratedAt = DateTimeOffset.UtcNow,
			TenantId = "test-tenant"
		};

	#endregion
}
