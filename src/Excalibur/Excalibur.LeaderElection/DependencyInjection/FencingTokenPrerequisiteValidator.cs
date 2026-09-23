// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.LeaderElection.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/> if a
/// registered <see cref="IFencingTokenProvider"/> cannot be resolved — whether because it was never
/// registered (an explicit <c>WithFencingTokens()</c> opt-in with no provider) or because resolving it
/// threw (a built-in provider's underlying store client failed to construct, e.g. an unreachable Redis
/// or a missing kubeconfig).
/// </summary>
/// <remarks>
/// <para>
/// A dependency fencing requires but cannot satisfy must fail at composition time, not silently
/// degrade. A consumer who believes a leader election is fenced and gets <em>no</em> split-brain
/// protection is strictly worse off than one who gets a clear startup failure. This is the Microsoft
/// <c>IOptions&lt;T&gt;</c> + <c>ValidateOnStart()</c> fail-fast contract. Mirrors
/// <see cref="LeaderElectionPrerequisiteValidator"/>.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetService&lt;IFencingTokenProvider&gt;()</c> — no
/// reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class FencingTokenPrerequisiteValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public FencingTokenPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	public void Validate()
	{
		if (_services.GetService<FencingOptOutMarker>() is not null)
		{
			// The consumer explicitly called WithoutFencingTokens(): a null provider is the intended,
			// non-fencing-mode outcome here, not a misconfiguration to fail loud on.
			return;
		}

		IFencingTokenProvider? provider;
		try
		{
			provider = _services.GetService<IFencingTokenProvider>();
		}
		catch (Exception ex)
		{
			// Fencing is on by default, so this now runs for every built-in provider, not just an
			// explicit WithFencingTokens() opt-in — and resolving the provider eagerly constructs the
			// store's underlying client (e.g. Redis connects, Kubernetes reads a kubeconfig). A store
			// that is not yet reachable at startup (a Redis container racing the host, ordering that
			// was never required under the old lazy resolution) throws here instead of failing later.
			// Name both remedies: fix the ordering, or opt out explicitly if this host cannot guarantee
			// the store is reachable at startup.
			throw new InvalidOperationException(
				"Excalibur leader election fencing is enabled by default and failed to construct its " +
				"provider at host startup — the store it fences against was not reachable or not yet " +
				"configured. Either (1) ensure the store (Redis/Consul/Kubernetes/MongoDB/Postgres/SQL " +
				"Server) is reachable before this host starts, or (2) call WithoutFencingTokens() on the " +
				"leader election builder (or WithoutFencingTokens() on the IServiceCollection for a " +
				"standalone Add*LeaderElection() entry point) if this host cannot guarantee that and does " +
				"not need fencing.",
				ex);
		}

		if (provider is null)
		{
			throw new InvalidOperationException(
				"Excalibur leader election fencing-token support was enabled via WithFencingTokens() " +
				"but no IFencingTokenProvider implementation is registered. Register a provider before " +
				"host startup — for example services.AddRedisFencingTokenProvider() " +
				"(Excalibur.LeaderElection.Redis), or services.AddFencingTokenSupport<TProvider>().");
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
