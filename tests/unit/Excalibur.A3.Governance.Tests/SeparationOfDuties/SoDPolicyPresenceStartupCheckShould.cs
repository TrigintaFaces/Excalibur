// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Governance.SeparationOfDuties;

namespace Excalibur.A3.Governance.Tests.SeparationOfDuties;

/// <summary>
/// Binds the boot-time separation of "no policies configured" from "the policy store came back empty for
/// some other reason" — two states an SoD evaluator cannot tell apart at evaluation time.
/// </summary>
/// <remarks>
/// An evaluator answering "no conflicts" because it could not load its policies is an authorizer that
/// permits on lookup failure: enforcement is entirely inert and nothing reports it. The hot path is
/// deliberately left alone — after a successful boot, an empty policy set genuinely means none are
/// configured, and returning no conflicts is then correct.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SoDPolicyPresenceStartupCheckShould
{
	// ---------- SAFETY ----------

	[Fact]
	public async Task Fail_startup_when_the_registered_policy_store_loads_nothing()
	{
		var check = new SoDPolicyPresenceStartupCheck(new FakeSoDPolicyStore());

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => check.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task Name_the_store_and_the_consequence_when_it_fails()
	{
		var check = new SoDPolicyPresenceStartupCheck(new FakeSoDPolicyStore());

		var error = await Should.ThrowAsync<InvalidOperationException>(
			() => check.StartAsync(CancellationToken.None));

		// An operator needs to know which store came back empty, and that the cost of continuing is
		// silently unenforced separation of duties — not merely that a check failed.
		error.Message.ShouldContain(nameof(FakeSoDPolicyStore));
		error.Message.ShouldContain("inert");
	}

	// ---------- LIVENESS ----------
	//
	// The arm that catches the over-correction. "Fail whenever policies are empty" satisfies every safety
	// arm above and breaks every deployment that legitimately has policies loaded.

	[Fact]
	public async Task Start_cleanly_when_the_store_has_policies()
	{
		var check = new SoDPolicyPresenceStartupCheck(
			new FakeSoDPolicyStore(
				new SoDPolicy(
					PolicyId: "sod-1",
					Name: "Maker cannot be checker",
					Description: null,
					Severity: SoDSeverity.Critical,
					PolicyScope: SoDPolicyScope.Activity,
					ConflictingItems: ["payment.create", "payment.approve"],
					TenantId: null,
					CreatedBy: "test")));

		await Should.NotThrowAsync(() => check.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task Stop_without_complaint()
	{
		var check = new SoDPolicyPresenceStartupCheck(new FakeSoDPolicyStore());

		await Should.NotThrowAsync(() => check.StopAsync(CancellationToken.None));
	}

	/// <summary>
	/// Implements <see cref="ISoDPolicyStore" /> directly, inheriting no first-party base — so the
	/// assertions bind the interface's contract rather than a base class that would supply it for free.
	/// </summary>
	private sealed class FakeSoDPolicyStore(params SoDPolicy[] policies) : ISoDPolicyStore
	{
		public Task<SoDPolicy?> GetPolicyAsync(string policyId, CancellationToken cancellationToken) =>
			Task.FromResult<SoDPolicy?>(null);

		public Task<IReadOnlyList<SoDPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<SoDPolicy>>(policies);

		public Task SavePolicyAsync(SoDPolicy policy, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<bool> DeletePolicyAsync(string policyId, CancellationToken cancellationToken) =>
			Task.FromResult(false);

		public object? GetService(Type serviceType) => null;
	}
}
