// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Governance;
using Excalibur.A3.Governance.SeparationOfDuties;

using GrantRecord = Excalibur.A3.Abstractions.Authorization.Grant;

namespace Excalibur.A3.Governance.Tests.SeparationOfDuties;

/// <summary>
/// Unit tests for <see cref="DefaultSoDEvaluator"/>: current and hypothetical
/// conflict evaluation across role-scoped and activity-scoped policies.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DefaultSoDEvaluatorShould : UnitTestBase
{
	private readonly ISoDPolicyStore _policyStore = A.Fake<ISoDPolicyStore>();
	private readonly IGrantStore _grantStore = A.Fake<IGrantStore>();
	private readonly DefaultSoDEvaluator _sut;

	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

	public DefaultSoDEvaluatorShould()
	{
		_sut = new DefaultSoDEvaluator(_policyStore, _grantStore);
	}

	private static GrantRecord MakeGrant(string userId, string grantType, string qualifier) =>
		new(userId, "User", "tenant-1", grantType, qualifier, null, "admin", Now);

	private static SoDPolicy MakePolicy(
		string id = "policy-1",
		SoDPolicyScope scope = SoDPolicyScope.Role,
		SoDSeverity severity = SoDSeverity.Violation,
		params string[] conflictingItems) =>
		new(id, $"Policy {id}", null, severity, scope, conflictingItems, null, "admin");

	#region EvaluateCurrentAsync -- No Conflicts

	[Fact]
	public async Task ReturnEmpty_WhenNoPolicies()
	{
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(Array.Empty<SoDPolicy>());

		var result = await _sut.EvaluateCurrentAsync("user-1", CancellationToken.None);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmpty_WhenNoGrants()
	{
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[] { MakePolicy("p1", SoDPolicyScope.Role, SoDSeverity.Violation, "Admin", "Finance") });
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Array.Empty<GrantRecord>());

		var result = await _sut.EvaluateCurrentAsync("user-1", CancellationToken.None);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmpty_WhenUserHasOnlyOneSideOfConflict()
	{
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[] { MakePolicy("p1", SoDPolicyScope.Role, SoDSeverity.Violation, "Admin", "Finance") });
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(new[] { MakeGrant("user-1", GrantType.Role, "Admin") });

		var result = await _sut.EvaluateCurrentAsync("user-1", CancellationToken.None);
		result.ShouldBeEmpty();
	}

	#endregion

	#region EvaluateCurrentAsync -- Conflicts Detected

	[Fact]
	public async Task DetectConflict_WhenUserHasBothRoles()
	{
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[] { MakePolicy("p1", SoDPolicyScope.Role, SoDSeverity.Critical, "Admin", "Finance") });
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(new[]
			{
				MakeGrant("user-1", GrantType.Role, "Admin"),
				MakeGrant("user-1", GrantType.Role, "Finance"),
			});

		var result = await _sut.EvaluateCurrentAsync("user-1", CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].PolicyId.ShouldBe("p1");
		result[0].UserId.ShouldBe("user-1");
		result[0].ConflictingItem1.ShouldBe("Admin");
		result[0].ConflictingItem2.ShouldBe("Finance");
		result[0].Severity.ShouldBe(SoDSeverity.Critical);
	}

	[Fact]
	public async Task DetectConflict_ForActivityScopedPolicy()
	{
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[] { MakePolicy("p1", SoDPolicyScope.Activity, SoDSeverity.Violation, "CreatePayment", "ApprovePayment") });
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(new[]
			{
				MakeGrant("user-1", GrantType.Activity, "CreatePayment"),
				MakeGrant("user-1", GrantType.Activity, "ApprovePayment"),
			});

		var result = await _sut.EvaluateCurrentAsync("user-1", CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].ConflictingItem1.ShouldBe("CreatePayment");
		result[0].ConflictingItem2.ShouldBe("ApprovePayment");
	}

	[Fact]
	public async Task DetectMultiplePairs_ForNWayConflict()
	{
		// 3-way conflict: user has all 3 -> 3 pairs (A-B, A-C, B-C)
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[] { MakePolicy("p1", SoDPolicyScope.Role, SoDSeverity.Violation, "Admin", "Finance", "HR") });
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(new[]
			{
				MakeGrant("user-1", GrantType.Role, "Admin"),
				MakeGrant("user-1", GrantType.Role, "Finance"),
				MakeGrant("user-1", GrantType.Role, "HR"),
			});

		var result = await _sut.EvaluateCurrentAsync("user-1", CancellationToken.None);
		result.Count.ShouldBe(3); // 3 pairs from 3 items
	}

	[Fact]
	public async Task DetectConflicts_AcrossMultiplePolicies()
	{
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[]
			{
				MakePolicy("p1", SoDPolicyScope.Role, SoDSeverity.Violation, "Admin", "Finance"),
				MakePolicy("p2", SoDPolicyScope.Role, SoDSeverity.Warning, "Admin", "Audit"),
			});
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(new[]
			{
				MakeGrant("user-1", GrantType.Role, "Admin"),
				MakeGrant("user-1", GrantType.Role, "Finance"),
				MakeGrant("user-1", GrantType.Role, "Audit"),
			});

		var result = await _sut.EvaluateCurrentAsync("user-1", CancellationToken.None);
		result.Count.ShouldBe(2); // One conflict per policy
		result.ShouldContain(c => c.PolicyId == "p1");
		result.ShouldContain(c => c.PolicyId == "p2");
	}

	#endregion

	#region EvaluateHypotheticalAsync

	[Fact]
	public async Task DetectHypotheticalConflict_WhenProposedScopeCreatesConflict()
	{
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[] { MakePolicy("p1", SoDPolicyScope.Role, SoDSeverity.Violation, "Admin", "Finance") });
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(new[] { MakeGrant("user-1", GrantType.Role, "Admin") });

		// Propose adding "Finance" role
		var result = await _sut.EvaluateHypotheticalAsync("user-1", "Finance", GrantType.Role, CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].PolicyId.ShouldBe("p1");
	}

	[Fact]
	public async Task ReturnEmpty_WhenProposedScopeDoesNotCreateConflict()
	{
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[] { MakePolicy("p1", SoDPolicyScope.Role, SoDSeverity.Violation, "Admin", "Finance") });
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(new[] { MakeGrant("user-1", GrantType.Role, "Admin") });

		// Propose adding "HR" (not in conflict set)
		var result = await _sut.EvaluateHypotheticalAsync("user-1", "HR", GrantType.Role, CancellationToken.None);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task DetectHypotheticalConflict_EvenWithNoExistingGrants()
	{
		// Hypothetical scope is added to both role and activity sets
		A.CallTo(() => _policyStore.GetAllPoliciesAsync(A<CancellationToken>._))
			.Returns(new[] { MakePolicy("p1", SoDPolicyScope.Role, SoDSeverity.Violation, "Admin", "Finance") });
		A.CallTo(() => _grantStore.GetAllGrantsAsync("user-1", A<CancellationToken>._))
			.Returns(Array.Empty<GrantRecord>());

		// No existing grants, proposing "Admin" -> only 1 item in set, no conflict
		var result = await _sut.EvaluateHypotheticalAsync("user-1", "Admin", GrantType.Role, CancellationToken.None);
		result.ShouldBeEmpty();
	}

	#endregion

	#region Argument Validation

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task ThrowOnNullOrEmptyUserId_Current(string? userId)
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.EvaluateCurrentAsync(userId!, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task ThrowOnNullOrEmptyUserId_Hypothetical(string? userId)
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.EvaluateHypotheticalAsync(userId!, "scope", GrantType.Role, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task ThrowOnNullOrEmptyProposedScope(string? scope)
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_sut.EvaluateHypotheticalAsync("user-1", scope!, GrantType.Role, CancellationToken.None));
	}

	#endregion

	#region GetService

	[Fact]
	public void ReturnNull_FromGetService()
	{
		_sut.GetService(typeof(string)).ShouldBeNull();
	}

	[Fact]
	public void ThrowOnNullServiceType()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	#endregion
}
