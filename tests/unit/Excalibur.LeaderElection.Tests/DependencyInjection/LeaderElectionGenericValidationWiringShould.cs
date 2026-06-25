// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.DependencyInjection;

/// <summary>
/// Author≠impl regression lock for <c>gmq2j7</c> (Sprint-846 Lane C): the cross-property timing rule
/// (<c>ol729k</c>: <c>Renew + Grace + skew &lt; Lease</c>) was previously enforced only on the Postgres
/// DI path. Both shared composition roots — the <strong>builder core</strong>
/// (<c>AddExcaliburLeaderElection(Action&lt;ILeaderElectionBuilder&gt;)</c>, the load-bearing funnel that
/// the named distributed providers SqlServer / Consul / Kubernetes / Mongo / Redis attach through) and
/// the <strong>generic overload</strong> (<c>AddExcaliburLeaderElection(Action&lt;LeaderElectionOptions&gt;)</c>)
/// — called <c>ValidateOnStart()</c> but registered no <see cref="IValidateOptions{LeaderElectionOptions}"/>,
/// so those consumers got <em>zero</em> cross-property validation. This lock proves <em>both</em> roots
/// now register the validator and reject a validation-passing split-brain config (the builder-path tests
/// guard the load-bearing root the named providers actually use; <c>gate-full-guard-suite</c>).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LeaderElectionGenericValidationWiringShould : UnitTestBase
{
	/// <summary>
	/// The <c>ol729k</c> split-brain config: each property is individually &lt; LeaseDuration, but the
	/// self-demotion deadline (Renew 10s + Grace 8s + 1s skew = 19s) exceeds the 15s lease — the renewal
	/// loop self-demotes after the lease has already expired, so another node can acquire the lease while
	/// this node still believes it leads. The per-property checks pass; only the cross-property rule fails.
	/// </summary>
	private static void ConfigureSplitBrain(LeaderElectionOptions options)
	{
		options.LeaseDuration = TimeSpan.FromSeconds(15);
		options.RenewInterval = TimeSpan.FromSeconds(10);
		options.GracePeriod = TimeSpan.FromSeconds(8);
	}

	[Fact]
	public void RegisterCrossPropertyValidator_OnGenericConfigurePath()
	{
		// Arrange + Act
		var services = new ServiceCollection();
		services.AddExcaliburLeaderElection(static (LeaderElectionOptions _) => { });

		// Assert — non-vacuity: pre-fix the generic path registered NO IValidateOptions for
		// LeaderElectionOptions (RED). The validator must now be present.
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<LeaderElectionOptions>) &&
			sd.ImplementationType == typeof(LeaderElectionOptionsValidator));
	}

	[Fact]
	public void RejectSplitBrainConfig_OnGenericConfigurePath()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburLeaderElection(ConfigureSplitBrain);
		using var provider = services.BuildServiceProvider();

		// Act + Assert — resolving IOptions<>.Value runs every registered
		// IValidateOptions<LeaderElectionOptions>. Non-vacuity: pre-fix no validator is registered on
		// the generic path, so the split-brain config is silently accepted (RED — no throw).
		Should.Throw<OptionsValidationException>(() =>
			_ = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value);
	}

	// ----- builder-core path (:42) — the LOAD-BEARING root the named providers (SqlServer/Consul/K8s/
	//       Mongo/Redis) flow through. A future removal of the builder-core registration would re-open
	//       the exact gmq2j7 gap while the generic-path tests above stay GREEN — hence these are required
	//       (gate-full-guard-suite: bind the seam that carries the invariant for the named providers). -----

	[Fact]
	public void RegisterCrossPropertyValidator_OnBuilderPath()
	{
		// Arrange + Act — the builder overload (Action<ILeaderElectionBuilder>), selecting a provider.
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburLeaderElection(static builder => builder.UseInMemory());

		// Assert — non-vacuity: pre-gmq2j7 the builder core registered NO IValidateOptions (RED).
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<LeaderElectionOptions>) &&
			sd.ImplementationType == typeof(LeaderElectionOptionsValidator));
	}

	[Fact]
	public void RejectSplitBrainConfig_OnBuilderPath()
	{
		// Arrange — builder path with a split-brain config applied via the builder's WithOptions hook
		// (Services.Configure of the default LeaderElectionOptions the validator reads).
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburLeaderElection(static builder =>
			builder.UseInMemory().WithOptions(ConfigureSplitBrain));
		using var provider = services.BuildServiceProvider();

		// Act + Assert — resolving .Value runs the builder-core-registered validator. Non-vacuity: pre-fix
		// the builder core registered no validator → the split-brain config is silently accepted (RED).
		Should.Throw<OptionsValidationException>(() =>
			_ = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value);
	}

	[Fact]
	public void AcceptShippedDefaults_OnGenericConfigurePath()
	{
		// Arrange — shipped defaults (Lease 15s / Renew 5s / Grace 5s → 11s margin) must remain valid,
		// guarding the wiring against rejecting good configs.
		var services = new ServiceCollection();
		services.AddExcaliburLeaderElection(static (LeaderElectionOptions _) => { });
		using var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;

		// Assert
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(15));
		(options.RenewInterval + options.GracePeriod).ShouldBeLessThan(options.LeaseDuration);
	}
}
