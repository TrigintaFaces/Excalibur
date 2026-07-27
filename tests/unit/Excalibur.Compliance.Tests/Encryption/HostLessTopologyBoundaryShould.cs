// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// Records, as an executable fact, the topology boundary of the sprint's fail-closed startup gates: they
/// fire on host start and therefore do not fire in a consumer that composes the container without running
/// an <see cref="IHost" />.
/// </summary>
/// <remarks>
/// <para>
/// This is a characterization test, not an endorsement. The gates are correct for hosted applications —
/// the mainstream case — and this file exists so their limitation is <em>falsifiable</em> rather than
/// tribal knowledge held in a sprint thread. The framework advertises serverless and manually-composed
/// scenarios, so the uncovered topology is a real one.
/// </para>
/// <para>
/// <strong>When a first-use floor is added, this test SHOULD fail.</strong> That failure is the signal
/// that the boundary moved, and the correct response is to update or delete this file — not to work around
/// it. A gate limitation that no test observes is exactly the "nothing would tell you" shape these gates
/// were written to remove, turned on the gates themselves.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class HostLessTopologyBoundaryShould
{
	[Fact]
	public void Not_validate_key_durability_when_the_container_is_built_without_starting_a_host()
	{
		// A host-less consumer: services composed, provider built, no IHost, no StartAsync.
		var services = new ServiceCollection();
		_ = services.AddKeyDurabilityGate();

		using var provider = services.BuildServiceProvider();

		// Building the provider runs no validation. ValidateOnStart is executed by the host's startup
		// validator, and there is no host here — so a misconfigured deployment reaches this point silently.
		Should.NotThrow(
			() => provider.GetService<IKeyManagementProvider>(),
			"building a container performs no startup validation; this documents the boundary rather than " +
			"asserting it is desirable");
	}

	[Fact]
	public void Still_reject_once_something_actually_resolves_the_validated_options()
	{
		// The other half of the boundary, and the reason the gates are not worthless host-less: the refusal
		// is real the moment anything triggers options resolution. What is missing host-less is a TRIGGER,
		// not the check.
		var services = new ServiceCollection();
		_ = services.AddKeyDurabilityGate();

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(
			() => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeyDurabilityOptions>>().Value);
	}
}
