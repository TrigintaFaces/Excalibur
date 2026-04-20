// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.Hosting;

namespace Excalibur.LeaderElection.Tests.DependencyInjection;

/// <summary>
/// Regression guard for bd-x6rg45: LeaderElection prerequisite validator must fail
/// loud at host start when the consumer calls <c>AddLeaderElection(...)</c> but omits a
/// concrete <see cref="ILeaderElection"/> provider, and must pass cleanly when a provider
/// is present.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class LeaderElectionPrerequisiteValidatorShould
{
	[Fact]
	public async Task Throw_WhenILeaderElectionIsMissing()
	{
		var services = new ServiceCollection();
		// Configure LeaderElection builder but deliberately omit the provider selection
		// (no UseInMemory() / UseSqlServer() / UseRedis()).
		_ = services.AddExcalibur(x => x.AddLeaderElection(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<LeaderElectionPrerequisiteValidator>()
			.SingleOrDefault();
		validator.ShouldNotBeNull(
			"AddLeaderElection(...) must register LeaderElectionPrerequisiteValidator as an IHostedService.");

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator!.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("ILeaderElection", Case.Sensitive);
		ex.Message.ShouldContain("AddLeaderElection", Case.Sensitive);
	}

	[Fact]
	public async Task Succeed_WhenILeaderElectionIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddLeaderElection(_ => { }));

		// Supply a consumer ILeaderElection fake — the validator should now pass.
		services.AddSingleton(A.Fake<ILeaderElection>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<LeaderElectionPrerequisiteValidator>()
			.Single();

		await validator.StartAsync(CancellationToken.None);
		// Reaching here without exception IS the success assertion.
	}

	[Fact]
	public void Register_Once_EvenWhenAddLeaderElectionCalledTwice()
	{
		// TryAddEnumerable must guarantee idempotence on repeated AddLeaderElection invocations.
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddLeaderElection(_ => { }));
		_ = services.AddExcalibur(x => x.AddLeaderElection(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var count = provider.GetServices<IHostedService>()
			.OfType<LeaderElectionPrerequisiteValidator>()
			.Count();

		count.ShouldBe(1);
	}
}
