// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Governance.OrphanedAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using GrantRecord = Excalibur.A3.Abstractions.Authorization.Grant;

namespace Excalibur.A3.Governance.Tests.OrphanedAccess;

/// <summary>
/// Unit tests for <see cref="DefaultOrphanedAccessDetector"/>: active users skip,
/// inactive/departed/unknown flagged with correct actions, provider failures
/// degrade gracefully, tenant filtering, and GetService escape hatch.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DefaultOrphanedAccessDetectorShould : UnitTestBase
{
	private readonly IGrantStore _grantStore = A.Fake<IGrantStore>();
	private readonly IUserStatusProvider _userStatusProvider = A.Fake<IUserStatusProvider>();
	private readonly IActivityGroupGrantStore _activityGroupGrantStore = A.Fake<IActivityGroupGrantStore>();
	private readonly OrphanedAccessOptions _options = new();
	private readonly DefaultOrphanedAccessDetector _sut;

	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
	private static readonly string[] ExpectedTenantAScopes = ["Admin", "HR"];

	public DefaultOrphanedAccessDetectorShould()
	{
		// Wire GetService to return the activity group grant store
		A.CallTo(() => _grantStore.GetService(typeof(IActivityGroupGrantStore)))
			.Returns(_activityGroupGrantStore);

		var opts = Microsoft.Extensions.Options.Options.Create(_options);

		_sut = new DefaultOrphanedAccessDetector(
			_grantStore,
			_userStatusProvider,
			opts,
			NullLogger<DefaultOrphanedAccessDetector>.Instance);
	}

	private static GrantRecord MakeGrant(string userId, string grantType, string qualifier,
		string? tenantId = "tenant-1") =>
		new(userId, "User", tenantId, grantType, qualifier, null, "admin", Now);

	private void SetupUserIds(params string[] userIds)
	{
		foreach (var grantType in new[] { GrantType.Activity, GrantType.ActivityGroup, GrantType.Role })
		{
			A.CallTo(() => _activityGroupGrantStore.GetDistinctActivityGroupGrantUserIdsAsync(
				grantType, A<CancellationToken>._))
				.Returns(Task.FromResult<IReadOnlyList<string>>([]));
		}

		// Return all user IDs for ActivityGroup grant type
		A.CallTo(() => _activityGroupGrantStore.GetDistinctActivityGroupGrantUserIdsAsync(
			GrantType.ActivityGroup, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<string>>(userIds));
	}

	private void SetupUserIdsAcrossGrantTypes(
		string[] activityUsers,
		string[] activityGroupUsers,
		string[] roleUsers)
	{
		A.CallTo(() => _activityGroupGrantStore.GetDistinctActivityGroupGrantUserIdsAsync(
			GrantType.Activity, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<string>>(activityUsers));

		A.CallTo(() => _activityGroupGrantStore.GetDistinctActivityGroupGrantUserIdsAsync(
			GrantType.ActivityGroup, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<string>>(activityGroupUsers));

		A.CallTo(() => _activityGroupGrantStore.GetDistinctActivityGroupGrantUserIdsAsync(
			GrantType.Role, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<string>>(roleUsers));
	}

	#region Active Users (no orphans)

	[Fact]
	public async Task ReturnEmptyReport_WhenAllUsersAreActive()
	{
		// Arrange
		SetupUserIds("user-1", "user-2");

		A.CallTo(() => _userStatusProvider.GetStatusAsync(A<string>._, A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Active, null));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.OrphanedGrants.ShouldBeEmpty();
		report.TotalUsersScanned.ShouldBe(2);
		report.TenantId.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnEmptyReport_WhenNoUsersExist()
	{
		// Arrange
		SetupUserIds();

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.OrphanedGrants.ShouldBeEmpty();
		report.TotalUsersScanned.ShouldBe(0);
	}

	#endregion

	#region Inactive Users

	[Fact]
	public async Task FlagInactiveUserGrants_WhenAutoRevokeDisabled()
	{
		// Arrange
		_options.AutoRevokeAfterGracePeriod = false;
		SetupUserIds("user-1");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-1", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Inactive, null));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-1", GrantType.ActivityGroup, "Reports")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.OrphanedGrants.Count.ShouldBe(1);
		var orphan = report.OrphanedGrants[0];
		orphan.UserId.ShouldBe("user-1");
		orphan.UserStatus.ShouldBe(PrincipalStatus.Inactive);
		orphan.RecommendedAction.ShouldBe(OrphanedAccessAction.Flag);
		orphan.GrantScope.ShouldBe("Reports");
	}

	[Fact]
	public async Task RevokeInactiveUserGrants_WhenAutoRevokeEnabledAndPastGracePeriod()
	{
		// Arrange
		_options.AutoRevokeAfterGracePeriod = true;
		_options.InactiveGracePeriodDays = 30;
		SetupUserIds("user-1");

		// Inactive for 45 days (past 30-day grace period)
		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-1", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Inactive, Now.AddDays(-45)));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-1", GrantType.ActivityGroup, "Reports")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.OrphanedGrants.Count.ShouldBe(1);
		report.OrphanedGrants[0].RecommendedAction.ShouldBe(OrphanedAccessAction.Revoke);
	}

	[Fact]
	public async Task FlagInactiveUserGrants_WhenAutoRevokeEnabledButWithinGracePeriod()
	{
		// Arrange
		_options.AutoRevokeAfterGracePeriod = true;
		_options.InactiveGracePeriodDays = 30;
		SetupUserIds("user-1");

		// Inactive for only 10 days (within 30-day grace period)
		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-1", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Inactive, Now.AddDays(-10)));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-1", GrantType.ActivityGroup, "Reports")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert -- within grace period, so Flag not Revoke
		report.OrphanedGrants.Count.ShouldBe(1);
		report.OrphanedGrants[0].RecommendedAction.ShouldBe(OrphanedAccessAction.Flag);
	}

	[Fact]
	public async Task FlagInactiveUserGrants_WhenAutoRevokeEnabledButNoTimestamp()
	{
		// Arrange
		_options.AutoRevokeAfterGracePeriod = true;
		SetupUserIds("user-1");

		// No StatusChangedAt -> conservative: Flag
		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-1", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Inactive, null));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-1", GrantType.ActivityGroup, "Reports")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert -- no timestamp = conservative Flag
		report.OrphanedGrants.Count.ShouldBe(1);
		report.OrphanedGrants[0].RecommendedAction.ShouldBe(OrphanedAccessAction.Flag);
	}

	#endregion

	#region Departed Users

	[Fact]
	public async Task FlagDepartedUserGrants_WhenAutoRevokeDisabled()
	{
		// Arrange
		_options.AutoRevokeDeparted = false;
		SetupUserIds("user-departed");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-departed", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Departed, null));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-departed", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-departed", GrantType.Role, "Admin")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.OrphanedGrants.Count.ShouldBe(1);
		report.OrphanedGrants[0].RecommendedAction.ShouldBe(OrphanedAccessAction.Flag);
		report.OrphanedGrants[0].UserStatus.ShouldBe(PrincipalStatus.Departed);
	}

	[Fact]
	public async Task RevokeDepartedUserGrants_WhenAutoRevokeEnabled()
	{
		// Arrange
		_options.AutoRevokeDeparted = true;
		SetupUserIds("user-departed");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-departed", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Departed, null));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-departed", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-departed", GrantType.Role, "Admin"),
				MakeGrant("user-departed", GrantType.ActivityGroup, "Finance")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.OrphanedGrants.Count.ShouldBe(2);
		report.OrphanedGrants.ShouldAllBe(g => g.RecommendedAction == OrphanedAccessAction.Revoke);
	}

	#endregion

	#region Unknown Status

	[Fact]
	public async Task RecommendInvestigate_WhenStatusIsUnknown()
	{
		// Arrange
		SetupUserIds("user-unknown");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-unknown", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Unknown, null));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-unknown", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-unknown", GrantType.Activity, "ViewDashboard")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.OrphanedGrants.Count.ShouldBe(1);
		report.OrphanedGrants[0].RecommendedAction.ShouldBe(OrphanedAccessAction.Investigate);
		report.OrphanedGrants[0].UserStatus.ShouldBe(PrincipalStatus.Unknown);
	}

	#endregion

	#region Provider Failure (partial report)

	[Fact]
	public async Task TreatAsUnknown_WhenStatusProviderThrows()
	{
		// Arrange
		SetupUserIds("user-ok", "user-fail");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-ok", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Active, null));

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-fail", A<CancellationToken>._))
			.Throws(new InvalidOperationException("Provider unavailable"));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-fail", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-fail", GrantType.ActivityGroup, "HR")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert -- provider failure = Unknown = Investigate
		report.TotalUsersScanned.ShouldBe(2);
		report.OrphanedGrants.Count.ShouldBe(1);
		report.OrphanedGrants[0].UserId.ShouldBe("user-fail");
		report.OrphanedGrants[0].UserStatus.ShouldBe(PrincipalStatus.Unknown);
		report.OrphanedGrants[0].RecommendedAction.ShouldBe(OrphanedAccessAction.Investigate);
	}

	[Fact]
	public async Task ContinueScan_WhenOneUserStatusFails()
	{
		// Arrange
		SetupUserIds("user-fail", "user-inactive");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-fail", A<CancellationToken>._))
			.Throws(new TimeoutException("HR system timeout"));

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-inactive", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Inactive, null));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-fail", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-fail", GrantType.Role, "Viewer")
			]));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-inactive", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-inactive", GrantType.ActivityGroup, "Audit")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert -- both users still produce orphaned grants
		report.TotalUsersScanned.ShouldBe(2);
		report.OrphanedGrants.Count.ShouldBe(2);
	}

	#endregion

	#region Tenant Filtering

	[Fact]
	public async Task FilterByTenant_WhenTenantIdSpecified()
	{
		// Arrange
		SetupUserIds("user-1");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-1", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Departed, null));

		_options.AutoRevokeDeparted = true;

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-1", GrantType.Role, "Admin", tenantId: "tenant-A"),
				MakeGrant("user-1", GrantType.Role, "Viewer", tenantId: "tenant-B"),
				MakeGrant("user-1", GrantType.ActivityGroup, "HR", tenantId: "tenant-A")
			]));

		// Act
		var report = await _sut.DetectAsync("tenant-A", CancellationToken.None);

		// Assert -- only tenant-A grants included
		report.TenantId.ShouldBe("tenant-A");
		report.OrphanedGrants.Count.ShouldBe(2);
		report.OrphanedGrants.Select(g => g.GrantScope).ShouldBe(
			ExpectedTenantAScopes, ignoreOrder: true);
	}

	[Fact]
	public async Task IncludeAllTenants_WhenTenantIdIsNull()
	{
		// Arrange
		SetupUserIds("user-1");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-1", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Inactive, null));

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-1", GrantType.Role, "Admin", tenantId: "tenant-A"),
				MakeGrant("user-1", GrantType.Role, "Viewer", tenantId: "tenant-B")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert -- all tenants included
		report.OrphanedGrants.Count.ShouldBe(2);
	}

	#endregion

	#region User Deduplication Across Grant Types

	[Fact]
	public async Task DeduplicateUsersAcrossGrantTypes()
	{
		// Arrange -- same user returned from multiple grant types
		SetupUserIdsAcrossGrantTypes(
			activityUsers: ["user-1", "user-2"],
			activityGroupUsers: ["user-1", "user-3"],
			roleUsers: ["user-2", "user-3"]);

		A.CallTo(() => _userStatusProvider.GetStatusAsync(A<string>._, A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Active, null));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert -- 3 distinct users, not 6
		report.TotalUsersScanned.ShouldBe(3);
		report.OrphanedGrants.ShouldBeEmpty();
	}

	#endregion

	#region Mixed Statuses

	[Fact]
	public async Task HandleMixedStatuses_AcrossMultipleUsers()
	{
		// Arrange
		SetupUserIds("active-user", "inactive-user", "departed-user", "unknown-user");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("active-user", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Active, null));
		A.CallTo(() => _userStatusProvider.GetStatusAsync("inactive-user", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Inactive, null));
		A.CallTo(() => _userStatusProvider.GetStatusAsync("departed-user", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Departed, null));
		A.CallTo(() => _userStatusProvider.GetStatusAsync("unknown-user", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Unknown, null));

		foreach (var userId in new[] { "inactive-user", "departed-user", "unknown-user" })
		{
			A.CallTo(() => _grantStore.GetAllGrantsAsync(userId, A<CancellationToken>._))
				.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
					MakeGrant(userId, GrantType.ActivityGroup, "SomeScope")
				]));
		}

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.TotalUsersScanned.ShouldBe(4);
		report.OrphanedGrants.Count.ShouldBe(3); // active-user skipped
		report.OrphanedGrants.ShouldNotContain(g => g.UserId == "active-user");
	}

	#endregion

	#region No IActivityGroupGrantStore

	[Fact]
	public async Task ReturnEmptyReport_WhenGrantStoreDoesNotSupportActivityGroupStore()
	{
		// Arrange -- GetService returns null (no IActivityGroupGrantStore)
		A.CallTo(() => _grantStore.GetService(typeof(IActivityGroupGrantStore)))
			.Returns(null);

		var opts = Microsoft.Extensions.Options.Options.Create(new OrphanedAccessOptions());
		var sut = new DefaultOrphanedAccessDetector(
			_grantStore,
			_userStatusProvider,
			opts,
			NullLogger<DefaultOrphanedAccessDetector>.Instance);

		// Act
		var report = await sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.TotalUsersScanned.ShouldBe(0);
		report.OrphanedGrants.ShouldBeEmpty();
	}

	#endregion

	#region GetService

	[Fact]
	public void ReturnNull_FromGetService()
	{
		_sut.GetService(typeof(string)).ShouldBeNull();
		_sut.GetService(typeof(IOrphanedAccessDetector)).ShouldBeNull();
	}

	[Fact]
	public void ThrowOnGetService_WhenNull()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	#endregion

	#region Report Properties

	[Fact]
	public async Task SetGeneratedAtToRecentTimestamp()
	{
		// Arrange
		SetupUserIds();
		var before = DateTimeOffset.UtcNow;

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		var after = DateTimeOffset.UtcNow;
		report.GeneratedAt.ShouldBeGreaterThanOrEqualTo(before);
		report.GeneratedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public async Task PopulateGrantedOn_FromOriginalGrant()
	{
		// Arrange
		SetupUserIds("user-1");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-1", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Departed, null));

		_options.AutoRevokeDeparted = true;

		var grantDate = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				new GrantRecord("user-1", "User", "tenant-1", GrantType.Role, "Admin",
					null, "admin", grantDate)
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.OrphanedGrants[0].GrantedOn.ShouldBe(grantDate);
	}

	#endregion

	#region Multiple Grants Per User

	[Fact]
	public async Task DetectAllGrants_ForSingleOrphanedUser()
	{
		// Arrange
		SetupUserIds("user-1");

		A.CallTo(() => _userStatusProvider.GetStatusAsync("user-1", A<CancellationToken>._))
			.Returns(new PrincipalStatusResult(PrincipalStatus.Departed, null));

		_options.AutoRevokeDeparted = true;

		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<GrantRecord>>([
				MakeGrant("user-1", GrantType.Role, "Admin"),
				MakeGrant("user-1", GrantType.ActivityGroup, "Finance"),
				MakeGrant("user-1", GrantType.Activity, "ViewReports")
			]));

		// Act
		var report = await _sut.DetectAsync(null, CancellationToken.None);

		// Assert
		report.OrphanedGrants.Count.ShouldBe(3);
		report.OrphanedGrants.ShouldAllBe(g => g.UserId == "user-1");
		report.OrphanedGrants.ShouldAllBe(g => g.RecommendedAction == OrphanedAccessAction.Revoke);
	}

	#endregion
}
