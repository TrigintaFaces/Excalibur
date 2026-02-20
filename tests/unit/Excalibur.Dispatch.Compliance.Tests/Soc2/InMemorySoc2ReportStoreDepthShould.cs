using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemorySoc2ReportStoreDepthShould
{
	[Fact]
	public async Task Filter_reports_by_tenant_id()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(tenantId: "tenant-1"), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(tenantId: "tenant-2"), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(tenantId: null), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { TenantId = "tenant-1" };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
		results[0].TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public async Task Filter_reports_by_generated_after()
	{
		var store = new InMemorySoc2ReportStore();
		var oldDate = DateTimeOffset.UtcNow.AddDays(-10);
		var newDate = DateTimeOffset.UtcNow;

		await store.SaveReportAsync(CreateReport(generatedAt: oldDate), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(generatedAt: newDate), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { GeneratedAfter = DateTimeOffset.UtcNow.AddDays(-1) };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task Filter_reports_by_generated_before()
	{
		var store = new InMemorySoc2ReportStore();
		var oldDate = DateTimeOffset.UtcNow.AddDays(-10);
		var newDate = DateTimeOffset.UtcNow;

		await store.SaveReportAsync(CreateReport(generatedAt: oldDate), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(generatedAt: newDate), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { GeneratedBefore = DateTimeOffset.UtcNow.AddDays(-5) };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task Filter_reports_by_period_start_after()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(periodStart: DateTimeOffset.UtcNow.AddDays(-60)), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(periodStart: DateTimeOffset.UtcNow.AddDays(-10)), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { PeriodStartAfter = DateTimeOffset.UtcNow.AddDays(-30) };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task Filter_reports_by_period_end_before()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(periodEnd: DateTimeOffset.UtcNow.AddDays(-60)), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(periodEnd: DateTimeOffset.UtcNow), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { PeriodEndBefore = DateTimeOffset.UtcNow.AddDays(-30) };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task Apply_skip_pagination()
	{
		var store = new InMemorySoc2ReportStore();
		for (var i = 0; i < 5; i++)
		{
			await store.SaveReportAsync(CreateReport(), CancellationToken.None).ConfigureAwait(false);
		}

		var filter = new ReportFilter
		{
			Skip = 2,
			SortOrder = ReportSortOrder.GeneratedAtAscending
		};
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(3);
	}

	[Fact]
	public async Task Sort_by_period_end_descending()
	{
		var store = new InMemorySoc2ReportStore();
		var earlier = CreateReport(periodEnd: DateTimeOffset.UtcNow.AddDays(-10));
		var later = CreateReport(periodEnd: DateTimeOffset.UtcNow);

		await store.SaveReportAsync(earlier, CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(later, CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { SortOrder = ReportSortOrder.PeriodEndDescending };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(2);
		results[0].PeriodEnd.ShouldBeGreaterThan(results[1].PeriodEnd);
	}

	[Fact]
	public async Task Sort_by_period_end_ascending()
	{
		var store = new InMemorySoc2ReportStore();
		var earlier = CreateReport(periodEnd: DateTimeOffset.UtcNow.AddDays(-10));
		var later = CreateReport(periodEnd: DateTimeOffset.UtcNow);

		await store.SaveReportAsync(earlier, CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(later, CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { SortOrder = ReportSortOrder.PeriodEndAscending };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(2);
		results[0].PeriodEnd.ShouldBeLessThan(results[1].PeriodEnd);
	}

	[Fact]
	public async Task Sort_by_generated_at_descending_by_default()
	{
		var store = new InMemorySoc2ReportStore();
		var older = CreateReport(generatedAt: DateTimeOffset.UtcNow.AddDays(-5));
		var newer = CreateReport(generatedAt: DateTimeOffset.UtcNow);

		await store.SaveReportAsync(older, CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(newer, CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { SortOrder = ReportSortOrder.GeneratedAtDescending };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(2);
		results[0].GeneratedAt.ShouldBeGreaterThan(results[1].GeneratedAt);
	}

	[Fact]
	public async Task Combine_multiple_filters()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeI, AuditorOpinion.Unqualified, "t1"), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeI, AuditorOpinion.Adverse, "t1"), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeII, AuditorOpinion.Unqualified, "t2"), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter
		{
			ReportType = Soc2ReportType.TypeI,
			Opinion = AuditorOpinion.Unqualified,
			TenantId = "t1"
		};
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task Get_report_count_with_all_filters()
	{
		var store = new InMemorySoc2ReportStore();
		var now = DateTimeOffset.UtcNow;

		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeI, tenantId: "t1", generatedAt: now), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeII, tenantId: "t1", generatedAt: now), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeI, tenantId: "t2", generatedAt: now.AddDays(-10)), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter
		{
			TenantId = "t1",
			GeneratedAfter = now.AddDays(-1)
		};
		var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

		count.ShouldBe(2);
	}

	[Fact]
	public async Task Get_report_count_filtered_by_period_start_after()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(periodStart: DateTimeOffset.UtcNow.AddDays(-60)), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(periodStart: DateTimeOffset.UtcNow.AddDays(-5)), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { PeriodStartAfter = DateTimeOffset.UtcNow.AddDays(-30) };
		var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

		count.ShouldBe(1);
	}

	[Fact]
	public async Task Get_report_count_filtered_by_period_end_before()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(periodEnd: DateTimeOffset.UtcNow.AddDays(-60)), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(periodEnd: DateTimeOffset.UtcNow), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { PeriodEndBefore = DateTimeOffset.UtcNow.AddDays(-30) };
		var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

		count.ShouldBe(1);
	}

	[Fact]
	public async Task Get_report_count_filtered_by_generated_before()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(generatedAt: DateTimeOffset.UtcNow.AddDays(-10)), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(generatedAt: DateTimeOffset.UtcNow), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { GeneratedBefore = DateTimeOffset.UtcNow.AddDays(-5) };
		var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

		count.ShouldBe(1);
	}

	[Fact]
	public async Task Get_report_count_filtered_by_opinion()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(opinion: AuditorOpinion.Unqualified), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(opinion: AuditorOpinion.Adverse), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(opinion: AuditorOpinion.Unqualified), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { Opinion = AuditorOpinion.Adverse };
		var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

		count.ShouldBe(1);
	}

	[Fact]
	public async Task Report_summary_has_correct_exception_count()
	{
		var store = new InMemorySoc2ReportStore();
		var report = CreateReport() with
		{
			Exceptions =
			[
				new ReportException
				{
					ExceptionId = "EXC-001",
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					ControlId = "ctrl-1",
					Description = "Test exception"
				}
			]
		};

		await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

		var results = await store.ListReportsAsync(new ReportFilter(), CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
		results[0].ExceptionCount.ShouldBe(1);
	}

	[Fact]
	public async Task Report_summary_has_categories_included()
	{
		var store = new InMemorySoc2ReportStore();
		var report = CreateReport();

		await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

		var results = await store.ListReportsAsync(new ReportFilter(), CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
		results[0].CategoriesIncluded.ShouldContain(TrustServicesCategory.Security);
	}

	[Fact]
	public async Task Overwrite_report_with_same_id()
	{
		var store = new InMemorySoc2ReportStore();
		var reportId = Guid.NewGuid();
		var original = CreateReport() with { ReportId = reportId, Title = "Original" };
		var updated = CreateReport() with { ReportId = reportId, Title = "Updated" };

		await store.SaveReportAsync(original, CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(updated, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetReportAsync(reportId, CancellationToken.None).ConfigureAwait(false);

		retrieved.ShouldNotBeNull();
		retrieved.Title.ShouldBe("Updated");
		store.ReportCount.ShouldBe(1);
	}

	private static Soc2Report CreateReport(
		Soc2ReportType type = Soc2ReportType.TypeI,
		AuditorOpinion opinion = AuditorOpinion.Unqualified,
		string? tenantId = null,
		DateTimeOffset? generatedAt = null,
		DateTimeOffset? periodStart = null,
		DateTimeOffset? periodEnd = null) =>
		new()
		{
			ReportId = Guid.NewGuid(),
			ReportType = type,
			Title = "Test Report",
			PeriodStart = periodStart ?? DateTimeOffset.UtcNow.AddDays(-30),
			PeriodEnd = periodEnd ?? DateTimeOffset.UtcNow,
			GeneratedAt = generatedAt ?? DateTimeOffset.UtcNow,
			Opinion = opinion,
			ControlSections = [],
			Exceptions = [],
			CategoriesIncluded = [TrustServicesCategory.Security],
			TenantId = tenantId,
			System = new SystemDescription
			{
				Name = "Test",
				Description = "Test",
				Services = ["S"],
				Infrastructure = ["I"],
				DataTypes = ["D"]
			}
		};
}
