// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Reporting;
using Excalibur.A3.Governance.Stores.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

using GrantType = Excalibur.A3.Authorization.Grants.GrantType;

namespace Excalibur.A3.Governance.Tests.Reporting;

/// <summary>
/// The entitlement reports, run against the real in-memory grant store -- resolved exactly as a consumer gets
/// it, from <c>AddExcaliburA3Core()</c> -- rather than a fake that answers whatever it is asked.
/// </summary>
/// <remarks>
/// The reports used to ask the store for grants whose type and qualifier were the empty string, meaning
/// "any". Every real store reads an empty string as a value, so every tenant report came back empty -- on a
/// compliance report that reaches an auditor, with nothing to say it was wrong. These arms fail on that.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EntitlementReportsAgainstARealGrantStoreShould : UnitTestBase
{
	private readonly IGrantStore _grants = new ServiceCollection().AddExcaliburA3Core().Services
		.BuildServiceProvider().GetRequiredService<IGrantStore>();
	private readonly InMemoryAccessReviewStore _reviews = new();

	private DefaultEntitlementReportProvider CreateSut() =>
		new(_grants, _reviews, sodEvaluator: null, orphanedDetector: null, principalTypeProvider: null,
			NullLogger<DefaultEntitlementReportProvider>.Instance);

	private async Task SeedAsync()
	{
		async Task Save(string user, string tenant, string type, string qualifier, DateTimeOffset? expiresOn = null) =>
			_ = await _grants.SaveGrantAsync(
				new Grant(user, user, tenant, type, qualifier, expiresOn, "admin", DateTimeOffset.UtcNow.AddDays(-10)),
				CancellationToken.None).ConfigureAwait(false);

		await Save("user-1", "tenant-1", GrantType.Role, "Admin").ConfigureAwait(false);
		await Save("user-1", "tenant-1", "Activity", "orders.read", DateTimeOffset.UtcNow.AddDays(5)).ConfigureAwait(false);
		await Save("user-2", "tenant-1", GrantType.Role, "Reader").ConfigureAwait(false);
		await Save("user-1", "tenant-2", GrantType.Role, "Admin").ConfigureAwait(false);
	}

	private static string[] Keys(EntitlementSnapshot snapshot) =>
		snapshot.Entries.Select(e => $"{e.UserId}|{e.GrantScope}").OrderBy(k => k, StringComparer.Ordinal).ToArray();

	[Fact]
	public async Task ReportEveryGrantInTheTenant_ForATenantSnapshot()
	{
		await SeedAsync();

		var snapshot = await CreateSut().GenerateTenantSnapshotAsync("tenant-1", CancellationToken.None);

		Keys(snapshot).ShouldBe(["user-1|Admin", "user-1|orders.read", "user-2|Reader"],
			customMessage: "a tenant report must list the tenant's grants -- and only that tenant's");
	}

	[Fact]
	public async Task ReportOneTenant_OrEveryTenant_WhenNoneIsNamed()
	{
		await SeedAsync();
		var sut = CreateSut();

		var one = await sut.GenerateReportAsync(EntitlementReportType.TenantEntitlements, "tenant-2", CancellationToken.None);
		var all = await sut.GenerateReportAsync(EntitlementReportType.TenantEntitlements, tenantId: null, CancellationToken.None);

		Keys(one).ShouldBe(["user-1|Admin"]);
		all.Entries.Count.ShouldBe(4, "with no tenant named, the operator report reads every tenant");
	}

	[Fact]
	public async Task ReportGrantsExpiringSoon_InTheTenant()
	{
		await SeedAsync();

		var snapshot = await CreateSut().GenerateReportAsync(
			EntitlementReportType.ExpiringGrants, "tenant-1", CancellationToken.None);

		Keys(snapshot).ShouldBe(["user-1|orders.read"]);
	}

	/// <summary>
	/// A grant is reviewed only by a completed campaign of its OWN tenant, judged by what the campaign's scope
	/// denotes. The report used to compare every campaign's filter value with every grant's qualifier -- a
	/// user id against a role name -- and ignored tenants altogether.
	/// </summary>
	[Fact]
	public async Task CountAGrantAsReviewed_OnlyWhenACompletedCampaignOfItsTenantCoveredIt()
	{
		await SeedAsync();
		var now = DateTimeOffset.UtcNow;

		async Task Completed(string id, string tenant, AccessReviewScope scope) =>
			await _reviews.SaveCampaignAsync(
				new AccessReviewCampaignSummary(id, tenant, id, scope, "officer", now.AddDays(-20), now.AddDays(-1),
					AccessReviewExpiryPolicy.DoNothing, AccessReviewState.Completed, 1, 1),
				CancellationToken.None).ConfigureAwait(false);

		// user-1 reviewed in tenant-1 only; the Reader role reviewed in tenant-2, where nobody holds it.
		await Completed("by-user", "tenant-1", new AccessReviewScope(AccessReviewScopeType.ByUser, "user-1"));
		await Completed("by-role", "tenant-2", new AccessReviewScope(AccessReviewScopeType.ByRole, "Reader"));

		var tenant1 = await CreateSut().GenerateReportAsync(
			EntitlementReportType.UnreviewedGrants, "tenant-1", CancellationToken.None);
		var tenant2 = await CreateSut().GenerateReportAsync(
			EntitlementReportType.UnreviewedGrants, "tenant-2", CancellationToken.None);

		Keys(tenant1).ShouldBe(["user-2|Reader"],
			customMessage: "user-1's tenant-1 grants were reviewed; user-2's Reader grant was reviewed only in ANOTHER tenant");
		Keys(tenant2).ShouldBe(["user-1|Admin"],
			customMessage: "a campaign in tenant-1 reviews nothing in tenant-2");
	}
}
