// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Author≠impl regression lock for bd-umemwa AC-D2 clause (b) (ADR-339, FR-2 fail-loud): when
/// <c>WithFencingTokens()</c> is configured but NO <see cref="IFencingTokenProvider"/> is registered, the
/// host MUST fail loud at startup rather than silently degrade to no split-brain protection.
/// </summary>
/// <remarks>
/// Non-vacuity (RED): the pre-fix <c>WithFencingTokens()</c> registered only the middleware and did NOT add
/// any startup guard, so a misconfigured host (fencing enabled, no provider) started silently and gave the
/// consumer zero fencing protection. The fix registers <c>FencingTokenPrerequisiteValidator</c> as an
/// <see cref="IHostedService"/> that throws at <c>StartAsync</c> when no provider resolves. This is a pure
/// DI/startup unit test (no container). The validator is internal, so it is exercised through the public
/// <see cref="IHostedService"/> seam rather than by name.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class FencingTokenFailLoudShould
{
	[Fact]
	public async Task FailLoudAtStartupWhenFencingEnabledWithoutAProvider()
	{
		// Arrange — fencing enabled, but NO IFencingTokenProvider registered.
		var services = new ServiceCollection();
		_ = new TestLeaderElectionBuilder(services).WithFencingTokens();

		await using var provider = services.BuildServiceProvider();
		var hostedServices = provider.GetServices<IHostedService>().ToList();
		hostedServices.ShouldNotBeEmpty("WithFencingTokens() must register the startup prerequisite validator");

		// Act + Assert — startup must fail loud.
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			foreach (var hosted in hostedServices)
			{
				await hosted.StartAsync(CancellationToken.None);
			}
		});

		ex.Message.ShouldContain("WithFencingTokens", Case.Insensitive);
	}

	[Fact]
	public async Task StartCleanlyWhenAProviderIsRegistered()
	{
		// Arrange — fencing enabled WITH a registered provider.
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IFencingTokenProvider>());
		_ = new TestLeaderElectionBuilder(services).WithFencingTokens();

		await using var provider = services.BuildServiceProvider();

		// Act + Assert — startup must NOT throw.
		foreach (var hosted in provider.GetServices<IHostedService>())
		{
			await hosted.StartAsync(CancellationToken.None);
		}
	}

	private sealed class TestLeaderElectionBuilder(IServiceCollection services) : ILeaderElectionBuilder
	{
		public IServiceCollection Services { get; } = services;
	}
}
