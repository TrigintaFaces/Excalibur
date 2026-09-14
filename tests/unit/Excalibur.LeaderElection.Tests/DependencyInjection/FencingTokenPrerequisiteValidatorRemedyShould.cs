// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.DependencyInjection;

using Microsoft.Extensions.Hosting;

namespace Excalibur.LeaderElection.Tests.DependencyInjection;

/// <summary>
/// Regression lock for the team-lead's post-integration condition on b8ht6u: the startup failure a
/// consumer hits when a built-in provider's fencing-token construction throws (store unreachable, not
/// yet configured) must name BOTH remedies -- fix the ordering, or call WithoutFencingTokens() -- not
/// just surface the raw underlying exception.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class FencingTokenPrerequisiteValidatorRemedyShould
{
	[Fact]
	public void NameBothRemedies_WhenTheUnderlyingStoreClientFailsToConstruct()
	{
		// Arrange -- Kubernetes with no fake IKubernetes client supplied, so the real
		// KubernetesClientConfiguration.BuildConfigFromConfigFile() path runs and throws (no kubeconfig
		// on this machine) the moment IFencingTokenProvider is resolved -- the exact "store not yet
		// reachable at startup" failure mode the condition is about.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburKubernetesLeaderElection(k => k.LeaseName("remedy-check"));
		using var provider = services.BuildServiceProvider();

		var validators = provider.GetServices<IHostedService>().OfType<FencingTokenPrerequisiteValidator>().ToList();
		validators.ShouldHaveSingleItem("the standalone Kubernetes entry point must register the validator");

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => validators[0].Validate());
		ex.InnerException.ShouldNotBeNull("the real construction failure must be preserved, not swallowed");
		ex.Message.ShouldContain("WithoutFencingTokens", Case.Insensitive);
		ex.Message.ShouldContain("reachable", Case.Insensitive);
	}
}
