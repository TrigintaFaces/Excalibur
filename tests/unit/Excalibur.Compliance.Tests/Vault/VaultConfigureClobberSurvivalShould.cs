// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Vault;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Vault;

/// <summary>
/// Author≠impl regression lock for bead <c>r5r7fe</c> nit 4 (sprint 855): <c>AddVaultKeyManagement</c>'s
/// internal <c>Configure&lt;VaultOptions&gt;</c> MUST NOT clobber a consumer's previously-configured
/// sub-options. It must transfer the builder-owned fields <b>field-level</b> (never a wholesale
/// <c>opt.Auth = options.Auth</c> / <c>opt.Keys = options.Keys</c> sub-object replacement), so a
/// consumer's own <c>services.Configure&lt;VaultOptions&gt;(…)</c> registered <i>before</i>
/// <c>AddVaultKeyManagement</c> survives.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the fix (<c>issue-remediation-protocol</c>). This locks the regression first
/// surfaced as a caveat (Tests) then confirmed as a two-vector clobber (SoftwareArchitect/ProjectReviewer):
/// a wholesale sub-object copy runs AFTER the consumer's <c>Configure</c> (registration order) and
/// overwrites their <c>Auth</c>/<c>Retry</c>/<c>Suspension</c> with the builder's defaults.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> reference-equality (<c>ShouldBeSameAs</c>) is the clobber detector — the field-level
/// transfer leaves the consumer's sub-object instances untouched (GREEN), whereas a wholesale
/// <c>opt.X = options.X</c> replaces them with the builder's fresh defaults (different instance → RED).
/// RED-proven against a wholesale-copy mutation of <c>VaultServiceCollectionExtensions</c> in an isolated
/// worktree (no shared-tree mutation); GREEN on the committed field-level / <c>ApplyConfiguredFieldsTo</c>
/// surface.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class VaultConfigureClobberSurvivalShould
{
	[Fact]
	public void PreserveConsumerConfiguredSubObjectsThroughAddVaultKeyManagement()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// A consumer configures the sub-options BEFORE AddVaultKeyManagement (registration order matters:
		// AddVaultKeyManagement's own Configure runs AFTER this one).
		var consumerAuth = new VaultAuthOptions();
		var consumerRetry = new VaultRetryOptions();
		var consumerSuspension = new VaultSuspensionOptions();
		var consumerKeys = new VaultKeyOptions();
		_ = services.Configure<VaultOptions>(o =>
		{
			o.Auth = consumerAuth;
			o.Retry = consumerRetry;
			o.Suspension = consumerSuspension;
			o.Keys = consumerKeys;
		});

		_ = services.AddVaultKeyManagement(vault =>
			vault.VaultUri(new Uri("http://127.0.0.1:8200")).KeyNamePrefix("unit-"));

		using var provider = services.BuildServiceProvider();
		var opt = provider.GetRequiredService<IOptions<VaultOptions>>().Value;

		// The consumer's sub-object instances MUST survive — a wholesale `opt.X = options.X` copy would
		// replace them with the builder's fresh defaults (reference inequality → RED).
		opt.Auth.ShouldBeSameAs(consumerAuth);
		opt.Retry.ShouldBeSameAs(consumerRetry);
		opt.Suspension.ShouldBeSameAs(consumerSuspension);

		// m614qq: the `Keys` vector — the whole-`opt.Keys = options.Keys` wholesale copy (the vector SA
		// originally found) must NOT replace the consumer's Keys sub-object either. The builder owns
		// KeyNamePrefix, so it MUST transfer that field-level onto the SURVIVING consumer instance — never
		// swap the whole Keys object (which would drop any other consumer-set Keys sub-field).
		opt.Keys.ShouldBeSameAs(consumerKeys);

		// And the builder's own field still applies (field-level transfer onto the consumer's Keys instance,
		// not a regression of the builder path).
		opt.Keys.KeyNamePrefix.ShouldBe("unit-");
	}
}
