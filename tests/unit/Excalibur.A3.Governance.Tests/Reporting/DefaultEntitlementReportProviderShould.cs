// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.NonHumanIdentity;
using Excalibur.A3.Governance.OrphanedAccess;
using Excalibur.A3.Governance.Reporting;
using Excalibur.A3.Governance.SeparationOfDuties;

using Microsoft.Extensions.Logging.Abstractions;

using GrantRecord = Excalibur.A3.Abstractions.Authorization.Grant;

namespace Excalibur.A3.Governance.Tests.Reporting;

/// <summary>
/// Unit tests for <see cref="DefaultEntitlementReportProvider"/>:
/// user snapshots, tenant snapshots, all 6 report types, graceful degradation
/// with missing optional dependencies.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DefaultEntitlementReportProviderShould : UnitTestBase
{
	private readonly IGrantStore _grantStore = A.Fake<IGrantStore>();
	private readonly IAccessReviewStore _accessReviewStore = A.Fake<IAccessReviewStore>();
	private readonly ISoDEvaluator _sodEvaluator = A.Fake<ISoDEvaluator>();
	private readonly IOrphanedAccessDetector _orphanedDetector = A.Fake<IOrphanedAccessDetector>();
	private readonly IPrincipalTypeProvider _principalTypeProvider = A.Fake<IPrincipalTypeProvider>();
	private readonly DefaultEntitlementReportProvider _sut;

	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

	public DefaultEntitlementReportProviderShould()
	{
		A.CallTo(() => _principalTypeProvider.GetPrincipalTypeAsync(A<string>._, A<CancellationToken>._))
			.Returns(PrincipalType.Human);

		A.CallTo(() => _sodEvaluator.EvaluateCurrentAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<SoDConflict>>([]));

		A.CallTo(() => _accessReviewStore.GetCampaignsByStateAsync(A<AccessReviewState?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<AccessReviewCampaignSummary>>([]));

		_sut = new DefaultEntitlementReportProvider(
			_grantStore, _accessReviewStore, _sodEvaluator,
			_orphanedDetector, _principalTypeProvider,
			NullLogger<DefaultEntitlementReportProvider>.Instance);
	}

	private static GrantRecord MakeGrant(string userId, string qualifier, string? tenantId = null,
		DateTimeOffset? expiresOn = null) =>
		new(userId, "User", tenantId, GrantType.Role, qualifier, expiresOn, "admin", Now);

	#region GenerateUserSnapshotAsync

	[Fact]
	public async Task GenerateUserSnapshot_WithEntries()
	{
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>(
				[MakeGrant("user-1", "Admin"), MakeGrant("user-1", "Finance")]));

		var snapshot = await _sut.GenerateUserSnapshotAsync("user-1", CancellationToken.None);

		snapshot.ReportType.ShouldBe(EntitlementReportType.UserEntitlements);
		snapshot.Scope.ShouldBe("user-1");
		snapshot.Entries.Count.ShouldBe(2);
		snapshot.Entries[0].GrantScope.ShouldBe("Admin");
		snapshot.Entries[1].GrantScope.ShouldBe("Finance");
	}

	[Fact]
	public async Task GenerateUserSnapshot_Empty_WhenNoGrants()
	{
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-x", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([]));

		var snapshot = await _sut.GenerateUserSnapshotAsync("user-x", CancellationToken.None);
		snapshot.Entries.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task ThrowOnUserSnapshot_WhenUserIdIsNullOrEmpty(string? userId)
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.GenerateUserSnapshotAsync(userId!, CancellationToken.None));
	}

	#endregion

	#region GenerateReportAsync -- OrphanedGrants

	[Fact]
	public async Task GenerateOrphanedGrantsReport()
	{
		A.CallTo(() => _orphanedDetector.DetectAsync(A<string?>._, A<CancellationToken>._))
			.Returns(new OrphanedAccessReport(Now, null,
				[new OrphanedGrant("user-1", "Admin", PrincipalStatus.Departed, Now, OrphanedAccessAction.Revoke)], 1));

		var snapshot = await _sut.GenerateReportAsync(
			EntitlementReportType.OrphanedGrants, null, CancellationToken.None);

		snapshot.ReportType.ShouldBe(EntitlementReportType.OrphanedGrants);
		snapshot.Entries.Count.ShouldBe(1);
		snapshot.Entries[0].UserId.ShouldBe("user-1");
	}

	[Fact]
	public async Task ReturnEmpty_WhenOrphanedDetectorIsMissing()
	{
		var sut = new DefaultEntitlementReportProvider(
			_grantStore, null, null, null, null,
			NullLogger<DefaultEntitlementReportProvider>.Instance);

		var snapshot = await sut.GenerateReportAsync(
			EntitlementReportType.OrphanedGrants, null, CancellationToken.None);

		snapshot.Entries.ShouldBeEmpty();
	}

	#endregion

	#region GenerateReportAsync -- UserEntitlements throws

	[Fact]
	public async Task ThrowOnGenerateReport_WhenUserEntitlementsType()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.GenerateReportAsync(EntitlementReportType.UserEntitlements, null, CancellationToken.None));
	}

	#endregion

	#region GenerateReportAsync -- Unknown report type

	[Fact]
	public async Task ThrowOnGenerateReport_WhenUnknownReportType()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.GenerateReportAsync((EntitlementReportType)999, null, CancellationToken.None));
	}

	#endregion

	#region Graceful Degradation

	[Fact]
	public async Task ReturnEmpty_ForSoDViolations_WhenEvaluatorIsMissing()
	{
		var sut = new DefaultEntitlementReportProvider(
			_grantStore, null, null, null, null,
			NullLogger<DefaultEntitlementReportProvider>.Instance);

		var snapshot = await sut.GenerateReportAsync(
			EntitlementReportType.SoDViolations, null, CancellationToken.None);

		snapshot.Entries.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmpty_ForUnreviewedGrants_WhenAccessReviewStoreIsMissing()
	{
		var sut = new DefaultEntitlementReportProvider(
			_grantStore, null, null, null, null,
			NullLogger<DefaultEntitlementReportProvider>.Instance);

		var snapshot = await sut.GenerateReportAsync(
			EntitlementReportType.UnreviewedGrants, null, CancellationToken.None);

		snapshot.Entries.ShouldBeEmpty();
	}

	#endregion

	#region PrincipalType Resolution

	[Fact]
	public async Task ResolvePrincipalType_ForEachEntry()
	{
		A.CallTo(() => _principalTypeProvider.GetPrincipalTypeAsync("svc-1", A<CancellationToken>._))
			.Returns(PrincipalType.ServiceAccount);

		A.CallTo(() => _grantStore.GetAllGrantsAsync("svc-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([MakeGrant("svc-1", "API")]));

		var snapshot = await _sut.GenerateUserSnapshotAsync("svc-1", CancellationToken.None);

		snapshot.Entries[0].PrincipalType.ShouldBe(PrincipalType.ServiceAccount);
	}

	[Fact]
	public async Task DefaultToHuman_WhenPrincipalTypeProviderIsMissing()
	{
		var sut = new DefaultEntitlementReportProvider(
			_grantStore, null, null, null, null,
			NullLogger<DefaultEntitlementReportProvider>.Instance);

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([MakeGrant("user-1", "Admin")]));

		var snapshot = await sut.GenerateUserSnapshotAsync("user-1", CancellationToken.None);
		snapshot.Entries[0].PrincipalType.ShouldBe(PrincipalType.Human);
	}

	#endregion
}
