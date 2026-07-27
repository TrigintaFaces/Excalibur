// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Hosting;

namespace Excalibur.A3.Governance.SeparationOfDuties;

/// <summary>
/// Fails startup when separation-of-duties enforcement is registered but its policy store loads no
/// policies.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISoDEvaluator" /> reports "no conflicts" when the policy set is empty, and that answer is
/// correct for a deployment which genuinely defines no policies. It is indistinguishable, at evaluation
/// time, from a store that returned empty because it was misconfigured — a wrong connection, an unmigrated
/// table, an implementation that swallows a load failure. In that second case every separation-of-duties
/// check silently passes: an authorizer permitting on lookup failure, with no signal that enforcement has
/// become inert.
/// </para>
/// <para>
/// The two states cannot be told apart from inside the evaluator, so they are told apart here instead. A
/// host that registered SoD enforcement asked for it; loading zero policies is then a misconfiguration and
/// startup fails, naming the store. A host that never registered SoD never reaches this check. Once
/// startup has succeeded, an empty policy set on the hot path genuinely means "none configured", and the
/// evaluator's existing answer stands unchanged — the hot path is deliberately untouched.
/// </para>
/// <para>
/// This runs as a hosted service because the check is asynchronous: the policy store is loaded through its
/// real interface, so the guarantee does not rest on any particular store's error behaviour.
/// </para>
/// </remarks>
internal sealed class SoDPolicyPresenceStartupCheck : IHostedService
{
	private readonly ISoDPolicyStore _policyStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="SoDPolicyPresenceStartupCheck" /> class.
	/// </summary>
	/// <param name="policyStore"> The policy store whose contents are checked at startup. </param>
	public SoDPolicyPresenceStartupCheck(ISoDPolicyStore policyStore) => _policyStore = policyStore;

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var policies = await _policyStore.GetAllPoliciesAsync(cancellationToken).ConfigureAwait(false);

		if (policies.Count > 0)
		{
			return;
		}

		throw new InvalidOperationException(
			$"Separation-of-duties enforcement is registered, but the configured policy store " +
			$"({_policyStore.GetType().Name}) loaded zero policies. Every separation-of-duties check would " +
			"report 'no conflicts' regardless of the grants held, so enforcement would be silently inert. " +
			"Load the policy set the deployment expects, or do not register separation-of-duties " +
			"enforcement for hosts that genuinely have no policies.");
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
