using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemorySoc2ReportStoreShould
{
	[Fact]
	public async Task Save_and_retrieve_report()
	{
		var store = new InMemorySoc2ReportStore();
		var report = CreateReport();

		await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);

		retrieved.ShouldNotBeNull();
		retrieved.ReportId.ShouldBe(report.ReportId);
		retrieved.Title.ShouldBe(report.Title);
	}

	[Fact]
	public async Task Return_null_for_unknown_report()
	{
		var store = new InMemorySoc2ReportStore();

		var result = await store.GetReportAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task Throw_for_null_report()
	{
		var store = new InMemorySoc2ReportStore();

		await Should.ThrowAsync<ArgumentNullException>(
			() => store.SaveReportAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Delete_existing_report()
	{
		var store = new InMemorySoc2ReportStore();
		var report = CreateReport();
		await store.SaveReportAsync(report, CancellationToken.None).ConfigureAwait(false);

		var deleted = await store.DeleteReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);

		deleted.ShouldBeTrue();
		var retrieved = await store.GetReportAsync(report.ReportId, CancellationToken.None).ConfigureAwait(false);
		retrieved.ShouldBeNull();
	}

	[Fact]
	public async Task Return_false_when_deleting_unknown_report()
	{
		var store = new InMemorySoc2ReportStore();

		var result = await store.DeleteReportAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task List_reports_with_no_filter()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(), CancellationToken.None).ConfigureAwait(false);

		var results = await store.ListReportsAsync(new ReportFilter(), CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task Filter_reports_by_type()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeI), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeII), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { ReportType = Soc2ReportType.TypeI };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
		results[0].ReportType.ShouldBe(Soc2ReportType.TypeI);
	}

	[Fact]
	public async Task Filter_reports_by_opinion()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(opinion: AuditorOpinion.Unqualified), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(opinion: AuditorOpinion.Adverse), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { Opinion = AuditorOpinion.Unqualified };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
		results[0].Opinion.ShouldBe(AuditorOpinion.Unqualified);
	}

	[Fact]
	public async Task Apply_pagination()
	{
		var store = new InMemorySoc2ReportStore();
		for (var i = 0; i < 5; i++)
		{
			await store.SaveReportAsync(CreateReport(), CancellationToken.None).ConfigureAwait(false);
		}

		var filter = new ReportFilter { MaxResults = 2 };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task Get_report_count_with_filter()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeI), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeI), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(Soc2ReportType.TypeII), CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { ReportType = Soc2ReportType.TypeI };
		var count = await store.GetReportCountAsync(filter, CancellationToken.None).ConfigureAwait(false);

		count.ShouldBe(2);
	}

	[Fact]
	public void Track_report_count()
	{
		var store = new InMemorySoc2ReportStore();

		store.ReportCount.ShouldBe(0);
	}

	[Fact]
	public async Task Clear_all_reports()
	{
		var store = new InMemorySoc2ReportStore();
		await store.SaveReportAsync(CreateReport(), CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(CreateReport(), CancellationToken.None).ConfigureAwait(false);

		store.Clear();

		store.ReportCount.ShouldBe(0);
	}

	[Fact]
	public async Task Sort_reports_ascending()
	{
		var store = new InMemorySoc2ReportStore();
		var older = CreateReport();
		var newer = CreateReport();
		await store.SaveReportAsync(older, CancellationToken.None).ConfigureAwait(false);
		await store.SaveReportAsync(newer, CancellationToken.None).ConfigureAwait(false);

		var filter = new ReportFilter { SortOrder = ReportSortOrder.GeneratedAtAscending };
		var results = await store.ListReportsAsync(filter, CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(2);
	}

	private static Soc2Report CreateReport(
		Soc2ReportType type = Soc2ReportType.TypeI,
		AuditorOpinion opinion = AuditorOpinion.Unqualified) =>
		new()
		{
			ReportId = Guid.NewGuid(),
			ReportType = type,
			Title = "Test Report",
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-30),
			PeriodEnd = DateTimeOffset.UtcNow,
			GeneratedAt = DateTimeOffset.UtcNow,
			Opinion = opinion,
			ControlSections = [],
			Exceptions = [],
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = new SystemDescription
			{
				Name = "Test System",
				Description = "Test system description",
				Services = ["TestService"],
				Infrastructure = ["TestInfra"],
				DataTypes = ["TestData"]
			}
		};
}
